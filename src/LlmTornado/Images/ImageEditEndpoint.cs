using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Code.MimeTypeMap;
using LlmTornado.Images.Models;
using LlmTornado.Images.Models.OpenAi;

namespace LlmTornado.Images;

/// <summary>
///     Given a prompt, the model will generate a new image.
/// </summary>
public class ImageEditEndpoint : EndpointBase
{
    /// <summary>
    ///     Constructor of the api endpoint. Rather than instantiating this yourself, access it through an instance of
    ///     <see cref="TornadoApi" /> as <see cref="TornadoApi.ImageGenerations" />.
    /// </summary>
    /// <param name="api"></param>
    internal ImageEditEndpoint(TornadoApi api) : base(api)
    {
    }

    /// <summary>
    ///     The name of the endpoint, which is the final path segment in the API URL.  For example, "image".
    /// </summary>
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.ImageEdit;

    /// <summary>
    ///     Ask the API to edit an image with a given a prompt.
    /// </summary>
    /// <param name="request">Request to be sent</param>
    /// <returns>Asynchronously returns the image result. Look in its <see cref="TornadoGeneratedImage.Url" /> </returns>
    public async Task<ImageGenerationResult?> EditImage(ImageEditRequest request)
    {
        IEndpointProvider provider = Api.GetProvider(request.Model ?? ImageModelOpenAiGpt.Default);

        if (provider.Provider == LLmProviders.OpenAi)
        {
            request = GptImageModelHelper.SanitizeEditRequest(request);
        }
        
        // xAI uses JSON body instead of multipart form
        if (provider.Provider is LLmProviders.XAi or LLmProviders.Google)
        {
            return await EditImageJson(request, provider).ConfigureAwait(false);
        }
        
        // OpenAI GPT image models support JSON body when UseJsonBody is set
        if (provider.Provider == LLmProviders.OpenAi && request.UseJsonBody == true)
        {
            return await EditImageJson(request, provider).ConfigureAwait(false);
        }
        
        // OpenAI and other providers use multipart form
        return await EditImageMultipart(request, provider).ConfigureAwait(false);
    }
    
    /// <summary>
    ///     Edit an image using JSON body (for xAI and similar providers).
    /// </summary>
    private async Task<ImageGenerationResult?> EditImageJson(ImageEditRequest request, IEndpointProvider provider)
    {
        TornadoRequestContent requestBody = request.Serialize(provider);
        return await HttpPost1<ImageGenerationResult>(provider, Endpoint, requestBody.Url, postData: requestBody.Body).ConfigureAwait(false);
    }
    
    /// <summary>
    ///     Edit an image using multipart form (for OpenAI and similar providers).
    /// </summary>
    private async Task<ImageGenerationResult?> EditImageMultipart(ImageEditRequest request, IEndpointProvider provider)
    {
        using MultipartFormDataContent content = new MultipartFormDataContent();
        
        if (request.Images?.Count > 0)
        {
            int imageIndex = 0;
            
            foreach (TornadoInputFile img in request.Images)
            {
                if (img.Base64 is not null)
                {
                    string base64Data = img.Base64.StripDataUriPrefix();
                    byte[] imageBytes = Convert.FromBase64String(base64Data);
                    MemoryStream imageMs = new MemoryStream(imageBytes);
                    StreamContent imageSc = new StreamContent(imageMs);
                    imageSc.Headers.ContentLength = imageBytes.Length;

                    string mime = img.MimeType ?? "image/png";
                    imageSc.Headers.ContentType = new MediaTypeHeaderValue(mime);
                    string extension = MimeTypeMap.TryGetExtension(mime, out string? ext) ? ext : ".png";
                    content.Add(imageSc, "image[]", $"image{imageIndex}{extension}");
                    imageIndex++;
                }
            }
        }
        else
        {
            if (request.Image?.Base64 != null)
            {
                string base64Data = request.Image.Base64.StripDataUriPrefix();
                byte[] bytes = Convert.FromBase64String(base64Data);
                MemoryStream ms = new MemoryStream(bytes);
                StreamContent sc = new StreamContent(ms);
                sc.Headers.ContentLength = bytes.Length;

                string mime = request.Image.MimeType ?? "image/png";
                string extension = MimeTypeMap.TryGetExtension(mime, out string? ext) ? ext : ".png";
                
                sc.Headers.ContentType = new MediaTypeHeaderValue(request.Image.MimeType ?? "image/png");
                content.Add(sc, "image", $"image{extension}");
            }
        }

        content.Add(new StringContent(request.Prompt), "prompt");

        if (request.Model is not null)
        {
            content.Add(new StringContent(request.Model.Name), "model");   
        }

        if (request.Quality is not null)
        {
            string quality = request.Quality.Value switch
            { 
                TornadoImageQualities.Hd => "hd",
                TornadoImageQualities.Auto => "auto",
                TornadoImageQualities.High => "high",
                TornadoImageQualities.XHigh => "xhigh",
                TornadoImageQualities.Max => "max",
                TornadoImageQualities.Low => "low",
                TornadoImageQualities.Medium => "medium",
                TornadoImageQualities.Standard => "standard",
                _ => "auto"
            };
            
            content.Add(new StringContent(quality), "quality");   
        }

        if (request.Mask?.Base64 != null)
        {
            string base64Data = request.Mask.Base64.StripDataUriPrefix();
            byte[] maskBytes = Convert.FromBase64String(base64Data);
            MemoryStream maskMs = new MemoryStream(maskBytes);
            StreamContent maskSc = new StreamContent(maskMs);
            maskSc.Headers.ContentLength = maskBytes.Length;
            maskSc.Headers.ContentType = new MediaTypeHeaderValue(request.Mask.MimeType ?? "image/png");
            content.Add(maskSc, "mask", "mask.png");
        }

        if (request.Size is not null || request.Width is not null || request.Height is not null)
        {
            string? size = ImageGenerationRequest.GetSizeString(request.Size, request.Width, request.Height);
            if (size is not null)
            {
                content.Add(new StringContent(size), "size");
            }
        }

        if (request.User is not null)
        {
            content.Add(new StringContent(request.User), "user");   
        }

        if (request.ResponseFormat is not null)
        {
            string format = request.ResponseFormat.Value switch
            { 
                TornadoImageResponseFormats.Base64 => "b64_json",
                TornadoImageResponseFormats.Url => "url",
                _ => "b64_json"
            };
            
            content.Add(new StringContent(format), "response_format");   
        }

        if (request.NumOfImages is not null)
        {
            content.Add(new StringContent(request.NumOfImages.ToString() ?? string.Empty), "n");   
        }
        
        if (request.Background is not null)
        {
            string background = request.Background.Value switch
            {
                TornadoImageBackgrounds.Transparent => "transparent",
                TornadoImageBackgrounds.Opaque => "opaque",
                TornadoImageBackgrounds.Auto => "auto",
                _ => "auto"
            };
            
            content.Add(new StringContent(background), "background");
        }
        
        if (request.InputFidelity is not null)
        {
            string inputFidelity = request.InputFidelity.Value switch
            {
                TornadoImageInputFidelity.Low => "low",
                TornadoImageInputFidelity.High => "high",
                _ => "low"
            };
            
            content.Add(new StringContent(inputFidelity), "input_fidelity");
        }
        
        if (request.OutputFormat is not null)
        {
            string outputFormat = request.OutputFormat.Value switch
            {
                TornadoImageOutputFormats.Png => "png",
                TornadoImageOutputFormats.Jpeg => "jpeg",
                TornadoImageOutputFormats.Webp => "webp",
                _ => "png"
            };
            
            content.Add(new StringContent(outputFormat), "output_format");
        }
        
        if (request.OutputCompression is not null)
        {
            content.Add(new StringContent(request.OutputCompression.ToString() ?? string.Empty), "output_compression");
        }
        
        if (request.PartialImages is not null)
        {
            content.Add(new StringContent(request.PartialImages.ToString() ?? string.Empty), "partial_images");
        }
        
        if (request.Stream is not null)
        {
            // Note: Streaming is not yet supported in this implementation
            content.Add(new StringContent(request.Stream.Value ? "true" : "false"), "stream");
        }
        
        ImageGenerationResult? data = await HttpPost1<ImageGenerationResult>(provider, Endpoint, postData: content).ConfigureAwait(false);
        return data;
    }
}