using System.Collections.Generic;
using System.Threading;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Responses;

/// <summary>
/// Request for responses API.
/// </summary>
public class ResponseRequest
{
    /// <summary>
    ///		Cancellation token to use with the request.
    /// </summary>
    [JsonIgnore]
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
    
    /// <summary>
    /// Whether to run the model response in the background.
    /// </summary>
    [JsonProperty("background")]
    public bool? Background { get; set; }

    /// <summary>
    /// The conversation that this response belongs to. Items from this conversation are prepended
    /// to <c>input</c> for this response request. Input items and output items from this response
    /// are automatically added to this conversation after this response completes.
    /// Can be a plain conversation ID string or an object with an <c>id</c> field.
    /// Cannot be used in conjunction with <see cref="PreviousResponseId"/>.
    /// </summary>
    [JsonProperty("conversation")]
    public IResponseConversation? Conversation { get; set; }

    /// <summary>
    /// Specify additional output data to include in the model response. 
    /// See <see cref="ResponseIncludeFields"/> for supported values.
    /// </summary>
    [JsonProperty("include")]
    public List<ResponseIncludeFields>? Include { get; set; }

    /// <summary>
    /// Text input for text requests.
    /// </summary>
    [JsonIgnore]
    public string? InputString { get; set; }

    /// <summary>
    /// Multi-part, multi-modal requests.
    /// </summary>
    [JsonIgnore]
    public List<ResponseInputItem>? InputItems { get; set; }

    /// <summary>
    /// Text, image, or file inputs to the model, used to generate a response.
    /// Can be either a simple string or a list of input items.
    /// </summary>
    [JsonProperty("input")]
    internal object? Input
    {
        get
        {
            if (InputItems?.Count > 0)
            {
                return InputItems;
            }
            
            return InputString;
        }
    }

    /// <summary>
    /// A system (or developer) message inserted into the model's context. When using along with previous_response_id, the instructions from a previous response will not be carried over to the next response. This makes it simple to swap out system (or developer) messages in new responses.
    /// </summary>
    [JsonProperty("instructions")]
    public string? Instructions { get; set; }
    
    /// <summary>
    /// An upper bound for the number of tokens that can be generated for a response, including visible output tokens and reasoning tokens.
    /// </summary>
    [JsonProperty("max_output_tokens")]
    public int? MaxOutputTokens { get; set; }
    
    /// <summary>
    /// The maximum number of total calls to built-in tools that can be processed in a response. This maximum number applies across all built-in tool calls, not per individual tool. Any further attempts to call a tool by the model will be ignored.
    /// </summary>
    [JsonProperty("max_tool_calls")]
    public int? MaxToolCalls { get; set; }
    
    /// <summary>
    /// Set of 16 key-value pairs that can be attached to an object. This can be useful for storing additional information about the object in a structured format, and querying for objects via API or the dashboard.<br/>
    /// Keys are strings with a maximum length of 64 characters. Values are strings with a maximum length of 512 characters.
    /// </summary>
    [JsonProperty("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
    
    /// <summary>
    /// Model ID used to generate the response, like gpt-4o or o3. OpenAI offers a wide range of models with different capabilities, performance characteristics, and price points. Refer to the model guide to browse and compare available models.
    /// </summary>
    [JsonProperty("model")]
    [JsonConverter(typeof(ChatModelJsonConverter))]
    public ChatModel? Model { get; set; }
    
    /// <summary>
    /// Perplexity Agent API preset (fast, low, medium, high, xhigh, wide-research).
    /// When set, Perplexity uses the preset instead of a raw model id.
    /// </summary>
    [JsonProperty("preset")]
    public string? Preset { get; set; }
    
    /// <summary>
    /// Perplexity Agent API fallback model chain. Tried in order when the primary model is unavailable.
    /// </summary>
    [JsonProperty("models")]
    public List<string>? Models { get; set; }
    
    /// <summary>
    /// Whether to allow the model to run tool calls in parallel. Defaults to true if null.
    /// </summary>
    [JsonProperty("parallel_tool_calls")]
    public bool? ParallelToolCalls { get; set; }
    
    /// <summary>
    /// The unique ID of the previous response to the model. Use this to create multi-turn conversations.
    /// </summary>
    [JsonProperty("previous_response_id")]
    public string? PreviousResponseId { get; set; }
    
    /// <summary>
    /// Reference to a prompt template and its variables.
    /// </summary>
    [JsonProperty("prompt")]
    public PromptConfiguration? Prompt { get; set; }
    
    /// <summary>
    /// Configuration options for reasoning models (o-series models only).
    /// </summary>
    [JsonProperty("reasoning")]
    public ReasoningConfiguration? Reasoning { get; set; }
    
    /// <summary>
    /// Specifies the processing type used for serving the request.
    /// </summary>
    [JsonProperty("service_tier")]
    public ChatRequestServiceTiers? ServiceTier { get; set; }
    
    /// <summary>
    /// Whether to store the generated model response for later retrieval via API. Defaults to true if null.
    /// </summary>
    [JsonProperty("store")]
    public bool? Store { get; set; }
    
    /// <summary>
    /// If set to true, the model response data will be streamed to the client as it is generated using server-sent events. See the Streaming section below for more information.
    /// </summary>
    [JsonProperty("stream")]
    public bool? Stream { get; set; }
    
    /// <summary>
    /// Options for streaming responses. Only set this when <see cref="Stream"/> is <c>true</c>.
    /// </summary>
    [JsonProperty("stream_options")]
    public ResponseStreamOptions? StreamOptions { get; set; }
    
    /// <summary>
    /// What sampling temperature to use, between 0 and 2. Higher values like 0.8 will make the output more random, while lower values like 0.2 will make it more focused and deterministic. We generally recommend altering this or top_p but not both.
    /// </summary>
    [JsonProperty("temperature")]
    public double? Temperature { get; set; }
    
    /// <summary>
    /// Configuration options for a text response from the model. Can be plain text or structured JSON data.
    /// </summary>
    [JsonProperty("text")]
    public ResponseTextConfiguration? Text { get; set; }
    
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
    /// An array of tools the model may call while generating a response. You can specify which tool to use by setting the tool_choice parameter.
    /// </summary>
    [JsonProperty("tools")]
    public List<ResponseTool>? Tools { get; set; }
    
    /// <summary>
    /// An integer between 0 and 20 specifying the number of most likely tokens to return at each token position, each with an associated log probability.
    /// </summary>
    [JsonProperty("top_logprobs")]
    public int? TopLogprobs { get; set; }
    
    /// <summary>
    /// An alternative to sampling with temperature, called nucleus sampling, where the model considers the results of the tokens with top_p probability mass. So 0.1 means only the tokens comprising the top 10% probability mass are considered. We generally recommend altering this or temperature but not both.
    /// </summary>
    [JsonProperty("top_p")]
    public double? TopP { get; set; }
    
    /// <summary>
    /// The truncation strategy to use for the model response.
    /// </summary>
    [JsonProperty("truncation")]
    public ResponseTruncationStrategies? Truncation { get; set; }
    
    /// <summary>
    /// Used by OpenAI to cache responses for similar requests to optimize your cache hit rates. Replaces the user field.
    /// </summary>
    [JsonProperty("prompt_cache_key")]
    public string? PromptCacheKey { get; set; }
    
    /// <summary>
    /// The retention policy for the prompt cache. Set to <see cref="PromptCacheRetention.TwentyFourHours"/> for extended caching (up to 24 hours).
    /// When omitted, OpenAI defaults to extended retention for non-ZDR organizations (since 2026-05-29) and in-memory retention for ZDR organizations.
    /// GPT-5.5 models only support extended retention; serialization coerces <see cref="PromptCacheRetention.InMemory"/> to <see cref="PromptCacheRetention.TwentyFourHours"/> for those models.
    /// </summary>
    [JsonProperty("prompt_cache_retention")]
    public PromptCacheRetention? PromptCacheRetention { get; set; }
    
    /// <summary>
    /// A stable identifier used to help detect users of your application that may be violating OpenAI's usage policies. The IDs should be a string that uniquely identifies each user. We recommend hashing their username or email address, in order to avoid sending us any identifying information.
    /// </summary>
    [JsonProperty("safety_identifier")]
    public string? SafetyIdentifier { get; set; }
    
    /// <summary>
    /// A stable identifier for your end-users. Used to boost cache hit rates by better bucketing similar requests and to help OpenAI detect and prevent abuse.
    /// </summary>
    [JsonProperty("user")]
    public string? User { get; set; }
    
    /// <summary>
    /// Constrains the verbosity of the model's response. Only supported by GPT-5. Lower values will result in more concise responses, while higher values will result in more verbose responses. Currently supported values are low, medium, and high.
    /// </summary>
    [JsonProperty("verbosity")]
    public ChatRequestVerbosities? Verbosity { get; set; }
    
    /// <summary>
    /// Configuration for server-side context management. When set, the server can automatically compact the conversation
    /// when the rendered token count crosses the configured threshold. The compaction item is emitted in the response stream
    /// and carries forward key prior state using fewer tokens.
    /// </summary>
    [JsonProperty("context_management")]
    public List<ResponseContextManagementItem>? ContextManagement { get; set; }

    /// <summary>
    ///	Serializes the chat request into the request body, based on the conventions used by the LLM provider.
    /// </summary>
    public TornadoRequestContent Serialize(IEndpointProvider provider, ResponseRequestSerializeOptions? options = null)
    {
        ApplyModelParameterPolicies(provider);

        string body = this.ToJson(options?.Pretty ?? false);
        return new TornadoRequestContent(body, Model, null, provider, CapabilityEndpoints.Responses);
    }

    internal void ApplyModelParameterPolicies(IEndpointProvider provider)
    {
        if (provider.Provider is LLmProviders.OpenAi)
        {
            if (Model is not null && ChatModelOpenAi.TempIncompatibleModels.Contains(Model))
            {
                Temperature = null;
            }

            bool? hasNonNoneReasoning = Reasoning?.Effort is { } effort ? effort != ResponseReasoningEfforts.None : null;
            if (ChatModelOpenAi.ShouldClearSamplingParams(Model, hasNonNoneReasoning))
            {
                Temperature = null;
                TopP = null;
                TopLogprobs = null;
                Include = Include?.FindAll(field => field is not ResponseIncludeFields.MessageOutputTextLogprobs);
            }

            PromptCacheRetention? retention = PromptCacheRetention;
            ChatModelOpenAi.ApplyPromptCacheRetentionPolicy(Model, ref retention);
            PromptCacheRetention = retention;
        }
    }

    /// <summary>
    /// Creates a new ResponseRequest with the specified model and input string.
    /// </summary>
    /// <param name="model">The model to use for the response</param>
    /// <param name="input">The text input for the response</param>
    public ResponseRequest(ChatModel model, string input)
    {
        Model = model;
        InputString = input;
    }

    /// <summary>
    /// Creates a new ResponseRequest with the specified model and input items.
    /// </summary>
    /// <param name="model">The model to use for the response</param>
    /// <param name="inputItems">The input items for the response</param>
    public ResponseRequest(ChatModel model, List<ResponseInputItem> inputItems)
    {
        Model = model;
        InputItems = inputItems;
    }

    /// <summary>
    /// Creates a new empty ResponseRequest.
    /// </summary>
    public ResponseRequest()
    {
    }

    /// <summary>
    /// Creates a shallow copy of the given request, optionally stripping generation-only fields.
    /// </summary>
    internal ResponseRequest(ResponseRequest basedOn, bool forTokenization = false)
    {
        Background = basedOn.Background;
        Conversation = basedOn.Conversation;
        Include = basedOn.Include;
        InputString = basedOn.InputString;
        InputItems = basedOn.InputItems;
        Instructions = basedOn.Instructions;
        MaxOutputTokens = basedOn.MaxOutputTokens;
        MaxToolCalls = basedOn.MaxToolCalls;
        Metadata = basedOn.Metadata;
        Model = basedOn.Model;
        ParallelToolCalls = basedOn.ParallelToolCalls;
        PreviousResponseId = basedOn.PreviousResponseId;
        Prompt = basedOn.Prompt;
        Reasoning = basedOn.Reasoning;
        ServiceTier = basedOn.ServiceTier;
        Store = basedOn.Store;
        Temperature = basedOn.Temperature;
        Text = basedOn.Text;
        ToolChoice = basedOn.ToolChoice;
        Tools = basedOn.Tools;
        TopLogprobs = basedOn.TopLogprobs;
        TopP = basedOn.TopP;
        Truncation = basedOn.Truncation;
        PromptCacheKey = basedOn.PromptCacheKey;
        PromptCacheRetention = basedOn.PromptCacheRetention;
        SafetyIdentifier = basedOn.SafetyIdentifier;
        User = basedOn.User;
        Verbosity = basedOn.Verbosity;
        ContextManagement = basedOn.ContextManagement;

        if (forTokenization)
        {
            Stream = null;
            StreamOptions = null;
            Store = null;
            Background = null;
        }
        else
        {
            Stream = basedOn.Stream;
            StreamOptions = basedOn.StreamOptions;
        }
    }
}
