using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.Serialization;
using LlmTornado.Code;
using LlmTornado.Images.Models;
using LlmTornado.Images.Models.OpenAi;
using LlmTornado.Images.Vendors.XAi;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LlmTornado.Images;

/// <summary>
///     Represents a request to the Images API.  Mostly matches the parameters in
///     <see href="https://platform.openai.com/docs/api-reference/images/create">the OpenAI docs</see>, although some have
///     been renamed or expanded into single/multiple properties for ease of use.
/// </summary>
public class ImageEditRequest
{
	/// <summary>
	///     Creates a new, empty <see cref="ImageGenerationRequest" />
	/// </summary>
	public ImageEditRequest()
    {
    }
	
	/// <summary>
	///     Creates a new, minimal <see cref="ImageGenerationRequest" />
	/// </summary>
	public ImageEditRequest(string prompt)
	{
		Prompt = prompt;
	}

	/// <summary>
	///     Creates a new <see cref="ImageEditRequest" /> with the specified parameters
	/// </summary>
	/// <param name="image"></param>
	/// <param name="prompt">A text description of the desired image(s). The maximum length is 1000 characters.</param>
	/// <param name="numOfImages">How many different choices to request for each prompt. Defaults to 1.</param>
	/// <param name="size">The size of the generated images. Must be one of 256x256, 512x512, or 1024x1024.</param>
	/// <param name="user">A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse.</param>
	/// <param name="responseFormat">The format in which the generated images are returned. Must be one of url or b64_json.</param>
	/// <param name="model">Which model will be used.</param>
	public ImageEditRequest(TornadoInputFile image, string prompt, int numOfImages = 1, TornadoImageSizes? size = null, string? user = null, TornadoImageResponseFormats? responseFormat = null, ImageModel? model = null)
    {
        Image = image;
        Prompt = prompt;
        NumOfImages = numOfImages;
        User = user;
        Size = size;
        ResponseFormat = responseFormat;
        Model = model;
    }
	
	/// <summary>
	///     A model to use.
	/// </summary>
	[JsonProperty("model")]
	[JsonConverter(typeof(ImageModelJsonConverter))]
	public ImageModel? Model { get; set; } = ImageModelOpenAiGpt.Default;

	/// <summary>
	/// The image(s) to edit. Must be a supported image file or an array of images. For gpt-image-1, each image should be a png, webp, or jpg file less than 25MB.
	/// For dall-e-2, you can only provide one image, and it should be a square png file less than 4MB.
	/// </summary>
	[JsonIgnore]
    public TornadoInputFile? Image { get; set; }
	
	/// <summary>
	/// The image(s) to edit. Must be a supported image file or an array of images. For gpt-image-1, each image should be a png, webp, or jpg file less than 25MB.
	/// For dall-e-2, you can only provide one image, and it should be a square png file less than 4MB.
	/// Setting this has priority over <see cref="Image"/>
	/// </summary>
	[JsonIgnore]
	public List<TornadoInputFile>? Images { get; set; }

	/// <summary>
	/// Serialized image/images.
	/// </summary>
	[JsonProperty("image")]
	internal object? SerializedImages => Images?.Count > 0 ? Images : Image;

	/// <summary>
	///     An additional image whose fully transparent areas (e.g. where alpha is zero) indicate where image should be edited.
	///     Must be a valid PNG file, less than 4MB, and have the same.
	/// </summary>
	[JsonProperty("mask")]
    public TornadoInputFile? Mask { get; set; }

	/// <summary>
	///     A text description of the desired image(s). The maximum length is 1000 characters for dall-e-2, and 32000 characters for gpt-image-1.
	/// </summary>
	[JsonProperty("prompt")]
    public string Prompt { get; set; }
	
	/// <summary>
	///     Number of images to generate
	/// </summary>
	[JsonProperty("n")]
    public int? NumOfImages { get; set; }

	/// <summary>
	///     The size of the generated images. Defaults to auto/1024x1024 depending on the model.
	///     For gpt-image-2, any resolution satisfying API constraints can be set via <see cref="Width"/> and <see cref="Height"/>.
	/// </summary>
	[JsonIgnore]
    public TornadoImageSizes? Size { get; set; }
	
	/// <summary>
	///     Custom width for the generated image. Used when <see cref="Size"/> is <see cref="TornadoImageSizes.Custom"/> or unset with height set.
	///     Supported by gpt-image-2 (width and height must be divisible by 16, aspect ratio between 1:3 and 3:1).
	/// </summary>
	[JsonIgnore]
	public int? Width { get; set; }
	
	/// <summary>
	///     Custom height for the generated image. Used when <see cref="Size"/> is <see cref="TornadoImageSizes.Custom"/> or unset with width set.
	/// </summary>
	[JsonIgnore]
	public int? Height { get; set; }
	
	/// <summary>
	///     Internal property for JSON serialization of the size parameter.
	/// </summary>
	[JsonProperty("size")]
	internal string? InternalSize => ImageGenerationRequest.GetSizeString(Size, Width, Height);
	
	/// <summary>
	///     Either empty or "hd" for dalle3.
	/// </summary>
	[JsonProperty("quality")]
	public TornadoImageQualities? Quality { get; set; }
	
	/// <summary>
	///     The format in which the generated images are returned. Must be one of url or b64_json. URLs are only valid for 60 minutes after the image has been generated. This parameter is only supported for dall-e-2, as gpt-image-1 will always return base64-encoded images.
	/// </summary>
	[JsonProperty("response_format")]
    public TornadoImageResponseFormats? ResponseFormat { get; set; }

	/// <summary>
	///     A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse. Optional.
	/// </summary>
	[JsonProperty("user")]
    public string? User { get; set; }
	
	/// <summary>
	///     Background transparency setting. Only supported for gpt-image-1. Must be one of transparent, opaque or auto (default).
	/// </summary>
	[JsonProperty("background")]
	public TornadoImageBackgrounds? Background { get; set; }
	
	/// <summary>
	///     Control how much effort the model will exert to match the style and features of input images.
	///     Supported by gpt-image-1 (not gpt-image-1-mini). Omit for gpt-image-2, which always uses high fidelity.
	/// </summary>
	[JsonProperty("input_fidelity")]
	public TornadoImageInputFidelity? InputFidelity { get; set; }
	
	/// <summary>
	///     The format in which the generated images are returned. Only supported for gpt-image-1. Must be one of png (default), jpeg, or webp.
	/// </summary>
	[JsonProperty("output_format")]
	public TornadoImageOutputFormats? OutputFormat { get; set; }
	
	/// <summary>
	///     The compression level (0-100%) for the generated images. Only supported for gpt-image-1 with webp or jpeg output formats. Defaults to 100.
	/// </summary>
	[JsonProperty("output_compression")]
	public int? OutputCompression { get; set; }
	
	/// <summary>
	///     The number of partial images to generate. Used for streaming responses that return partial images. Value must be between 0 and 3. When set to 0, the response will be a single image sent in one streaming event. Defaults to 0.
	/// </summary>
	[JsonProperty("partial_images")]
	public int? PartialImages { get; set; }
	
	/// <summary>
	///     Edit the image in streaming mode. Defaults to false. NOTE: Streaming is not yet supported in this implementation.
	/// </summary>
	[JsonProperty("stream")]
	public bool? Stream { get; set; }
	
	/// <summary>
	///		Features supported only by a single/few providers with no shared equivalent.
	/// </summary>
	[JsonIgnore]
	public ImageEditRequestVendorExtensions? VendorExtensions { get; set; }
	
	/// <summary>
	/// When set to true, the request will be sent as a JSON body instead of multipart form data.
	/// Only supported for GPT image models (gpt-image-1, gpt-image-1.5, gpt-image-1-mini, gpt-image-2, chatgpt-image-latest).
	/// In JSON mode, images are referenced via <see cref="ImageReferences"/> using <c>image_url</c> or <c>file_id</c>.
	/// </summary>
	[JsonIgnore]
	public bool? UseJsonBody { get; set; }
	
	internal ImageEditRequest(ImageEditRequest basedOn)
	{
		Model = basedOn.Model;
		Image = basedOn.Image;
		Images = basedOn.Images;
		Mask = basedOn.Mask;
		Prompt = basedOn.Prompt;
		NumOfImages = basedOn.NumOfImages;
		Size = basedOn.Size;
		Width = basedOn.Width;
		Height = basedOn.Height;
		Quality = basedOn.Quality;
		ResponseFormat = basedOn.ResponseFormat;
		User = basedOn.User;
		Background = basedOn.Background;
		InputFidelity = basedOn.InputFidelity;
		OutputFormat = basedOn.OutputFormat;
		OutputCompression = basedOn.OutputCompression;
		PartialImages = basedOn.PartialImages;
		Stream = basedOn.Stream;
		VendorExtensions = basedOn.VendorExtensions;
		UseJsonBody = basedOn.UseJsonBody;
		ImageReferences = basedOn.ImageReferences;
		MaskReference = basedOn.MaskReference;
	}
	
	/// <summary>
	/// Image references for JSON body mode. Each reference specifies an image via <c>image_url</c> or <c>file_id</c>.
	/// Used when <see cref="UseJsonBody"/> is true. This replaces the binary <see cref="Image"/>/<see cref="Images"/> properties.
	/// </summary>
	[JsonIgnore]
	public List<ImageEditReference>? ImageReferences { get; set; }
	
	/// <summary>
	/// Mask reference for JSON body mode. Specifies a mask via <c>image_url</c> or <c>file_id</c>.
	/// Used when <see cref="UseJsonBody"/> is true. This replaces the binary <see cref="Mask"/> property.
	/// </summary>
	[JsonIgnore]
	public ImageEditReference? MaskReference { get; set; }
	
	/// <summary>
	///		Serializes the image edit request into the request body, based on the conventions used by the LLM provider.
	/// </summary>
	/// <param name="provider"></param>
	/// <returns></returns>
	public TornadoRequestContent Serialize(IEndpointProvider provider)
	{
		return SerializeMap.TryGetValue(provider.Provider, out Func<ImageEditRequest, IEndpointProvider, string>? serializerFn) 
			? new TornadoRequestContent(serializerFn.Invoke(this, provider), Model, provider.Provider is LLmProviders.Google ? $"{provider.ApiUrl(CapabilityEndpoints.ImageGeneration, null)}/{Model?.Name}:generateContent" : null, provider, CapabilityEndpoints.ImageEdit)
			: new TornadoRequestContent(string.Empty, Model, provider.Provider is LLmProviders.Google ? $"{provider.ApiUrl(CapabilityEndpoints.ImageGeneration, null)}/{Model?.Name}:generateContent" : null, provider, CapabilityEndpoints.ImageEdit);
	}
	
	private static readonly FrozenDictionary<LLmProviders, Func<ImageEditRequest, IEndpointProvider, string>> SerializeMap = new Dictionary<LLmProviders, Func<ImageEditRequest, IEndpointProvider, string>>
	{
        { LLmProviders.Google, (x, y) => JsonConvert.SerializeObject(new Vendors.Google.VendorGoogleGeminiImageRequest(x, y), EndpointBase.NullSettings) },
		{ LLmProviders.XAi, (x, y) => JsonConvert.SerializeObject(new VendorXAiImageEditRequest(x, y), EndpointBase.NullSettings) },
		{ LLmProviders.OpenAi, (x, y) => JsonConvert.SerializeObject(new VendorOpenAiImageEditJsonRequest(GptImageModelHelper.SanitizeEditRequest(x)), EndpointBase.NullSettings) }
	}.ToFrozenDictionary();
}

/// <summary>
/// An image reference for JSON body image edit requests. Specifies an image via URL or file ID.
/// </summary>
public class ImageEditReference
{
	/// <summary>
	/// The type of reference. One of "image_url" or "file_id".
	/// </summary>
	[JsonProperty("type")]
	[JsonConverter(typeof(StringEnumConverter))]
	public ImageEditReferenceType Type { get; set; }
	
	/// <summary>
	/// The URL of the image. Used when <see cref="Type"/> is <see cref="ImageEditReferenceType.ImageUrl"/>.
	/// Can be a fully qualified URL or a base64-encoded data URI.
	/// </summary>
	[JsonProperty("image_url")]
	public string? ImageUrl { get; set; }
	
	/// <summary>
	/// The file ID of the image. Used when <see cref="Type"/> is <see cref="ImageEditReferenceType.FileId"/>.
	/// </summary>
	[JsonProperty("file_id")]
	public string? FileId { get; set; }

	/// <summary>
	/// Creates an empty image reference.
	/// </summary>
	public ImageEditReference()
	{
	}

	/// <summary>
	/// Creates an image reference from a URL.
	/// </summary>
	/// <param name="imageUrl">The URL or data URI of the image.</param>
	public static ImageEditReference FromUrl(string imageUrl)
	{
		return new ImageEditReference
		{
			Type = ImageEditReferenceType.ImageUrl,
			ImageUrl = imageUrl
		};
	}

	/// <summary>
	/// Creates an image reference from a file ID.
	/// </summary>
	/// <param name="fileId">The file ID of the image.</param>
	public static ImageEditReference FromFileId(string fileId)
	{
		return new ImageEditReference
		{
			Type = ImageEditReferenceType.FileId,
			FileId = fileId
		};
	}
}

/// <summary>
/// The type of image reference for JSON body image edit requests.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ImageEditReferenceType
{
	/// <summary>
	/// Reference by URL (fully qualified URL or base64 data URI).
	/// </summary>
	[EnumMember(Value = "image_url")]
	ImageUrl,

	/// <summary>
	/// Reference by file ID (previously uploaded file).
	/// </summary>
	[EnumMember(Value = "file_id")]
	FileId
}

/// <summary>
/// OpenAI-specific JSON body format for image edit requests (GPT image models only).
/// </summary>
internal class VendorOpenAiImageEditJsonRequest
{
	[JsonProperty("model")]
	public string? Model { get; set; }
	
	[JsonProperty("prompt")]
	public string? Prompt { get; set; }
	
	[JsonProperty("image")]
	public object? Image { get; set; }
	
	[JsonProperty("mask")]
	public ImageEditReference? Mask { get; set; }
	
	[JsonProperty("n")]
	public int? N { get; set; }
	
	[JsonProperty("size")]
	public string? Size { get; set; }
	
	[JsonProperty("quality")]
	public string? Quality { get; set; }
	
	[JsonProperty("background")]
	public string? Background { get; set; }
	
	[JsonProperty("input_fidelity")]
	public string? InputFidelity { get; set; }
	
	[JsonProperty("output_format")]
	public string? OutputFormat { get; set; }
	
	[JsonProperty("output_compression")]
	public int? OutputCompression { get; set; }
	
	[JsonProperty("response_format")]
	public string? ResponseFormat { get; set; }
	
	[JsonProperty("user")]
	public string? User { get; set; }

	public VendorOpenAiImageEditJsonRequest(ImageEditRequest request)
	{
		Model = request.Model?.GetApiName;
		Prompt = request.Prompt;
		N = request.NumOfImages;
		User = request.User;
		OutputCompression = request.OutputCompression;
		
		// Map image references
		if (request.ImageReferences is { Count: > 0 })
		{
			Image = request.ImageReferences.Count == 1 ? request.ImageReferences[0] : (object)request.ImageReferences;
		}
		
		// Map mask reference
		Mask = request.MaskReference;
		
		Size = ImageGenerationRequest.GetSizeString(request.Size, request.Width, request.Height);
		
		// Map quality
		if (request.Quality.HasValue)
		{
			Quality = request.Quality.Value switch
			{
				TornadoImageQualities.Low => "low",
				TornadoImageQualities.Medium => "medium",
				TornadoImageQualities.High or TornadoImageQualities.Hd => "high",
				TornadoImageQualities.XHigh => "xhigh",
				TornadoImageQualities.Max => "max",
				TornadoImageQualities.Auto => "auto",
				_ => "auto"
			};
		}
		
		// Map background
		if (request.Background.HasValue)
		{
			Background = request.Background.Value switch
			{
				TornadoImageBackgrounds.Transparent => "transparent",
				TornadoImageBackgrounds.Opaque => "opaque",
				TornadoImageBackgrounds.Auto => "auto",
				_ => "auto"
			};
		}
		
		// Map input fidelity
		if (request.InputFidelity.HasValue)
		{
			InputFidelity = request.InputFidelity.Value switch
			{
				TornadoImageInputFidelity.Low => "low",
				TornadoImageInputFidelity.High => "high",
				_ => "low"
			};
		}
		
		// Map output format
		if (request.OutputFormat.HasValue)
		{
			OutputFormat = request.OutputFormat.Value switch
			{
				TornadoImageOutputFormats.Png => "png",
				TornadoImageOutputFormats.Jpeg => "jpeg",
				TornadoImageOutputFormats.Webp => "webp",
				_ => "png"
			};
		}
		
		// Map response format
		if (request.ResponseFormat.HasValue)
		{
			ResponseFormat = request.ResponseFormat.Value switch
			{
				TornadoImageResponseFormats.Base64 => "b64_json",
				TornadoImageResponseFormats.Url => "url",
				_ => "b64_json"
			};
		}
	}
}