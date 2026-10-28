using System;
using System.Collections.Generic;
using System.Linq;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Chat.Vendors.Cohere;
using LlmTornado.Code;
using LlmTornado.Chat;
using LlmTornado.Embedding.Models;
using LlmTornado.Embedding.Vendors.Google;
using LlmTornado.Embedding.Vendors.Mistral;
using LlmTornado.Embedding.Vendors.OpenAi;
using LlmTornado.Embedding.Vendors.Voyage;
using LlmTornado.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Extensions = LlmTornado.Code.Extensions;

namespace LlmTornado.Embedding;

/// <summary>
///     Represents a request to the Embeddings API.
/// </summary>
public class EmbeddingRequest
{
	/// <summary>
	///     Creates a new, empty <see cref="EmbeddingRequest" />.
	/// </summary>
	public EmbeddingRequest()
    {
    }

	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified parameters
	/// </summary>
	/// <param name="model">
	///     The model to use. You can use <see cref="ModelsEndpoint.GetModels" /> to see all of your
	///     available models, or use a standard model like <see cref="Model.TextEmbedding3Small" />,
	///     <see cref="Model.TextEmbedding3Large" /> or (legacy) <see cref="Model.AdaTextEmbedding" />.
	/// </param>
	/// <param name="input">The prompt to transform</param>
	/// <param name="dimensions">
	///     The dimensions length to return. The maximum value is the size supported by model. This is
	///     only supported in newer embedding models.
	/// </param>
	public EmbeddingRequest(EmbeddingModel model, string input, int dimensions)
    {
        Model = model;
        InputScalar = input;
        Dimensions = dimensions;
    }
	
	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified parameters
	/// </summary>
	/// <param name="model">
	///     The model to use. You can use <see cref="ModelsEndpoint.GetModels" /> to see all of your
	///     available models, or use a standard model like <see cref="Model.TextEmbedding3Small" />,
	///     <see cref="Model.TextEmbedding3Large" /> or (legacy) <see cref="Model.AdaTextEmbedding" />.
	/// </param>
	/// <param name="input">The prompt to transform</param>
	/// <param name="dimensions">
	///     The dimensions length to return. The maximum value is the size supported by model. This is
	///     only supported in newer embedding models.
	/// </param>
	/// <param name="extensions">Vendor extensions</param>
	public EmbeddingRequest(EmbeddingModel model, string input, int dimensions, EmbeddingRequestVendorExtensions extensions)
	{
		Model = model;
		InputScalar = input;
		Dimensions = dimensions;
		VendorExtensions = extensions;
	}

	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified input and the
	///     <see cref="Model.AdaTextEmbedding" /> model.
	/// </summary>
	/// <param name="model">The model to use</param>
	/// <param name="input">The prompt to transform</param>
	public EmbeddingRequest(EmbeddingModel model, string input)
    {
        Model = model;
        InputScalar = input;
    }
	
	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified input and the
	///     <see cref="Model.AdaTextEmbedding" /> model.
	/// </summary>
	/// <param name="model">The model to use</param>
	/// <param name="input">The prompt to transform</param>
	/// <param name="extensions">Vendor extensions</param>
	public EmbeddingRequest(EmbeddingModel model, string input, EmbeddingRequestVendorExtensions extensions)
	{
		Model = model;
		InputScalar = input;
		VendorExtensions = extensions;
	}

	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified input and the
	///     <see cref="Model.AdaTextEmbedding" /> model.
	/// </summary>
	/// <param name="model">The model to use</param>
	/// <param name="input">The prompt to transform</param>
	public EmbeddingRequest(EmbeddingModel model, IEnumerable<string> input)
	{
		Model = model;
		InputVector = input.ToList();
	}
	
	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified input and the
	///     <see cref="Model.AdaTextEmbedding" /> model.
	/// </summary>
	/// <param name="model">The model to use</param>
	/// <param name="input">The prompt to transform</param>
	public EmbeddingRequest(EmbeddingModel model, IList<string> input)
	{
		Model = model;
		InputVector = input;
	}
	
	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified input and the
	///     <see cref="Model.AdaTextEmbedding" /> model.
	/// </summary>
	/// <param name="model">The model to use</param>
	/// <param name="input">The prompt to transform</param>
	/// <param name="extensions">Vendor extensions</param>
	public EmbeddingRequest(EmbeddingModel model, IEnumerable<string> input, EmbeddingRequestVendorExtensions extensions)
	{
		Model = model;
		InputVector = input.ToList();
		VendorExtensions = extensions;
	}
	
	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified input and the
	///     <see cref="Model.AdaTextEmbedding" /> model.
	/// </summary>
	/// <param name="model">The model to use</param>
	/// <param name="input">The prompt to transform</param>
	/// <param name="extensions">Vendor extensions</param>
	public EmbeddingRequest(EmbeddingModel model, IList<string> input, EmbeddingRequestVendorExtensions extensions)
	{
		Model = model;
		InputVector = input;
		VendorExtensions = extensions;
	}
	
	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified input and the
	///     <see cref="Model.AdaTextEmbedding" /> model.
	/// </summary>
	/// <param name="model">The model to use</param>
	/// <param name="input">The prompt to transform</param>
	/// <param name="dimensions">Output dimensions</param>
	public EmbeddingRequest(EmbeddingModel model, IEnumerable<string> input, int dimensions)
	{
		Model = model;
		InputVector = input.ToList();
		Dimensions = dimensions;
	}
	
	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified input and the
	///     <see cref="Model.AdaTextEmbedding" /> model.
	/// </summary>
	/// <param name="model">The model to use</param>
	/// <param name="input">The prompt to transform</param>
	/// <param name="dimensions">Output dimensions</param>
	public EmbeddingRequest(EmbeddingModel model, IList<string> input, int dimensions)
	{
		Model = model;
		InputVector = input;
		Dimensions = dimensions;
	}
	
	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified input and the
	///     <see cref="Model.AdaTextEmbedding" /> model.
	/// </summary>
	/// <param name="model">The model to use</param>
	/// <param name="input">The prompt to transform</param>
	/// <param name="dimensions">Output dimensions</param>
	/// <param name="extensions">Vendor extensions</param>
	public EmbeddingRequest(EmbeddingModel model, IEnumerable<string> input, int dimensions, EmbeddingRequestVendorExtensions extensions)
	{
		Model = model;
		InputVector = input.ToList();
		Dimensions = dimensions;
		VendorExtensions = extensions;
	}
	
	/// <summary>
	///     Creates a new <see cref="EmbeddingRequest" /> with the specified input and the
	///     <see cref="Model.AdaTextEmbedding" /> model.
	/// </summary>
	/// <param name="model">The model to use</param>
	/// <param name="input">The prompt to transform</param>
	/// <param name="dimensions">Output dimensions</param>
	/// <param name="extensions">Vendor extensions</param>
	public EmbeddingRequest(EmbeddingModel model, IList<string> input, int dimensions, EmbeddingRequestVendorExtensions extensions)
	{
		Model = model;
		InputVector = input;
		Dimensions = dimensions;
		VendorExtensions = extensions;
	}

	/// <summary>
	///     Model to use.
	/// </summary>
	[JsonProperty("model")]
	[JsonConverter(typeof(ModelJsonConverter))]
    public EmbeddingModel Model { get; set; }

	/// <summary>
	///		Features supported only by a single/few providers with no shared equivalent.
	/// </summary>
	[JsonIgnore]
	public EmbeddingRequestVendorExtensions? VendorExtensions { get; set; }
	
	/// <summary>
	///     Main text to be embedded
	/// </summary>
	[JsonIgnore]
    public string? InputScalar { get; set; }
	
	/// <summary>
	///     Main text to be embedded
	/// </summary>
	[JsonIgnore]
	public IList<string>? InputVector { get; set; }

    /// <summary>
    /// Multimodal input.
    /// </summary>
    [JsonIgnore]
    public IList<IList<ChatMessagePart>>? MultimodalInput { get; set; }

	[JsonProperty("input")]
	internal object InputSerialized { get; set; }

	/// <summary>
	///     The dimensions length to be returned. Only supported by newer models.
	/// </summary>
	[JsonProperty("dimensions")]
    public int? Dimensions { get; set; }
	
	/// <summary>
	///		Precision and format of the embeddings. Currently supported by Voyage and Mistral.
	/// </summary>
	[JsonIgnore]
	public EmbeddingOutputDtypes? OutputDType { get; set; }
	
	[JsonIgnore]
	internal string? UrlOverride { get; set; }
	
	/// <summary>
	///		Serializes the embedding request into the request body, based on the conventions used by the LLM provider.
	/// </summary>
	/// <param name="provider"></param>
	/// <returns></returns>
	public TornadoRequestContent Serialize(IEndpointProvider provider)
	{
		string content = provider.Provider switch
		{
			LLmProviders.OpenAi => PreparePayload(new VendorOpenAiEmbeddingRequest(this, provider), this, provider, EndpointBase.NullSettings),
			LLmProviders.Mistral => PreparePayload(new VendorMistralEmbeddingRequest(this, provider), this, provider, EndpointBase.NullSettings),
			LLmProviders.Cohere => PreparePayload(new VendorCohereEmbeddingRequest(this, provider), this, provider, EndpointBase.NullSettings),
			LLmProviders.Google => PreparePayload(new VendorGoogleEmbeddingRequest(this, provider), this, provider, EndpointBase.NullSettings),
			LLmProviders.Voyage => PreparePayload(new VendorVoyageEmbeddingRequest(this, provider), this, provider, EndpointBase.NullSettings),
			LLmProviders.OpenRouter => PreparePayload(new VendorOpenAiEmbeddingRequest(this, provider), this, provider, EndpointBase.NullSettings),
			LLmProviders.Requesty => PreparePayload(new VendorOpenAiEmbeddingRequest(this, provider), this, provider, EndpointBase.NullSettings),
			LLmProviders.LiteLlm => PreparePayload(new VendorOpenAiEmbeddingRequest(this, provider), this, provider, EndpointBase.NullSettings),
			LLmProviders.Upstage => PreparePayload(new VendorOpenAiEmbeddingRequest(this, provider), this, provider, EndpointBase.NullSettings),
			_ => string.Empty
		};
		
		return new TornadoRequestContent(content, Model, UrlOverride, provider, CapabilityEndpoints.Embeddings);
	}
	
	private static string PreparePayload(object sourceObject, EmbeddingRequest context, IEndpointProvider provider, JsonSerializerSettings? settings)
	{
		return sourceObject.SerializeRequestObject(context, provider, RequestActionTypes.EmbeddingCreate, settings);
	}
	
	internal void OverrideUrl(string url)
	{
		UrlOverride = url;
	}
	
	internal class ModelJsonConverter : JsonConverter<EmbeddingModel>
	{
		public override void WriteJson(JsonWriter writer, EmbeddingModel? value, JsonSerializer serializer)
		{
			writer.WriteValue(value?.Name);
		}

		public override EmbeddingModel? ReadJson(JsonReader reader, Type objectType, EmbeddingModel? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			return existingValue;
		}
	}
}