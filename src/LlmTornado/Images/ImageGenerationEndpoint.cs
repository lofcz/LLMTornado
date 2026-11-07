using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Images.Models;
using LlmTornado.Images.Models.OpenAi;

namespace LlmTornado.Images;

/// <summary>
///     Given a prompt, the model will generate a new image.
/// </summary>
public class ImageGenerationEndpoint : EndpointBase
{
	/// <summary>
	///     Constructor of the api endpoint. Rather than instantiating this yourself, access it through an instance of
	///     <see cref="TornadoApi" /> as <see cref="TornadoApi.ImageGenerations" />.
	/// </summary>
	/// <param name="api"></param>
	internal ImageGenerationEndpoint(TornadoApi api) : base(api)
    {
    }

	/// <summary>
	///     The name of the endpoint, which is the final path segment in the API URL.  For example, "image".
	/// </summary>
	protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.ImageGeneration;
	
	/// <summary>
	///     Ask the API to create an image given a prompt.
	/// </summary>
	/// <param name="input">A text description of the desired image(s)</param>
	/// <returns>Asynchronously returns the image result. Look in its <see cref="ImageGenerationResult"/></returns>
	public Task<ImageGenerationResult?> CreateImage(string input)
    {
        ImageGenerationRequest req = new ImageGenerationRequest(input);
        return CreateImage(req);
    }

	/// <summary>
	///     Ask the API to create an image given a prompt.
	/// </summary>
	/// <param name="request">Request to be sent</param>
	/// <returns>Asynchronously returns the image result. Look in its <see cref="ImageGenerationResult"/></returns>
	public Task<ImageGenerationResult?> CreateImage(ImageGenerationRequest request)
    {
	    IEndpointProvider provider = Api.GetProvider(request.Model ?? ImageModelOpenAiGpt.Default);
	    TornadoRequestContent requestBody = request.Serialize(provider);
	    
        return HttpPost1<ImageGenerationResult>(provider, Endpoint, requestBody.Url, postData: requestBody.Body, ct: request.CancellationToken);
    }
}