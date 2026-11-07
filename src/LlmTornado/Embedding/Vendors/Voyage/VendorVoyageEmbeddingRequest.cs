using System;
using System.Collections.Generic;
using LlmTornado.Chat;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Embedding.Vendors.Voyage;

internal class VendorVoyageEmbeddingRequest
{
    /// <summary>
    ///     Model to use.
    /// </summary>
    [JsonProperty("model")]
    public string Model { get; set; }

    /// <summary>
    ///     Multimodal inputs.
    /// </summary>
    [JsonProperty("inputs")]
    public List<VendorVoyageMultimodalInput>? Inputs { get; set; }

    /// <summary>
    ///     string | string[] | null
    /// </summary>
    [JsonProperty("input")]
    internal object? Input { get; set; }
    
    /// <summary>
    /// Null | query | document
    /// </summary>
    [JsonProperty("input_type")]
    public string? InputType { get; set; }
    
    /// <summary>
    /// Whether to truncate the input texts to fit within the context length. Defaults to true.
    /// </summary>
    [JsonProperty("truncation")]
    public bool? Truncation { get; set; }
    
    /// <summary>
    /// float (default) | int8 | uint8 | binary | ubinary 
    /// </summary>
    /// <returns></returns>
    [JsonProperty("output_dtype")]
    public string? OutputDtype { get; set; }

    /// <summary>
    /// Format in which the embeddings are encoded. Defaults to null. Other options: base64.
    /// </summary>
    [JsonProperty("encoding_format")]
    public string? EncodingFormat { get; set; }
    
    /// <summary>
    ///     The dimensions length to be returned. Only supported by newer models.
    /// </summary>
    [JsonProperty("output_dimension")]
    public int? Dimensions { get; set; }
    
    public VendorVoyageEmbeddingRequest(EmbeddingRequest request, IEndpointProvider provider)
    {
        Model = request.Model.Name;

        if (request.InputVector is not null)
        {
            Input = request.InputVector;
        }
        else
        {
            Input = request.InputScalar ?? string.Empty;
        }

        if (request.MultimodalInput is not null)
        {
            Inputs = [];
            request.OverrideUrl("/multimodalembeddings");

            foreach (IList<ChatMessagePart> inputParts in request.MultimodalInput)
            {
                VendorVoyageMultimodalInput input = new VendorVoyageMultimodalInput();

                foreach (ChatMessagePart part in inputParts)
                {
                    VendorVoyageMultimodalContent content = new VendorVoyageMultimodalContent();

                    if (part.Type == ChatMessageTypes.Text)
                    {
                        content.Type = "text";
                        content.Text = part.Text;
                    }
                    else if (part.Type == ChatMessageTypes.Image)
                    {
                        if (part.Image?.Url is null)
                        {
                            continue;
                        }
                        
                        string url = part.Image.Url;

                        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            content.Type = "image_base64";
                            content.ImageBase64 = url;
                        }
                        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            content.Type = "image_url";
                            content.ImageUrl = url;
                        }
                        else
                        {
                            // ChatImage.Url may hold raw base64; Voyage expects a data URI in image_base64
                            content.Type = "image_base64";
                            content.ImageBase64 = $"data:{part.Image.MimeType ?? "image/jpeg"};base64,{url}";
                        }
                    }
                    else if (part.Type == ChatMessageTypes.Video)
                    {
                        if (part.Video?.Url is null)
                        {
                            continue;
                        }
                        
                        string url = part.Video.Url.ToString();

                        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            content.Type = "video_base64";
                            content.VideoBase64 = url;
                        }
                        else
                        {
                            content.Type = "video_url";
                            content.VideoUrl = url;
                        }
                    }

                    input.Content.Add(content);
                }
                
                Inputs.Add(input);
            }
            
            Input = null;
        }

        Dimensions = request.Dimensions;

        if (request.OutputDType is not null)
        {
            OutputDtype = request.OutputDType switch
            {
                null => null,
                EmbeddingOutputDtypes.Float => "float",
                EmbeddingOutputDtypes.Int8 => "int8",
                EmbeddingOutputDtypes.Uint8 => "uint8",
                EmbeddingOutputDtypes.Binary => "binary",
                EmbeddingOutputDtypes.Ubinary => "ubinary",
                _ => "float"
            };
        }
        
        if (request.VendorExtensions?.Voyage is not null)
        {
            Truncation = request.VendorExtensions.Voyage.Truncation;
            InputType = request.VendorExtensions.Voyage.InputType switch
            {
                null => null,
                EmbeddingVendorVoyageInputTypes.Query => "query",
                EmbeddingVendorVoyageInputTypes.Document => "document",
                _ => null
            };
            EncodingFormat = request.VendorExtensions.Voyage.EncodingFormat switch
            {
                EmbeddingVendorVoyageEncodingFormats.Base64 => "base64",
                _ => null
            };
        }
    }
}

internal class VendorVoyageMultimodalInput
{
    [JsonProperty("content")]
    public List<VendorVoyageMultimodalContent> Content { get; set; } = [];
}

internal class VendorVoyageMultimodalContent
{
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public string? Text { get; set; }
    
    [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? ImageUrl { get; set; }
    
    [JsonProperty("image_base64", NullValueHandling = NullValueHandling.Ignore)]
    public string? ImageBase64 { get; set; }

    [JsonProperty("video_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? VideoUrl { get; set; }
    
    [JsonProperty("video_base64", NullValueHandling = NullValueHandling.Ignore)]
    public string? VideoBase64 { get; set; }
}