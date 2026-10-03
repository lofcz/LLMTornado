using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Models.Perplexity;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Chat.Vendors.Alibaba;
using LlmTornado.Chat.Vendors.Cohere;
using LlmTornado.Chat.Vendors.Mistral;
using LlmTornado.Chat.Vendors.Perplexity;
using LlmTornado.Chat.Vendors.XAi;
using LlmTornado.Chat.Vendors.Zai;
using LlmTornado.Chat.Vendors.MiniMax;
using LlmTornado.Chat.Vendors.MoonshotAi;
using LlmTornado.Code.Models;
using LlmTornado.Images;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using LlmTornado.Chat.Vendors.Google;
using LlmTornado.Chat.Vendors.Groq;
using LlmTornado.Responses;

namespace LlmTornado.Chat;

/// <summary>
///     A request to the Chat API. This is similar, but not exactly the same as the
///     <see cref="Completions.CompletionRequest" />
///     Based on the <see href="https://platform.openai.com/docs/api-reference/chat">OpenAI API docs</see>
/// </summary>
public class ChatRequest : IModelRequest, ISerializableRequest, IHeaderProvider
{
	/// <summary>
	///     Creates a new, empty <see cref="ChatRequest" />
	/// </summary>
	public ChatRequest()
    {
	   
    }

	/// <summary>
	///     Create a new chat request enlisted in a conversation, using the data from the input chat request.
	/// </summary>
	/// <param name="conversation"></param>
	/// <param name="basedOn"></param>
	internal ChatRequest(Conversation conversation, ChatRequest? basedOn)
	{
		OwnerConversation = conversation;

		if (basedOn is not null)
		{
			CopyData(basedOn);
		}
	}

	/// <summary>
	///     Create a new chat request using the data from the input chat request.
	/// </summary>
	/// <param name="basedOn"></param>
	public ChatRequest(ChatRequest? basedOn)
    {
	    if (basedOn is null)
	    {
		    return;
	    }

	    CopyData(basedOn);
    }

	private void CopyData(ChatRequest basedOn)
	{
		Model = basedOn.Model;
		Messages = basedOn.Messages;
		Temperature = basedOn.Temperature;
		TopP = basedOn.TopP;
		NumChoicesPerMessage = basedOn.NumChoicesPerMessage;
		StopSequence = basedOn.StopSequence;
		MultipleStopSequences = basedOn.MultipleStopSequences;
		MaxTokens = basedOn.MaxTokens;
		FrequencyPenalty = basedOn.FrequencyPenalty;
		PresencePenalty = basedOn.PresencePenalty;
		LogitBias = basedOn.LogitBias;
		Tools = basedOn.Tools;
		ToolChoice = basedOn.ToolChoice;
		OutboundFunctionsContent = basedOn.OutboundFunctionsContent;
		Adapter = basedOn.Adapter;
		VendorExtensions = basedOn.VendorExtensions;
		StreamOptions = basedOn.StreamOptions;
		TrimResponseStart = basedOn.TrimResponseStart;
		ParallelToolCalls = basedOn.ParallelToolCalls;
		Seed = basedOn.Seed;
		User = basedOn.User;
		ResponseFormat = basedOn.ResponseFormat;
		Audio = basedOn.Audio;
		ImageOutput = basedOn.ImageOutput;
		Modalities = basedOn.Modalities;
		Metadata = basedOn.Metadata;
		Store = basedOn.Store;
		ReasoningEffort = basedOn.ReasoningEffort;
		Prediction = basedOn.Prediction;
		ServiceTier = basedOn.ServiceTier;
		Speed = basedOn.Speed;
		Stream = basedOn.Stream;
		ReasoningBudget = basedOn.ReasoningBudget;
		Logprobs = basedOn.Logprobs;
		TopLogprobs = basedOn.TopLogprobs;
		WebSearchOptions = basedOn.WebSearchOptions;
		OnSerialize = basedOn.OnSerialize;
		ResponseRequestParameters = basedOn.ResponseRequestParameters;
		UseResponseEndpoint = basedOn.UseResponseEndpoint;
		ReasoningFormat = basedOn.ReasoningFormat;
		IncludeReasoning = basedOn.IncludeReasoning;
		Verbosity = basedOn.Verbosity;
		SafetyIdentifier = basedOn.SafetyIdentifier;
		PromptCacheKey = basedOn.PromptCacheKey;
		PromptCacheRetention = basedOn.PromptCacheRetention;
		AutoCache = basedOn.AutoCache;
		CancellationToken = basedOn.CancellationToken;
		InvokeClrToolsAutomatically = basedOn.InvokeClrToolsAutomatically;
	}

	/// <summary>
	///     The model to use for this request
	/// </summary>
	[JsonProperty("model")]
	[JsonConverter(typeof(ChatModelJsonConverter))]
	public ChatModel? Model { get; set; } = ChatModel.OpenAi.Gpt35.Turbo;

	[JsonIgnore]
	IModel? IModelRequest.RequestModel => Model;

	/// <summary>
	///		Modalities of the model. Can be omitted for text only conversations.
	///		For audio, OpenAI requires both: <see cref="ChatModelModalities.Text"/> and <see cref="ChatModelModalities.Audio"/>, using only <see cref="ChatModelModalities.Audio"/> is invalid.
	/// </summary>
	[JsonProperty("modalities")]
	[JsonConverter(typeof(ModalitiesJsonConverter))]
	public List<ChatModelModalities>? Modalities { get; set; }
	
	/// <summary>
	///		Parameters for audio output. Required when audio output is requested with <see cref="Modalities"/>: ["<see cref="ChatModelModalities.Audio"/>"].
	///		Currently only works with OpenAI models.
	/// </summary>
	[JsonProperty("audio")]
	public ChatRequestAudio? Audio { get; set; }
	
	/// <summary>
	///		Parameters for image output. Optional when image output is requested with <see cref="Modalities"/>: ["<see cref="ChatModelModalities.Image"/>"].
	///		Allows configuring aspect ratio and resolution of generated images.
	/// </summary>
	[JsonIgnore]
	public ChatImageOutputConfig? ImageOutput { get; set; }
	
	/// <summary>
	/// Set of 16 key-value pairs that can be attached to an object. This can be useful for storing additional information about the object in a structured format, and querying for objects via API or the dashboard.<br/>
	/// Keys are strings with a maximum length of 64 characters. Values are strings with a maximum length of 512 characters.
	/// </summary>
	[JsonProperty("metadata")]
	public Dictionary<string, string>? Metadata { get; set; }
	
	/// <summary>
	///		Whether to store the output of this chat completion request for use in model distillation, evals, or later retrieval.
	///		Supported by OpenAI (as "store") and xAI (mapped to "store_messages"). Defaults to false for xAI.
	/// </summary>
	[JsonProperty("store")]
	public bool? Store { get; set; }

	/// <summary>
	///     The messages to send with this Chat Request
	/// </summary>
	[JsonProperty("messages")]
    [JsonConverter(typeof(ChatMessageRequestMessagesJsonConverter))]
    public List<ChatMessage>? Messages { get; set; }

	/// <summary>
	///     What sampling temperature to use. Higher values means the model will take more risks. Try 0.9 for more creative
	///     applications, and 0 (argmax sampling) for ones with a well-defined answer. It is generally recommend to use this or
	///     <see cref="TopP" /> but not both.
	/// </summary>
	[JsonProperty("temperature")]
    public double? Temperature { get; set; }

	/// <summary>
	/// Allows transforming the request on JSON level, before it is serialized into a string.
	/// </summary>
	[JsonIgnore]
	public Action<JObject, ChatRequest>? OnSerialize { get; set; }
	
	/// <summary>
	/// If set, the request may be promoted from <see cref="CapabilityEndpoints.Chat"/> to <see cref="CapabilityEndpoints.Responses"/>. Currently supported by OpenAI and Groq.<br/>
	/// </summary>
	[JsonIgnore]
	public ResponseRequest? ResponseRequestParameters { get; set; }
	
	/// <summary>
	///     An alternative to sampling with temperature, called nucleus sampling, where the model considers the results of the
	///     tokens with top_p probability mass. So 0.1 means only the tokens comprising the top 10% probability mass are
	///     considered. It is generally recommend to use this or <see cref="Temperature" /> but not both.
	/// </summary>
	[JsonProperty("top_p")]
    public double? TopP { get; set; }

	/// <summary>
	///     How many different choices to request for each message. Defaults to 1.
	/// </summary>
	[JsonProperty("n")]
    public int? NumChoicesPerMessage { get; set; }
	
	/// <summary>
	///     Balance option between response time and cost/latency. Supported by O1, O1 Mini, Grok 3+, Sonar Deep Research, Qwen, Anthropic (maps to <c>output_config.effort</c>), Kimi K3 (low / high / max), and Mistral Small 4 / Medium 3.5 (<c>none</c> / <c>high</c>).<br/>
	///     xAI: Grok 4.3 accepts none / low (default) / medium / high / xhigh; Grok 4.6 accepts low / medium / high (default) / xhigh.
	///     For Alibaba/DashScope, this maps to <c>enable_thinking</c> (and <see cref="ReasoningBudget"/> maps to <c>thinking_budget</c>).
	///     For Anthropic-specific control, prefer <see cref="ChatRequestVendorAnthropicExtensions.Effort"/>.
	/// </summary>
	[JsonProperty("reasoning_effort")]
	public ChatReasoningEfforts? ReasoningEffort { get; set; }
	
	/// <summary>
	///     Format of the reasoning. Supported by Groq (and some Grok/Qwen hosts). Mutually exclusive with <see cref="IncludeReasoning"/> on Groq.
	/// </summary>
	[JsonProperty("reasoning_format")]
	public ChatReasoningFormats? ReasoningFormat { get; set; }
	
	/// <summary>
	///     When true (default on Groq reasoning models), include reasoning in a dedicated <c>message.reasoning</c> field.
	///     When false, exclude reasoning from the response. Supported by Groq. Mutually exclusive with <see cref="ReasoningFormat"/>.
	/// </summary>
	[JsonProperty("include_reasoning")]
	public bool? IncludeReasoning { get; set; }
	
	/// <summary>
		///		Sets a token limit on reasoning. 0 disables reasoning. Currently supported by Google (natively "thinkingBudget"), Anthropic (natively "budget_tokens"), Cohere (natively "thinking.token_budget"), and MiniMax-M3 (natively "thinking.type").<br/>
		///		Note: Some providers (Google) don't guarantee this limit is honored without under/over-flowing.<br/>
		///		Google: 2.5 pro: 128-32768; 2.5 flash: 0-24576; 2.5 flash lite: 512-24576; dynamic thinking for any model: -1;<br/>
		///		Anthropic: 0 disables; 1024+ manual budget (deprecated on Opus/Sonnet 4.6+); -1 adaptive thinking (recommended on Opus 4.6+, Sonnet 4.6, required on Opus 4.7+)<br/>
		///		Cohere: 0 or <see cref="ChatReasoningEfforts.None"/> disables thinking on Command A Reasoning / Command A+; a positive budget caps thinking tokens (leave at least 1K for the response).<br/>
		///		MiniMax-M3: 0 disables thinking; -1 or any other value enables adaptive thinking. M2.x models always think.
	/// </summary>
	[JsonIgnore]
	public int? ReasoningBudget { get; set; }
	
	/// <summary>
	/// Configuration for a Predicted Output, which can greatly improve response times when large parts of the model response are known ahead of time. This is most common when you are regenerating a file with only minor changes to most of the content.
	/// </summary>
	[JsonProperty("prediction")]
	public ChatRequestPrediction? Prediction { get; set; }
	
	/// <summary>
	///     The seed to use for deterministic requests.
	/// </summary>
	[JsonProperty("seed")]
    public int? Seed { get; set; }
	
	/// <summary>
	///     Specifies the latency tier to use for processing the request. This parameter is relevant for customers subscribed to the OpenAI scale tier service.
	///     For Anthropic, controls Priority Tier usage with values <see cref="ChatRequestServiceTiers.Auto"/> (default) and <see cref="ChatRequestServiceTiers.StandardOnly"/>.
	///     For Google Gemini, use <see cref="ChatRequestServiceTiers.Flex"/> (50% cost discount, best-effort latency) or <see cref="ChatRequestServiceTiers.Priority"/> (lower latency, premium pricing).
	///     For MiniMax, <see cref="ChatRequestServiceTiers.Priority"/> maps to MiniMax <c>priority</c> admission (1.5x price).
	///     For xAI, <see cref="ChatRequestServiceTiers.Priority"/> requests higher scheduling priority on Chat Completions and Responses.
	/// </summary>
	[JsonProperty("service_tier")]
	public ChatRequestServiceTiers? ServiceTier { get; set; }

	/// <summary>
	///     Controls the inference speed tier. Currently supported by Anthropic on Claude Opus 4.6, Opus 4.7, and NextOpus (Opus 4.8).
	///     Set to <see cref="ChatRequestSpeeds.Fast"/> for up to 2.5x faster output token generation at premium pricing.
	///     Requires the <c>fast-mode-2026-02-01</c> beta header (added automatically).
	/// </summary>
	[JsonIgnore]
	public ChatRequestSpeeds? Speed { get; set; }

	/// <summary>
	///     The response format to use. If <see cref="ChatRequestResponseFormats.Json" />, either system or user message in the
	///     conversation must contain "JSON".
	/// </summary>
	[JsonProperty("response_format")]
    public ChatRequestResponseFormats? ResponseFormat { get; set; }

	/// <summary>
	///     Specifies the response should be streamed. When using abstractions, such as <see cref="Conversation"/>, this is set automatically by the library.
	/// </summary>
	[JsonProperty("stream")]
    public bool? Stream { get; set; }

	[JsonIgnore]
	internal bool StreamResolved => Stream ?? false;
	
	/// <summary>
	/// Whether to return log probabilities of the output tokens or not. If true, returns the log probabilities of each output token returned in the content of message.
	/// </summary>
	[JsonProperty("logprobs")]
	public bool? Logprobs { get; set; }
	
	/// <summary>
	/// Used by OpenAI to cache responses for similar requests to optimize your cache hit rates. Replaces the user field.
	/// On xAI, this is sent as <c>prompt_cache_key</c> on the Responses API and as the <c>x-grok-conv-id</c> header on Chat Completions so a conversation stays on the same cache-warm server.
	/// </summary>
	[JsonProperty("prompt_cache_key")]
	public string? PromptCacheKey { get; set; }
	
	/// <summary>
	/// The retention policy for the prompt cache. Set to 24h to enable extended prompt caching, which keeps cached prefixes active for longer, up to a maximum of 24 hours. Supported by GPT-5.1 and newer models.
	/// </summary>
	[JsonProperty("prompt_cache_retention")]
	public PromptCacheRetention? PromptCacheRetention { get; set; }

	/// <summary>
	/// Enables automatic prompt caching for Anthropic models. When set, the system automatically applies a cache breakpoint to the last cacheable block and moves it forward as conversations grow.
	/// This maps to the top-level <c>cache_control</c> field in the Anthropic API. No effect on other providers.
	/// </summary>
	[JsonIgnore]
	public ChatRequestCacheSettings? AutoCache { get; set; }
	
	/// <summary>
	/// A stable identifier used to help detect users of your application that may be violating OpenAI's usage policies. The IDs should be a string that uniquely identifies each user. We recommend hashing their username or email address, in order to avoid sending us any identifying information. 
	/// </summary>
	[JsonProperty("safety_identifier")]
	public string? SafetyIdentifier { get; set; }
	
	/// <summary>
	/// An integer between 0 and 20 specifying the number of most likely tokens to return at each token position, each with an associated log probability. logprobs must be set to true if this parameter is used.
	/// </summary>
	[JsonProperty("top_logprobs")]
	public int? TopLogprobs { get; set; }
	
	/// <summary>
	/// Constrains the verbosity of the model's response. Only supported by GPT-5. Lower values will result in more concise responses, while higher values will result in more verbose responses. Currently supported values are low, medium, and high.
	/// </summary>
	[JsonProperty("verbosity")]
	public ChatRequestVerbosities? Verbosity { get; set; }
	
	/// <summary>
	/// This tool searches the web for relevant results to use in a response. Learn more about the web search tool.
	/// </summary>
	[JsonProperty("web_search_options")]
	public ChatRequestWebSearchOptions? WebSearchOptions { get; set; }
	
	/// <summary>
	///     The stream configuration.<br/>
	///		Note: by default Tornado includes usage for all providers.
	/// </summary>
	[JsonIgnore]
	public ChatStreamOptions? StreamOptions
	{
		get => StreamOptionsInternal;
		set
		{
			StreamOptionsInternal = value;
			StreamOptionsInternalSerialized = StreamOptionsInternal;
		}
	}

	[JsonIgnore]
	internal ChatStreamOptions? StreamOptionsInternal { get; set; }
	
	/// <summary>
	/// If enabled, any tools backed by delegates will be invoked automatically.
	/// </summary>
	[JsonIgnore]
	public bool InvokeClrToolsAutomatically { get; set; } = true;
	
	[JsonProperty("stream_options")]
	internal object? StreamOptionsInternalSerialized { get; set; }
	
	/// <summary>
	///     This is only used for serializing the request into JSON, do not use it directly.
	/// </summary>
	[JsonProperty("stop")]
    internal object? CompiledStop
    {
        get
        {
            return MultipleStopSequences?.Length switch
            {
                1 => StopSequence,
                > 0 => MultipleStopSequences,
                _ => null
            };
        }
    }
	
	/// <summary>
	///     One or more sequences where the API will stop generating further tokens. The returned text will not contain the
	///     stop sequence.
	/// </summary>
	[JsonIgnore]
    public string[]? MultipleStopSequences { get; set; }

	/// <summary>
	///     The stop sequence where the API will stop generating further tokens. The returned text will not contain the stop
	///     sequence.  For convenience, if you are only requesting a single stop sequence, set it here
	/// </summary>
	[JsonIgnore]
    public string? StopSequence
    {
        get => MultipleStopSequences?.FirstOrDefault() ?? null;
        set
        {
            if (value != null)
                MultipleStopSequences = [value];
        }
    }

	/// <summary>
	///     How many tokens to complete to. Can return fewer if a stop sequence is hit.
	///		Note Anthropic: Streaming is required when max_tokens is greater than 21,333 for Claude 3.7+ models.
	/// </summary>
	[JsonProperty("max_tokens")]
    public int? MaxTokens { get; set; }

	/// <summary>
	///		Strategy for serializing <see cref="MaxTokens"/>.
	/// </summary>
	[JsonIgnore] 
	public ChatRequestMaxTokensSerializers MaxTokensSerializer { get; set; } = ChatRequestMaxTokensSerializers.Auto;

	/// <summary>
	///     The scale of the penalty for how often a token is used.  Should generally be between 0 and 1, although negative
	///     numbers are allowed to encourage token reuse.  Defaults to 0.
	/// </summary>
	[JsonProperty("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }

	/// <summary>
	///     The scale of the penalty applied if a token is already present at all.  Should generally be between 0 and 1,
	///     although negative numbers are allowed to encourage token reuse.  Defaults to 0.
	/// </summary>
	[JsonProperty("presence_penalty")]
    public double? PresencePenalty { get; set; }

	/// <summary>
	///     Modify the likelihood of specified tokens appearing in the completion.
	///     Accepts a json object that maps tokens(specified by their token ID in the tokenizer) to an associated bias value
	///     from -100 to 100.
	///     Mathematically, the bias is added to the logits generated by the model prior to sampling.
	///     The exact effect will vary per model, but values between -1 and 1 should decrease or increase likelihood of
	///     selection; values like -100 or 100 should result in a ban or exclusive selection of the relevant token.
	/// </summary>
	[JsonProperty("logit_bias")]
    public IReadOnlyDictionary<string, float>? LogitBias { get; set; }

	/// <summary>
	///     A unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse.
	/// </summary>
	[JsonProperty("user")]
    public string? User { get; set; }

	/// <summary>
	///     A list of tools the model may generate JSON inputs for.
	/// </summary>
	[JsonProperty("tools")]
    public List<Tool>? Tools { get; set; }

	/// <summary>
	///     Parallel function calling can be disabled / enabled for vendors supporting the feature.
	///		Supported by OpenAI and xAI. When set to false, the model will perform at most one tool call per response.
	/// </summary>
	[JsonProperty("parallel_tool_calls")]
	public bool? ParallelToolCalls { get; set; }

	/// <summary>
	///     Represents an optional field when sending tools calling prompt.
	///     This field determines which function to call.
	/// </summary>
	/// <remarks>
	///     If this field is not specified, the default behavior ("auto") allows the model to automatically decide whether to
	///     call tools or not.
	///     Specify the name of the function to call in the "Name" attribute of the FunctionCall object.
	///     If you do not want the model to call any function, pass "None" for the "Name" attribute.
	/// </remarks>
	[JsonProperty("tool_choice")]
    [JsonConverter(typeof(OutboundToolChoice.OutboundToolChoiceConverter))]
    public OutboundToolChoice? ToolChoice { get; set; }

	/// <summary>
	///     If set the functions part of the outbound request encoded as JSON are stored here.
	///     This can be used a cheap heuristic for counting tokens used when streaming.
	///     Note that OpenAI silently transforms the provided JSON-schema into TypeScript and hence the real usage will be
	///     somewhat lower.
	/// </summary>
	[JsonIgnore]
    public Ref<string>? OutboundFunctionsContent { get; internal set; }

	/// <summary>
	///     This can be any API provider specific data. Currently used in KoboldCpp.
	/// </summary>
	[JsonProperty("adapter")]
    public Dictionary<string, object?>? Adapter { get; set; }
	
	/// <summary>
	///		Features supported only by a single/few providers with no shared equivalent.
	/// </summary>
	[JsonIgnore]
	public ChatRequestVendorExtensions? VendorExtensions { get; set; }

	/// <summary>
	///		Trims the leading whitespace and newline characters in the response. Unless you need to work with responses
	///		with leading whitespace it is recommended to keep this switch on. When streaming, some providers/models incorrectly
	///		produce leading whitespace if the text part of the streamed response is preceded with tool blocks.
	/// </summary>
	[JsonIgnore]
	public bool TrimResponseStart { get; set; } = true;

	/// <summary>
	///		Cancellation token to use with the request.
	/// </summary>
	[JsonIgnore]
	public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
	
	[JsonIgnore]
	internal string? UrlOverride { get; set; }
	
	[JsonIgnore]
	internal Conversation? OwnerConversation { get; set; }
	
	/// <summary>
	/// If enabled the requested will be routed via <see cref="ResponsesEndpoint"/>. If null, the request will be routed this way, if the given model supports only the responses endpoint, or a compatible model is used and <see cref="ResponseRequestParameters"/> is not null.
	/// </summary>
	[JsonIgnore]
	public bool? UseResponseEndpoint { get; set; }
	
	[JsonIgnore]
	internal ChatRequest? TransformedRequest { get; set; }
	
	internal void OverrideUrl(string url)
	{
		UrlOverride = url;
	}
	
	private static readonly PropertyRenameAndIgnoreSerializerContractResolver MaxTokensRenamer = new PropertyRenameAndIgnoreSerializerContractResolver();
	private static readonly JsonSerializerSettings MaxTokensRenamerSettings = new JsonSerializerSettings
	{
		ContractResolver = MaxTokensRenamer,
		NullValueHandling = NullValueHandling.Ignore
	};
	
	static ChatRequest()
	{
		MaxTokensRenamer.RenameProperty(typeof(ChatRequest), "max_tokens", "max_completion_tokens");	
	}

	private static JsonSerializerSettings GetSerializer(JsonSerializerSettings def, JsonSerializerSettings? input)
	{
		if (input is null)
		{
			return def;
		}

		JsonSerializerSettings newSettings = def.DeepCopy();
		newSettings.Formatting = input.Formatting;
		return newSettings;
	}

	private static string PreparePayload(object sourceObject, ChatRequest context, IEndpointProvider provider, CapabilityEndpoints endpoint, JsonSerializerSettings? settings)
	{
		if (context.OnSerialize is null && provider.RequestSerializer is null)
		{
			return JsonConvert.SerializeObject(sourceObject, settings ?? EndpointBase.NullSettings);
		}
		
		JsonSerializer serializer = JsonSerializer.CreateDefault(settings);
		JObject jsonPayload = JObject.FromObject(sourceObject, serializer);
		context.OnSerialize?.Invoke(jsonPayload, context);
		provider.RequestSerializer?.Invoke(jsonPayload, new RequestSerializerContext(sourceObject, provider, RequestActionTypes.ChatCompletionCreate));
		return jsonPayload.ToString(settings?.Formatting ?? Formatting.None);
	}
	
	private static string PreparePayload(JObject sourceObject, ChatRequest context, IEndpointProvider provider, CapabilityEndpoints endpoint, JsonSerializerSettings? settings)
	{
		context.OnSerialize?.Invoke(sourceObject, context);
		provider.RequestSerializer?.Invoke(sourceObject, new RequestSerializerContext(sourceObject, provider, RequestActionTypes.ChatCompletionCreate));
		return sourceObject.ToString(settings?.Formatting ?? Formatting.None);
	}

	private static void ApplyMiniMaxChatExtensions(JObject payload, ChatRequest request)
	{
		ChatRequestVendorMiniMaxExtensions? ext = request.VendorExtensions?.MiniMax;
		
		ChatRequestVendorMiniMaxThinkingTypes? thinkingType = ext?.Thinking?.Type;
		if (thinkingType is null)
		{
			if (request.ReasoningBudget is 0 || request.ReasoningEffort is ChatReasoningEfforts.None)
			{
				thinkingType = ChatRequestVendorMiniMaxThinkingTypes.Disabled;
			}
			else if (request.ReasoningBudget is -1)
			{
				thinkingType = ChatRequestVendorMiniMaxThinkingTypes.Adaptive;
			}
		}

		if (thinkingType is not null)
		{
			payload["thinking"] = new JObject
			{
				["type"] = thinkingType is ChatRequestVendorMiniMaxThinkingTypes.Disabled ? "disabled" : "adaptive"
			};
		}

		// Separate thinking into reasoning_content so multi-turn tool calls keep the reasoning chain.
		payload["reasoning_split"] = ext?.ReasoningSplit ?? true;

		if (ext?.ServiceTier is ChatRequestVendorMiniMaxServiceTier.Priority || request.ServiceTier is ChatRequestServiceTiers.Priority)
		{
			payload["service_tier"] = "priority";
		}
		else if (ext?.ServiceTier is ChatRequestVendorMiniMaxServiceTier.Standard)
		{
			payload["service_tier"] = "standard";
		}
	}

	private static readonly Dictionary<LLmProviders, Func<ChatRequest, IEndpointProvider, CapabilityEndpoints, JsonSerializerSettings?, string>> serializeMap = new Dictionary<LLmProviders, Func<ChatRequest, IEndpointProvider, CapabilityEndpoints, JsonSerializerSettings?, string>>((int)LLmProviders.Length)
	{
		{
			LLmProviders.OpenAi, (x, y, z, a) =>
			{
				if (z is CapabilityEndpoints.Responses)
				{
					ResponseRequest response = ResponseHelpers.ToResponseRequest(y, x.ResponseRequestParameters, x);
					response.ApplyModelParameterPolicies(y);
					return PreparePayload(response, x, y, z, GetSerializer(MaxTokensRenamerSettings, a));
				}

				if (x.Model is not null)
				{
					if (ChatModelOpenAi.TempIncompatibleModels.Contains(x.Model))
					{
						x.Temperature = null;
					}
					
					// GPT-5.2, GPT-5.4, GPT-5.5, and GPT-5.6 parameter compatibility
					bool? hasNonNoneReasoning = x.ReasoningEffort is { } effort ? effort != ChatReasoningEfforts.None : null;
					if (ChatModelOpenAi.ShouldClearSamplingParams(x.Model, hasNonNoneReasoning))
					{
						x.Temperature = null;
						x.TopP = null;
						x.Logprobs = null;
						x.TopLogprobs = null;
					}

					PromptCacheRetention? retention = x.PromptCacheRetention;
					ChatModelOpenAi.ApplyPromptCacheRetentionPolicy(x.Model, ref retention);
					x.PromptCacheRetention = retention;

					if ((x.Modalities?.Contains(ChatModelModalities.Audio) ?? false) && ChatModelOpenAi.AudioModelsAllSet.Contains(x.Model))
					{
						x.Audio ??= new ChatRequestAudio();
						x.Audio.Format ??= ChatRequestAudioFormats.Wav;
						x.Audio.Voice ??= ChatAudioRequestKnownVoices.Ash;
					}
				}

				JsonSerializerSettings settings = GetSerializer(MaxTokensRenamerSettings, a);
				return PreparePayload(x, x, y, z, settings);
			}
		},
		{ LLmProviders.DeepSeek, (x, y, z, a) => PreparePayload(x, x, y, z, GetSerializer(EndpointBase.NullSettings, a)) },
		{ LLmProviders.Anthropic, (x, y, z, a) => PreparePayload(new VendorAnthropicChatRequest(x, y), x, y, z, GetSerializer(EndpointBase.NullSettings, a)) },
		{
			LLmProviders.Cohere, (x, y, z, a) =>
			{
				VendorCohereChatRequest request = new VendorCohereChatRequest(x, y);
				JsonSerializerSettings serializer = GetSerializer(EndpointBase.NullSettings, a);
				return PreparePayload(request.Serialize(serializer), x, y, z, serializer);
			}
		},
		{ LLmProviders.Google, (x, y, z, a) => PreparePayload(new VendorGoogleChatRequest(x, y), x, y, z, GetSerializer(EndpointBase.NullSettings, a)) },
		{
			LLmProviders.Mistral, (x, y, z, a) =>
			{
				VendorMistralChatRequest request = new VendorMistralChatRequest(x, y);
				JsonSerializerSettings serializer = GetSerializer(EndpointBase.NullSettings, a);
				return PreparePayload(request.Serialize(serializer), x, y, z, serializer);
			}
		},
		{
			LLmProviders.Groq, (x, y, z, a) =>
			{
				// fields unsupported by groq
				x.LogitBias = null;
				VendorGroqChatRequest request = new VendorGroqChatRequest(x, y);
				JsonSerializerSettings serializer = GetSerializer(EndpointBase.NullSettings, a);
				return PreparePayload(request.Serialize(serializer), x, y, z, serializer);
			}
		},
		{
			LLmProviders.XAi, (x, y, z, a) =>
			{
				VendorXAiChatRequest request = new VendorXAiChatRequest(x, y);
				JsonSerializerSettings serializer = GetSerializer(EndpointBase.NullSettings, a);
				return PreparePayload(request.Serialize(serializer), x, y, z, serializer);
			}
		},
		{
			LLmProviders.Perplexity, (x, y, z, a) =>
			{
				JsonSerializerSettings serializer = GetSerializer(EndpointBase.NullSettings, a);

				if (z is CapabilityEndpoints.Responses)
				{
					ResponseRequest responseRequest = ResponseHelpers.ToResponseRequest(y, x.ResponseRequestParameters, x);
					return PreparePayload(responseRequest, x, y, z, serializer);
				}

				VendorPerplexityChatRequest request = new VendorPerplexityChatRequest(x, y);
				return PreparePayload(request.Serialize(serializer), x, y, z, serializer);
			}
		},
		{
			LLmProviders.Zai, (x, y, z, a) =>
			{
				VendorZaiChatRequest request = new VendorZaiChatRequest(x, y);
				JsonSerializerSettings serializer = GetSerializer(EndpointBase.NullSettings, a);
				return PreparePayload(request.Serialize(serializer), x, y, z, serializer);
			}
		},
		{
			LLmProviders.DeepInfra, (x, y, z, a) =>
			{
				return PreparePayload(x, x, y, z, GetSerializer(EndpointBase.NullSettings, a));
			}
		},
		{
			LLmProviders.Blablador, (x, y, z, a) =>
			{
				return PreparePayload(x, x, y, z, GetSerializer(EndpointBase.NullSettings, a));
			}
		},
		{
			LLmProviders.OpenRouter, (x, y, z, a) =>
			{
				return PreparePayload(x, x, y, z, GetSerializer(EndpointBase.NullSettings, a));
			}
		},
		{
			LLmProviders.Requesty, (x, y, z, a) =>
			{
				return PreparePayload(x, x, y, z, GetSerializer(EndpointBase.NullSettings, a));
			}
		},
		{
			LLmProviders.Upstage, (x, y, z, a) =>
			{
				if (x.ReasoningEffort is not null && x.ReasoningEffort != ChatReasoningEfforts.None)
				{
					x.ReasoningEffort = x.ReasoningEffort switch
					{
						ChatReasoningEfforts.Low => ChatReasoningEfforts.Low,
						ChatReasoningEfforts.High => ChatReasoningEfforts.High,
						ChatReasoningEfforts.Minimal => ChatReasoningEfforts.Low,
						ChatReasoningEfforts.Medium => ChatReasoningEfforts.High,
						ChatReasoningEfforts.XHigh => ChatReasoningEfforts.High,
						_ => ChatReasoningEfforts.Low
					};
				}
				
				return PreparePayload(x, x, y, z, GetSerializer(EndpointBase.NullSettings, a));
			}
		},
		{
			LLmProviders.MoonshotAi, (x, y, z, a) =>
			{
				VendorMoonshotAiChatRequest request = new VendorMoonshotAiChatRequest(x, y);
				JsonSerializerSettings serializer = GetSerializer(EndpointBase.NullSettings, a);
				return PreparePayload(request.Serialize(serializer), x, y, z, serializer);
			}
		},
		{
			LLmProviders.Alibaba, (x, y, z, a) =>
			{
				VendorAlibabaChatRequest request = new VendorAlibabaChatRequest(x, y);
				JsonSerializerSettings serializer = GetSerializer(EndpointBase.NullSettings, a);
				return PreparePayload(request.Serialize(serializer), x, y, z, serializer);
			}
		},
		{
			LLmProviders.MiniMax, (x, y, z, a) =>
			{
				x.LogitBias = null;
				x.FrequencyPenalty = null;
				x.PresencePenalty = null;
				x.NumChoicesPerMessage = 1;
				
				JsonSerializerSettings serializer = GetSerializer(EndpointBase.NullSettings, a);
				JObject payload = JObject.FromObject(x, JsonSerializer.CreateDefault(serializer));
				ApplyMiniMaxChatExtensions(payload, x);
				return PreparePayload(payload, x, y, z, serializer);
			}
		},
		{
			// Self-hosted / OpenAI-compatible endpoints (Ollama, vLLM, KoboldCpp, ...).
			LLmProviders.Custom, (x, y, z, a) => PreparePayload(x, x, y, z, GetSerializer(EndpointBase.NullSettings, a))
		},
		{
			// LiteLLM AI gateway (OpenAI-compatible proxy for 100+ providers).
			LLmProviders.LiteLlm, (x, y, z, a) => PreparePayload(x, x, y, z, GetSerializer(EndpointBase.NullSettings, a))
		}
	};

	internal CapabilityEndpoints GetCapabilityEndpoint()
	{
		return GetCapabilityEndpoint(this);
	}
	
	internal static CapabilityEndpoints GetCapabilityEndpoint(ChatRequest req)
	{
		// if we are explicitly told to use responses, honor the setting
		if (req.UseResponseEndpoint is true)
		{
			return CapabilityEndpoints.Responses;
		}
		
		// if we are explicitly told to not use responses
		if (req.UseResponseEndpoint is false)
		{
			return CapabilityEndpoints.Chat;
		}

		if (req.Model?.Provider is LLmProviders.Perplexity)
		{
			HashSet<ChatModelEndpointCapabilities>? perplexityCapabilities = req.Model.EndpointCapabilities;
			if (perplexityCapabilities?.Contains(ChatModelEndpointCapabilities.Responses) == true &&
			    !perplexityCapabilities.Contains(ChatModelEndpointCapabilities.Chat))
			{
				return CapabilityEndpoints.Responses;
			}

			if (req.Model.Name is not null && ChatModelPerplexity.AgentModelNames.Contains(req.Model.Name))
			{
				return CapabilityEndpoints.Responses;
			}

			return req.ResponseRequestParameters is not null ? CapabilityEndpoints.Responses : CapabilityEndpoints.Chat;
		}

		if (req.Model?.Provider is LLmProviders.Groq)
		{
			HashSet<ChatModelEndpointCapabilities>? groqCapabilities = req.Model.EndpointCapabilities;
			if (groqCapabilities?.Contains(ChatModelEndpointCapabilities.Responses) == true &&
			    !groqCapabilities.Contains(ChatModelEndpointCapabilities.Chat))
			{
				return CapabilityEndpoints.Responses;
			}

			return req.ResponseRequestParameters is not null || req.UseResponseEndpoint is true
				? CapabilityEndpoints.Responses
				: CapabilityEndpoints.Chat;
		}
		
		// if the model is from an unsupported provider
		if (req.Model?.Provider is not LLmProviders.OpenAi)
		{
			return CapabilityEndpoints.Chat;
		}
		
		// OpenAI Ultrafast uses the Responses API.
		if (req.ServiceTier is ChatRequestServiceTiers.Ultrafast)
		{
			return CapabilityEndpoints.Responses;
		}

		// if we are missing metadata, use responses if we have parameters for it
		if (req.Model.EndpointCapabilities is null || req.Model.EndpointCapabilities.Count is 0)
		{
			return req.ResponseRequestParameters is not null ? CapabilityEndpoints.Responses : CapabilityEndpoints.Chat;
		}

		// automatically upcast in the case of /chat endpoint not being supported by the model
		HashSet<ChatModelEndpointCapabilities>? capabilities = req.Model.EndpointCapabilities;
		if (capabilities?.Contains(ChatModelEndpointCapabilities.Responses) == true && !capabilities.Contains(ChatModelEndpointCapabilities.Chat))
		{
			return CapabilityEndpoints.Responses;
		}

		// GPT-6 tool calling requires Responses, except Sol and Luna with explicit none effort.
		if (req.Tools is { Count: > 0 } &&
		    capabilities?.Contains(ChatModelEndpointCapabilities.Responses) == true &&
		    (ChatModelOpenAi.ToolsRequireResponsesModelsAllSet.Contains(req.Model) ||
		     (req.ReasoningEffort is not ChatReasoningEfforts.None &&
		      ChatModelOpenAi.ToolsWithReasoningRequireResponsesModelsAllSet.Contains(req.Model))))
		{
			return CapabilityEndpoints.Responses;
		}

		return req.ResponseRequestParameters is not null ? CapabilityEndpoints.Responses : CapabilityEndpoints.Chat;
	}

	/// <summary>
	/// Serializes the request with debugging options
	/// </summary>
	/// <param name="provider"></param>
	/// <param name="options"></param>
	/// <returns></returns>
	public TornadoRequestContent Serialize(IEndpointProvider provider, ChatRequestSerializeOptions? options)
	{
		CapabilityEndpoints capabilityEndpoint = GetCapabilityEndpoint(this);
		TornadoRequestContent serialized = Serialize(provider, capabilityEndpoint, options?.Pretty ?? false);
		
		string finalUrl = EndpointBase.BuildRequestUrl(serialized.Url, provider, capabilityEndpoint, Model);
		serialized.Url = finalUrl;

		if (options?.IncludeHeaders ?? false)
		{
			using HttpRequestMessage msg = provider.OutboundMessage(finalUrl, HttpMethod.Post, serialized.Body, options.Stream, this);
			serialized.Headers = msg.Headers.ConvertHeaders();
		}

		return serialized;
	}

	/// <summary>
	/// Serializes the request.
	/// </summary>
	public TornadoRequestContent Serialize(IEndpointProvider provider, RequestSerializeOptions options)
	{
		return Serialize(provider, new ChatRequestSerializeOptions
		{
			Pretty = options.Pretty
		});
	}

	internal void Preserialize(IEndpointProvider provider)
	{
		if (OwnerConversation is not null)
		{
			IReadOnlyList<ChatMessage> conversationMessages = OwnerConversation.Messages;
			
			if (conversationMessages is List<ChatMessage> list)
			{
				Messages = list;
			}
			else
			{
				Messages = new List<ChatMessage>(conversationMessages);
			}
		}
		
		if (Messages is not null)
		{
			foreach (ChatMessage msg in Messages)
			{
				msg.Request = this;
			}	
		}
		
		if (Tools is not null)
		{
			int i = 0;
			
			foreach (Tool tool in Tools)
			{
				tool.Serialize(provider, i);
				i++;
			}	
		}

		if (ResponseFormat is { Type: ChatRequestResponseFormatTypes.StructuredJson, Schema: not null } && (ResponseFormat.Schema.Delegate is not null || ResponseFormat.Schema.SchemaParams is not null))
		{
			ResponseFormat.Serialize(provider);
		}
	}

	internal static TornadoRequestContentWithProvider Serialize(TornadoApi api, ChatRequest request, bool pretty = false)
	{
		ChatModel? modelToUse = request.Model;
		IEndpointProvider provider = api.GetProvider(request.Model ?? ChatModel.OpenAi.Gpt35.Turbo);
        
		// the resolved model is potentially incorrect - optimistically resolved models pick the first provider by name, but multiple providers
		// offer the same models, we need to correlate with the available API keys
		if ((modelToUse?.OptimisticallyResolved ?? false) && provider.Api is not null)
		{
			if (provider.Api.Authentications.Count > 0 && !provider.Api.Authentications.TryGetValue(modelToUse.Provider, out _))
			{
				foreach (KeyValuePair<LLmProviders, ProviderAuthentication> auth in provider.Api.Authentications)
				{
					ChatModel? resolved = ChatModel.ResolveModel(auth.Key, modelToUse.Name);

					if (resolved is not null)
					{
						modelToUse = resolved;
						provider = provider.Api?.GetProvider(modelToUse.Provider) ?? provider;
					}
				}
			}
		}

		return new TornadoRequestContentWithProvider(provider, request.Serialize(provider, GetCapabilityEndpoint(request), pretty));
	}

	private TornadoRequestContent Serialize(IEndpointProvider provider, CapabilityEndpoints capabilityEndpoint, bool pretty)
	{
		Preserialize(provider);

		ChatRequest outboundCopy = new ChatRequest(this);

		switch (StreamResolved)
		{
			case false when StreamOptions is not null:
			{
				outboundCopy.StreamOptions = null;
				break;
			}
			case true when StreamOptions is null:
			{
				outboundCopy.StreamOptions = ChatStreamOptions.KnownOptionsIncludeUsage;
				break;
			}
		}
		
		if (provider.Provider is not LLmProviders.Groq)
		{
			outboundCopy.ReasoningFormat = null;
			outboundCopy.IncludeReasoning = null;
		}
		
		TornadoRequestContent serialized = serializeMap.TryGetValue(provider.Provider, out Func<ChatRequest, IEndpointProvider, CapabilityEndpoints, JsonSerializerSettings?, string>? serializerFn) ? new TornadoRequestContent(serializerFn.Invoke(outboundCopy, provider, capabilityEndpoint, pretty ? new JsonSerializerSettings
		{
			Formatting = Formatting.Indented
		} : null), outboundCopy.Model, outboundCopy.UrlOverride, provider, capabilityEndpoint) : new TornadoRequestContent(string.Empty, outboundCopy.Model, outboundCopy.UrlOverride, provider, CapabilityEndpoints.Chat);
		
		TransformedRequest = outboundCopy;
		return serialized;
	}

	///  <summary>
	/// 		Serializes the chat request into the request body, based on the conventions used by the LLM provider.
	///  </summary>
	///  <param name="provider"></param>
	///  <param name="capabilityEndpoint"></param>
	///  <returns></returns>
	public TornadoRequestContent Serialize(IEndpointProvider provider)
	{
		return Serialize(provider, GetCapabilityEndpoint(this), false);
	}

	/// <summary>
	/// Returns additional headers required by this request for the specified provider.
	/// </summary>
	IEnumerable<string> IHeaderProvider.GetHeaders(LLmProviders provider)
	{
		if (provider is LLmProviders.Anthropic)
		{
			if (VendorExtensions?.Anthropic is not null)
			{
				if (VendorExtensions.Anthropic.McpServers is not null)
				{
					yield return AnthropicMcpBetaHeaders.McpClient;
				}
			
				if (VendorExtensions.Anthropic.Container is not null)
				{
					yield return "skills-2025-10-02";
				}
			}
		}
	}
	
	internal class ModalitiesJsonConverter : JsonConverter<List<ChatModelModalities>>
	{
		public override void WriteJson(JsonWriter writer, List<ChatModelModalities>? value, JsonSerializer serializer)
		{
			if (value is null)
			{
				return;
			}

			if (value.Count is 0)
			{
				return;
			}
			
			writer.WriteStartArray();

			foreach (ChatModelModalities x in value)
			{
				switch (x)
				{
					case ChatModelModalities.Audio:
						writer.WriteValue("audio");
						break;
					case ChatModelModalities.Text:
						writer.WriteValue("text");
						break;
				}
			}

			writer.WriteEndArray();
		}

		public override List<ChatModelModalities>? ReadJson(JsonReader reader, Type objectType, List<ChatModelModalities>? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			return existingValue;
		}
	}
    
    internal class ChatMessageRequestMessagesJsonConverter : JsonConverter<IList<ChatMessage>?>
    {
        /// <summary>
        /// Providers that support the reasoning_content field on assistant messages.
        /// </summary>
        private static readonly HashSet<LLmProviders> ReasoningContentProviders =
        [
            LLmProviders.Zai,
            LLmProviders.DeepSeek,
            LLmProviders.XAi,
            LLmProviders.OpenRouter,
            LLmProviders.Requesty,
            LLmProviders.DeepInfra,
            LLmProviders.LiteLlm,
            LLmProviders.Alibaba,
            LLmProviders.MoonshotAi,
            LLmProviders.MiniMax
        ];
        
        public override void WriteJson(JsonWriter writer, IList<ChatMessage>? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            ChatRequest? request = null;
            bool isExpired = false;

            if (value.Count > 0)
            {
	            request = value[0].Request;
            }

            writer.WriteStartArray();

            foreach (ChatMessage msg in value)
            {
                writer.WriteStartObject();
                
                if (msg.Role is not null)
                {
	                writer.WritePropertyName("role");
	                
	                if (msg.Role is ChatMessageRoles.System)
	                {
		                if (request?.Model is not null && ChatModelOpenAiGpt4.ReasoningModels.Contains(request.Model))
		                {
			                writer.WriteValue("developer");
		                }
		                else
		                {
			                writer.WriteValue("system");
		                }
	                }
	                else
	                {
		                string roleString = msg.Role.Value switch
		                {
			                ChatMessageRoles.User => "user",
			                ChatMessageRoles.Assistant => "assistant",
			                ChatMessageRoles.Tool => "tool",
			                ChatMessageRoles.System => "system",
			                _ => "user"
		                };
		                writer.WriteValue(roleString);
	                }   
                }

                if (msg.Role is not null)
                {
	                switch (msg.Role)
	                {
		                case ChatMessageRoles.Tool:
		                {
			                writer.WritePropertyName("tool_call_id");
			                writer.WriteValue(msg.ToolCallId);
			                break;
		                }
		                case ChatMessageRoles.Assistant:
		                {
			                if (msg.Prefix is not null)
			                {
				                writer.WritePropertyName("prefix");
				                writer.WriteValue(msg.Prefix.Value);
			                }

			                if (msg.Partial is true && request?.Model?.Provider is LLmProviders.MoonshotAi)
			                {
				                writer.WritePropertyName("partial");
				                writer.WriteValue(true);
			                }
			                
			                if (msg.ToolCalls is not null)
			                {
				                writer.WritePropertyName("tool_calls");

				                writer.WriteStartArray();

				                foreach (ToolCall call in msg.ToolCalls)
				                {
					                writer.WriteStartObject();

					                writer.WritePropertyName("id");
					                writer.WriteValue(call.Id);

					                writer.WritePropertyName("type");
					                writer.WriteValue(call.Type);

					                if (call.FunctionCall is not null)
					                {
						                writer.WritePropertyName("function");
						                writer.WriteStartObject();
					       
						                writer.WritePropertyName("name");
						                writer.WriteValue(call.FunctionCall.Name);   

						                writer.WritePropertyName("arguments");
						                writer.WriteValue(call.FunctionCall.Arguments);   
						                
						                writer.WriteEndObject();
					                }
					                else if (call.CustomCall is not null)
					                {
						                writer.WritePropertyName("custom");
						                writer.WriteStartObject();
					       
						                writer.WritePropertyName("name");
						                writer.WriteValue(call.CustomCall.Name);   

						                writer.WritePropertyName("input");
						                writer.WriteValue(call.CustomCall.Input);   
						                
						                writer.WriteEndObject();
					                }

					                writer.WriteEndObject();
				                }

				                writer.WriteEndArray();
			                }

			                if (msg.Audio is not null)
			                {
				                if (request?.Audio?.CompressionStrategy is ChatAudioCompressionStrategies.Native or ChatAudioCompressionStrategies.PreferNative)
				                {
					                if (request.Audio.CompressionStrategy is ChatAudioCompressionStrategies.PreferNative)
					                {
						                isExpired = DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= msg.Audio.ExpiresAt;
					                }

					                if (!isExpired)
					                {
						                writer.WritePropertyName("audio");
						                writer.WriteStartObject();
				                
						                writer.WritePropertyName("id");
						                writer.WriteValue(msg.Audio.Id);
								
						                writer.WriteEndObject();    
					                }
				                }
			                }

			                break;
		                }
	                }
                }

                if (msg.Role is ChatMessageRoles.Assistant && !string.IsNullOrWhiteSpace(msg.ReasoningContent) && request?.Model is not null && ReasoningContentProviders.Contains(request.Model.Provider))
                {
	                writer.WritePropertyName("reasoning_content");
	                writer.WriteValue(msg.ReasoningContent);
	                
	                // Write encrypted_content for xAI (harmonized with Anthropic's signature / Google's thoughtSignature)
	                if (request.Model.Provider is LLmProviders.XAi)
	                {
		                string? encryptedContent = msg.EncryptedContent ?? msg.Parts?.FirstOrDefault(p => p.Type == ChatMessageTypes.Reasoning)?.Reasoning?.Signature;
		                
		                if (!string.IsNullOrWhiteSpace(encryptedContent))
		                {
			                writer.WritePropertyName("encrypted_content");
			                writer.WriteValue(encryptedContent);
		                }
	                }
                }

                if (!string.IsNullOrWhiteSpace(msg.Name))
                {
                    writer.WritePropertyName("name");
                    writer.WriteValue(msg.Name);
                }

                if (msg is { Role: ChatMessageRoles.Tool, Content: null })
                {
	                goto closeMsgObj;
                }

                if (msg is { Role: ChatMessageRoles.Assistant, Content: null, ToolCalls: not null })
                {
	                goto closeMsgObj;
                }
                
                writer.WritePropertyName("content");

                if (msg.Parts?.Count > 0)
                {
                    writer.WriteStartArray();

                    foreach (ChatMessagePart part in msg.Parts)
                    {
                        writer.WriteStartObject();

                        writer.WritePropertyName("type");

                        string type = part.Type switch
                        {
	                        ChatMessageTypes.Text => "text",
	                        ChatMessageTypes.Image => "image_url",
	                        ChatMessageTypes.Audio => part.Audio?.Url is not null ? "audio_url" : "input_audio",
                            ChatMessageTypes.Video => "video_url",
	                        ChatMessageTypes.Document => "file",
	                        ChatMessageTypes.FileLink => "file",
                            _ => "text"
                        };
                        
	                    writer.WriteValue(type);   
	                    
                        switch (part.Type)
                        {
	                        case ChatMessageTypes.Text:
	                        {
		                        writer.WritePropertyName("text");
		                        writer.WriteValue(part.Text);
		                        break;
	                        }
	                        case ChatMessageTypes.Image:
	                        {
		                        writer.WritePropertyName("image_url");
		                        writer.WriteStartObject();

		                        writer.WritePropertyName("url");
		                        writer.WriteValue(part.Image?.Url);

		                        if (part.Image?.Detail is not null)
		                        {
			                        writer.WritePropertyName("detail");
			                        bool miniMax = request?.Model?.Provider is LLmProviders.MiniMax;
			                        writer.WriteValue(part.Image.Detail switch
			                        {
				                        ImageDetail.Auto => miniMax ? "default" : "auto",
				                        ImageDetail.High => "high",
				                        ImageDetail.Low => "low",
				                        _ => miniMax ? "default" : "auto"
			                        });
		                        }

		                        writer.WriteEndObject();
		                        break;
	                        }
	                        case ChatMessageTypes.Audio:
	                        {
								if (part.Audio?.Url is not null)
								{
                                    writer.WritePropertyName("audio_url");
                                    writer.WriteStartObject();

                                    writer.WritePropertyName("url");
                                    writer.WriteValue(part.Audio.Url);

                                    writer.WriteEndObject();
                                    break;
                                }

								writer.WritePropertyName("input_audio");
								writer.WriteStartObject();

								writer.WritePropertyName("data");
								writer.WriteValue(part.Audio?.Data);

								writer.WritePropertyName("format");

								if (part.Audio is not null)
								{
									switch (part.Audio.Format)
									{
										case ChatAudioFormats.Wav:
										{
											writer.WriteValue("wav");
											break;
										}
										case ChatAudioFormats.Mp3:
										{
											writer.WriteValue("mp3");
											break;
										}
									}
								}
								else
								{
									writer.WriteValue(string.Empty);
								}

								writer.WriteEndObject();
								break;
	                        }
                            case ChatMessageTypes.Video:
                            {
                                writer.WritePropertyName("video_url");
                                writer.WriteStartObject();

                                writer.WritePropertyName("url");
                                writer.WriteValue(part.Video?.UrlOverride ?? part.Video?.Url?.ToString());

                                if (part.Video?.Detail is not null)
                                {
	                                writer.WritePropertyName("detail");
	                                bool miniMax = request?.Model?.Provider is LLmProviders.MiniMax;
	                                writer.WriteValue(part.Video.Detail switch
	                                {
		                                ImageDetail.Auto => miniMax ? "default" : "auto",
		                                ImageDetail.High => "high",
		                                ImageDetail.Low => "low",
		                                _ => miniMax ? "default" : "auto"
	                                });
                                }

                                if (part.Video?.Fps is not null)
                                {
	                                writer.WritePropertyName("fps");
	                                writer.WriteValue(part.Video.Fps.Value);
                                }

                                if (part.Video?.MaxLongSidePixel is not null)
                                {
	                                writer.WritePropertyName("max_long_side_pixel");
	                                writer.WriteValue(part.Video.MaxLongSidePixel.Value);
                                }

                                writer.WriteEndObject();
                                break;
                            }
	                        case ChatMessageTypes.Document:
	                        {
	                        writer.WritePropertyName("file");
	                        writer.WriteStartObject();

	                        if (part.Document?.Base64 is not null)
	                        {
	                            writer.WritePropertyName("file_data");
	                            writer.WriteValue(part.Document.Base64);
	                        }
	                        else if (part.Document?.Uri is not null)
	                        {
	                            writer.WritePropertyName("file_url");
	                            writer.WriteValue(part.Document.Uri.ToString());

	                            string? fileName = Path.GetFileName(part.Document.Uri.AbsolutePath);
	                            if (!string.IsNullOrWhiteSpace(fileName))
	                            {
	                                writer.WritePropertyName("filename");
	                                writer.WriteValue(fileName);
	                            }
	                        }

	                        writer.WriteEndObject();
	                        break;
	                        }
	                        case ChatMessageTypes.FileLink:
	                        {
		                        writer.WritePropertyName("file");
		                        writer.WriteStartObject();

		                        // writer.WritePropertyName("file_data");
		                        // writer.WriteValue(part.Video?.Url);
		                        
		                        writer.WritePropertyName("file_id");
		                        writer.WriteValue(part.FileLinkData?.File?.Reference ?? part.FileLinkData?.FileUri);

		                        string? fileName = part.FileLinkData?.File?.Name;
		                        if (!string.IsNullOrWhiteSpace(fileName))
		                        {
			                        writer.WritePropertyName("filename");
			                        writer.WriteValue(fileName);   
		                        }
		                        
		                        writer.WriteEndObject();
		                        break;
	                        }
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }
                else
                {
	                if (msg.Audio is not null)
	                {
		                if (request?.Audio?.CompressionStrategy is ChatAudioCompressionStrategies.OutputAsText or ChatAudioCompressionStrategies.PreferNative)
		                {
			                // only write transcription if the audio cache expired
			                if (request.Audio.CompressionStrategy is ChatAudioCompressionStrategies.PreferNative && !isExpired)
			                {
				                msg.Content = msg.Audio.Id;
				                goto writeNativeContent;
			                }
			                
			                writer.WriteValue(msg.Audio.Transcript);
			                goto closeMsgObj;
		                }
	                }
	                
	                writeNativeContent:
	                writer.WriteValue(msg.Content);
                }

                closeMsgObj:
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        public override IList<ChatMessage>? ReadJson(JsonReader reader, Type objectType, IList<ChatMessage>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return existingValue;
        }
    }
}

/// <summary>
/// TTL options for automatic prompt caching. Currently only used by Anthropic.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatRequestCacheTtl
{
    /// <summary>
    /// 5-minute cache (default). Cache is refreshed for free each time the cached content is used.
    /// </summary>
    [EnumMember(Value = "5m")]
    FiveMinutes,
    /// <summary>
    /// 1-hour cache at 2x the base input token price.
    /// </summary>
    [EnumMember(Value = "1h")]
    OneHour
}

/// <summary>
/// Settings for automatic prompt caching. When set on <see cref="ChatRequest.AutoCache"/>, the provider automatically
/// applies a cache breakpoint to the last cacheable block and moves it forward as conversations grow.
/// Currently supported by Anthropic only.
/// </summary>
public class ChatRequestCacheSettings
{
    /// <summary>
    /// Ephemeral cache with default 5-minute TTL.
    /// </summary>
    public static readonly ChatRequestCacheSettings Ephemeral = new ChatRequestCacheSettings();

    /// <summary>
    /// Ephemeral cache with the specified TTL.
    /// </summary>
    public static ChatRequestCacheSettings EphemeralWithTtl(ChatRequestCacheTtl ttl)
    {
        return new ChatRequestCacheSettings { Ttl = ttl };
    }

    /// <summary>
    /// Time to live for the cache entry. Defaults to 5 minutes when null.
    /// </summary>
    [JsonIgnore]
    public ChatRequestCacheTtl? Ttl { get; private set; }

    private ChatRequestCacheSettings()
    {
    }
}
