using LlmTornado.Audio;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.ChatFunctions;
using LlmTornado.Code.Models;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;
using LlmTornado.Images;
using LlmTornado.Infra;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Code;

public class Ref<T>
{
    public T? Ptr { get; set; }
}

/// <summary>
/// Endpoints with which a model is compatible.
/// </summary>
public enum ChatModelEndpointCapabilities
{
    /// <summary>
    /// /chat
    /// </summary>
    Chat,
    /// <summary>
    /// /responses
    /// </summary>
    Responses,
    /// <summary>
    /// /batch
    /// </summary>
    Batch,
    /// <summary>
    /// /realtime (voice agents, GA WebSocket/WebRTC)
    /// </summary>
    Realtime,
    /// <summary>
    /// /live/sessions (GPT-Live full-duplex voice)
    /// </summary>
    Live
}

/// <summary>
/// Response with a stream content.
/// </summary>
public class StreamResponse
{
    /// <summary>
    /// The stream.
    /// </summary>
    public Stream Stream { get; set; }
    
    /// <summary>
    /// Headers of the response.
    /// </summary>
    public ApiResultBase Headers { get; set; }
    
    /// <summary>
    /// Accompanying HTTP response message.
    /// </summary>
    public HttpResponseMessage Response { get; set; }
}

/// <summary>
///     A failed HTTP request.
/// </summary>
public class HttpFailedRequest
{
    /// <summary>
    ///     The exception with details what went wrong.
    /// </summary>
    public Exception Exception { get; set; }
    
    /// <summary>
    ///     The request that failed.
    /// </summary>
    public HttpCallRequest? Request { get; set; }
    
    /// <summary>
    ///     Result of the failed request.
    /// </summary>
    public IHttpCallResult? Result { get; set; }
    
    /// <summary>
    ///     Raw message of the failed request. Do not dispose this, it will be disposed automatically by Tornado.
    /// </summary>
    public HttpResponseMessage RawMessage { get; set; }
    
    /// <summary>
    ///     Body of the request.
    /// </summary>
    public TornadoRequestContent Body { get; set; }
}

/// <summary>
///     Streaming HTTP request.
/// </summary>
public class TornadoStreamRequest : IAsyncDisposable
{
    public Stream? Stream { get; set; }
    public HttpResponseMessage? Response { get; set; }
    public StreamReader? StreamReader { get; set; }
    public Exception? Exception { get; set; }
    public HttpCallRequest? CallRequest { get; set; }
    public IHttpCallResult? CallResponse { get; set; }

    /// <summary>
    ///     Disposes the underlying stream.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Stream is not null)
        {
#if MODERN
            await Stream.DisposeAsync().ConfigureAwait(false);   
#else
            Stream.Dispose();
#endif
        }
        
        Response?.Dispose();
        StreamReader?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
///     Roles of chat participants.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatMessageRoles
{
    /// <summary>
    ///     Unknown role.
    /// </summary>
    [EnumMember(Value = "unknown")]
    Unknown,
    /// <summary>
    ///     System prompt / preamble / developer message.
    /// </summary>
    [EnumMember(Value = "system")]
    System,
    /// <summary>
    ///     Messages written by user.
    /// </summary>
    [EnumMember(Value = "user")]
    User,
    /// <summary>
    ///     Assistant messages.
    /// </summary>
    [EnumMember(Value = "assistant")]
    Assistant,
    /// <summary>
    ///     Messages representing tool/function/connector usage.
    /// </summary>
    [EnumMember(Value = "tool")]
    Tool
}

/// <summary>
/// The type of the predicted content you want to provide. 
/// </summary>
public enum ChatRequestPredictionTypes
{
    /// <summary>
    /// Static predicted output content.
    /// </summary>
    Content
}

/// <summary>
/// Configuration for a Predicted Output, which can greatly improve response times when large parts of the model response are known ahead of time. This is most common when you are regenerating a file with only minor changes to most of the content.
/// </summary>
public class ChatRequestPrediction
{
    /// <summary>
    /// The type of the predicted content you want to provide. This type is currently always "content".
    /// </summary>
    [JsonProperty("type")]
    public ChatRequestPredictionTypes Type { get; set; }

    /// <summary>
    /// Serialized content, either from <see cref="Parts"/> or <see cref="Text"/>
    /// </summary>
    [JsonProperty("content")]
    public object? Content => Parts?.Count > 0 ? Parts : Text;

    /// <summary>
    /// The content used for a Predicted Output. This is often the text of a file you are regenerating with minor changes.
    /// </summary>
    [JsonIgnore]
    public string? Text { get; set; }
    
    /// <summary>
    /// An array of content parts with a defined type. Supported options differ based on the model being used to generate the response. Can contain text inputs.
    /// </summary>
    [JsonIgnore]
    public List<ChatRequestPredictionPart>? Parts { get; set; }
}

/// <summary>
/// High level guidance for the amount of context window space to use for the search. One of low, medium, or high. medium is the default.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatRequestWebSearchContextSize
{
    /// <summary>
    /// Low context, lowest cost.
    /// </summary>
    [EnumMember(Value = "low")]
    Low,
    
    /// <summary>
    /// Balanced cost/performance.
    /// </summary>
    [EnumMember(Value = "medium")]
    Medium,
    
    /// <summary>
    /// Highest budget, best performance.
    /// </summary>
    [EnumMember(Value = "high")]
    High
}

/// <summary>
/// Types of the user location.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatRequestWebSearchUserLocationTypes
{
    /// <summary>
    /// Approximate location.
    /// </summary>
    [EnumMember(Value = "approximate")]
    Approximate
}

/// <summary>
/// Configuration of the user location, aids search relevancy.
/// </summary>
public class ChatRequestWebSearchUserLocation
{
    /// <summary>
    /// The type of location approximation. Always approximate.
    /// </summary>
    [JsonIgnore]
    public ChatRequestWebSearchUserLocationTypes Type { get; set; } = ChatRequestWebSearchUserLocationTypes.Approximate;
    
    /// <summary>
    /// Free text input for the city of the user, e.g. San Francisco.
    /// </summary>
    [JsonProperty("city")]
    public string? City { get; set; }
    
    /// <summary>
    /// The two-letter ISO country code of the user, e.g. US.
    /// </summary>
    [JsonProperty("country")]
    public string? Country { get; set; }
    
    /// <summary>
    /// Free text input for the region of the user, e.g. California.
    /// </summary>
    [JsonProperty("region")]
    public string? Region { get; set; }
    
    /// <summary>
    /// The IANA timezone of the user, e.g. America/Los_Angeles.
    /// </summary>
    [JsonProperty("timezone")]
    public string? Timezone { get; set; }
}


/// <summary>
/// Thinking blocks for Anthropic Claude 3.7+ models.
/// </summary>
public class ChatChoiceAnthropicThinkingBlock
{
    /// <summary>
    /// Content of the thinking block.
    /// </summary>
    public string Content { get; set; }
	
    /// <summary>
    /// This field holds a cryptographic token which verifies that the thinking block was generated by Claude, and is verified when thinking blocks are passed back to the API. When streaming responses, the signature is added via a signature_delta inside a content_block_delta event just before the content_block_stop event. It is only strictly necessary to send back thinking blocks when using tool use with extended thinking. Otherwise you can omit thinking blocks from previous turns, or let the API strip them for you if you pass them back.
    /// </summary>
    public string Signature { get; set; }

    /// <summary>
    /// Whether Anthropic omitted thinking text while preserving the signature.
    /// </summary>
    public bool Omitted { get; set; }
}

/// <summary>
/// Anthropic extensions to chat choices.
/// </summary>
public class ChatChoiceVendorExtensionsAnthropic : IChatChoiceVendorExtensions
{
    /// <summary>
    /// Thinking blocks.
    /// </summary>
    public List<ChatChoiceAnthropicThinkingBlock>? Thinking { get; set; }
}

/// <summary>
/// Policy category that triggered an Anthropic refusal response.
/// </summary>
public enum ChatRefusalCategory
{
    /// <summary>
    /// Cybersecurity-related policy refusal.
    /// </summary>
    Cyber,
    
    /// <summary>
    /// Biological-related policy refusal.
    /// </summary>
    Bio
}

/// <summary>
/// Additional details returned alongside <see cref="ChatMessageFinishReasons.Refusal"/> stop reasons.
/// Currently returned by Anthropic Claude Opus 4.7+ models.
/// </summary>
public class ChatStopDetails
{
    /// <summary>
    /// Type of stop details. Always "refusal" when present on refusal responses.
    /// </summary>
    public string Type { get; set; } = "refusal";
    
    /// <summary>
    /// Policy category that triggered the refusal. Null when the refusal doesn't map to a named category.
    /// </summary>
    public ChatRefusalCategory? Category { get; set; }
    
    /// <summary>
    /// Human-readable description of the refusal. Not guaranteed to be stable — don't parse programmatically.
    /// </summary>
    public string? Explanation { get; set; }
}

/// <summary>
/// A custom JSON converter for handling different response formats.
/// </summary>
internal class ChatMessageFinishReasonsConverter : JsonConverter<ChatMessageFinishReasons>
{
    internal static readonly FrozenDictionary<string, ChatMessageFinishReasons> Map = new Dictionary<string, ChatMessageFinishReasons>(StringComparer.OrdinalIgnoreCase)
    {
        { "stop", ChatMessageFinishReasons.EndTurn },
        { "end_turn", ChatMessageFinishReasons.EndTurn },
        { "complete", ChatMessageFinishReasons.EndTurn },
        
        { "stop_sequence", ChatMessageFinishReasons.StopSequence },
        
        { "length", ChatMessageFinishReasons.Length },
        { "max_tokens", ChatMessageFinishReasons.Length },
        { "error_limit", ChatMessageFinishReasons.Length },

        { "content_filter", ChatMessageFinishReasons.ContentFilter },
        { "safety", ChatMessageFinishReasons.ContentFilter },
        { "error_toxic", ChatMessageFinishReasons.ContentFilter },

        { "recitation", ChatMessageFinishReasons.Recitation },
        { "language", ChatMessageFinishReasons.UnsupportedLanguage },
        { "blocklist", ChatMessageFinishReasons.Blocklist },
        { "prohibited_content", ChatMessageFinishReasons.ProhibitedContent },
        { "spii", ChatMessageFinishReasons.SensitivePersonalInformation },
        { "malformed_function_call", ChatMessageFinishReasons.MalformedToolCall },
        { "image_safety", ChatMessageFinishReasons.ImageSafety },
        { "image_prohibited_content", ChatMessageFinishReasons.ImageProhibitedContent },
        { "image_other", ChatMessageFinishReasons.ImageOther },
        { "no_image", ChatMessageFinishReasons.NoImage },
        { "image_recitation", ChatMessageFinishReasons.ImageRecitation },
        { "error", ChatMessageFinishReasons.Error },
        { "other", ChatMessageFinishReasons.Error },

        { "tool_use", ChatMessageFinishReasons.ToolCalls },
        { "tool_calls", ChatMessageFinishReasons.ToolCalls },
        { "function_call", ChatMessageFinishReasons.ToolCalls },
        { "unexpected_tool_call", ChatMessageFinishReasons.UnexpectedToolCall },
        { "too_many_tool_calls", ChatMessageFinishReasons.TooManyToolCalls },
        
        { "model_context_window_exceeded", ChatMessageFinishReasons.ContextWindowExceeded },
        
        { "user_cancel", ChatMessageFinishReasons.Cancel },
        { "abort", ChatMessageFinishReasons.Cancel },
        
        { "refusal", ChatMessageFinishReasons.Refusal },
        
        { "compaction", ChatMessageFinishReasons.Compaction }
    }.ToFrozenDictionary();
    
    /// <summary>
    /// Reads and converts JSON input into the appropriate ResponseFormat object.
    /// </summary>
    public override ChatMessageFinishReasons ReadJson(JsonReader reader, Type objectType, ChatMessageFinishReasons existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType is JsonToken.String)
        {
            string str = reader.Value as string ?? string.Empty;
            return Map.GetValueOrDefault(str, ChatMessageFinishReasons.Unknown);   
        }

        return ChatMessageFinishReasons.Unknown;
    }

    /// <summary>
    /// Writes the ResponseFormat object as JSON output.
    /// </summary>
    public override void WriteJson(JsonWriter writer, ChatMessageFinishReasons value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString());
    }
}

/// <summary>
/// Types of reasons for ending the response.
/// </summary>
public enum ChatMessageFinishReasons
{
    /// <summary>
    /// Unknown reason, used as a fallback.
    /// </summary>
    Unknown,
    
    /// <summary>
    /// The model hit a natural stop point. Most providers report hitting stop sequences as "stop".
    /// </summary>
    EndTurn,
    
    /// <summary>
    /// The model hit your stop sequence. Currently reported only by Anthropic, Cohere.
    /// </summary>
    StopSequence,
    
    /// <summary>
    /// The maximum number of tokens specified in the request was reached.
    /// </summary>
    Length,
    
    /// <summary>
    /// Content was omitted due to a flag from content filters.
    /// </summary>
    ContentFilter,
    
    /// <summary>
    /// The model called tools.
    /// </summary>
    ToolCalls,
    
    /// <summary>
    /// The response candidate content was flagged for recitation reasons.
    /// </summary>
    Recitation,
    
    /// <summary>
    /// The response candidate content was flagged for using an unsupported language.
    /// </summary>
    UnsupportedLanguage,
    
    /// <summary>
    /// Token generation stopped because the content contains forbidden terms.
    /// </summary>
    Blocklist,
    
    /// <summary>
    /// Token generation stopped for potentially containing prohibited content.
    /// </summary>
    ProhibitedContent,
    
    /// <summary>
    /// The generation stopped because the content potentially contains Sensitive Personally Identifiable Information (SPII).
    /// </summary>
    SensitivePersonalInformation,
    
    /// <summary>
    /// The tool call generated by the model is invalid.
    /// </summary>
    MalformedToolCall,
    
    /// <summary>
    /// Token generation stopped because generated images contain safety violations.
    /// </summary>
    ImageSafety,
    
    /// <summary>
    /// Image generation stopped because generated images contain prohibited content.
    /// </summary>
    ImageProhibitedContent,
    
    /// <summary>
    /// Image generation stopped due to a miscellaneous issue.
    /// </summary>
    ImageOther,
    
    /// <summary>
    /// The model was expected to generate an image but none was produced.
    /// </summary>
    NoImage,
    
    /// <summary>
    /// Image generation stopped due to recitation.
    /// </summary>
    ImageRecitation,
    
    /// <summary>
    /// The model generated a tool call but no tools were enabled in the request.
    /// </summary>
    UnexpectedToolCall,
    
    /// <summary>
    /// The model called too many tools consecutively, causing the system to exit execution.
    /// </summary>
    TooManyToolCalls,
    
    /// <summary>
    /// The request was canceled/aborted.
    /// </summary>
    Cancel,
    
    /// <summary>
    /// There was an error while processing the request.
    /// </summary>
    Error,
    
    /// <summary>
    /// Response hit context window limit before max_tokens
    /// </summary>
    ContextWindowExceeded,
    
    /// <summary>
    /// Prompt was blocked due to safety reasons.
    /// </summary>
    Safety,
    
    /// <summary>
    /// The model refused to complete the request. Handle appropriately in your application.
    /// </summary>
    Refusal,
    
    /// <summary>
    /// Compaction was triggered and the API paused after generating the compaction summary.
    /// Only returned when pause_after_compaction is enabled. Anthropic-specific.
    /// </summary>
    Compaction
}

/// <summary>
/// Shared interface for vendor extensions to chat choices.
/// </summary>
public interface IChatChoiceVendorExtensions
{
	
}

/// <summary>
/// Configuration of the web search options.
/// </summary>
[JsonConverter(typeof(ChatRequestWebSearchOptionsJsonConverter))]
public class ChatRequestWebSearchOptions
{
    /// <summary>
    /// High level guidance for the amount of context window space to use for the search. One of low, medium, or high. medium is the default.
    /// </summary>
    [JsonProperty("search_context_size")]
    public ChatRequestWebSearchContextSize? SearchContextSize { get; set; }
    
    /// <summary>
    /// Approximate location parameters for the search.
    /// </summary>
    [JsonProperty("user_location")]
    public ChatRequestWebSearchUserLocation? UserLocation { get; set; }
}

internal class ChatRequestWebSearchOptionsJsonConverter : JsonConverter<ChatRequestWebSearchOptions>
{
    public override void WriteJson(JsonWriter writer, ChatRequestWebSearchOptions? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();

        if (value.SearchContextSize.HasValue)
        {
            writer.WritePropertyName("search_context_size");
            serializer.Serialize(writer, value.SearchContextSize.Value);
        }

        if (value.UserLocation != null)
        {
            writer.WritePropertyName("user_location");
            writer.WriteStartObject();
        
            writer.WritePropertyName("type");
            writer.WriteValue("approximate");
            
            writer.WritePropertyName("approximate");
            writer.WriteStartObject();
        
            if (!string.IsNullOrEmpty(value.UserLocation.City))
            {
                writer.WritePropertyName("city");
                writer.WriteValue(value.UserLocation.City);
            }
        
            if (!string.IsNullOrEmpty(value.UserLocation.Country))
            {
                writer.WritePropertyName("country");
                writer.WriteValue(value.UserLocation.Country);
            }
        
            if (!string.IsNullOrEmpty(value.UserLocation.Region))
            {
                writer.WritePropertyName("region");
                writer.WriteValue(value.UserLocation.Region);
            }
        
            if (!string.IsNullOrEmpty(value.UserLocation.Timezone))
            {
                writer.WritePropertyName("timezone");
                writer.WriteValue(value.UserLocation.Timezone);
            }
        
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    public override ChatRequestWebSearchOptions? ReadJson(JsonReader reader, Type objectType, ChatRequestWebSearchOptions? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        return existingValue;
    }
}

/// <summary>
/// A prediction part.
/// </summary>
public class ChatRequestPredictionPart 
{
    /// <summary>
    /// The text content.
    /// </summary>
    [JsonProperty("text")]
    public string Text { get; set; }
    
    /// <summary>
    /// The type of the content part.
    /// </summary>
    [JsonProperty("type")]
    public ChatRequestPredictionTypes Type { get; set; }

    /// <summary>
    /// Creates a new text prediction part.
    /// </summary>
    /// <param name="text"></param>
    public ChatRequestPredictionPart(string text)
    {
        Text = text;
        Type = ChatRequestPredictionTypes.Content;
    }
}

/// <summary>
/// Constrains the verbosity of the model's response. Lower values will result in more concise responses, while higher values will result in more verbose responses.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatRequestVerbosities
{
    /// <summary>
    /// Most concise responses.
    /// </summary>
    [EnumMember(Value = "low")]
    Low,
    
    /// <summary>
    /// Balanced responses.
    /// </summary>
    [EnumMember(Value = "medium")]
    Medium,
    
    /// <summary>
    /// Verbose responses.
    /// </summary>
    [EnumMember(Value = "high")]
    High
}

/// <summary>
/// Flex processing provides significantly lower costs for Chat Completions or Responses requests in exchange for slower response times and occasional resource unavailability. It is ideal for non-production or lower-priority tasks such as model evaluations, data enrichment, or asynchronous workloads.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatRequestServiceTiers
{
    /// <summary>
    /// If set to 'auto', and the Project is Scale tier enabled, the system will utilize scale tier credits until they are exhausted.
    /// If set to 'auto', and the Project is not Scale tier enabled, the request will be processed using the default service tier with a lower uptime SLA and no latency guarantee.
    /// </summary>
    [EnumMember(Value = "auto")]
    Auto,
    
    /// <summary>
    /// If set to 'default', the request will be processed using the default service tier with a lower uptime SLA and no latency guarantee.
    /// </summary>
    [EnumMember(Value = "default")]
    Default,
    
    /// <summary>
    /// If set to 'flex', the request will be processed with the Flex Processing service tier.
    /// Supported by OpenAI and Google Gemini (50% cost discount, 1-15 minute target latency, best-effort availability).
    /// </summary>
    [EnumMember(Value = "flex")]
    Flex,
    
    /// <summary>
    /// Scale tier processing with higher rate limits and guaranteed latency SLA.
    /// </summary>
    [EnumMember(Value = "scale")]
    Scale,

    /// <summary>
    /// Additional option for service_tier supported by Groq
    /// </summary>
    [EnumMember(Value = "on_demand")]
    OnDemand,

    /// <summary>
    /// Additional option for service_tier supported by Groq
    /// </summary>
    [EnumMember(Value = "performance")]
    Performance,
    
    /// <summary>
    ///  Only use standard tier capacity, useful if you don't want to use your Priority Tier capacity, supported only by Anthropic.
    /// </summary>
    [EnumMember(Value = "standard_only")]
    StandardOnly,
    
    /// <summary>
    /// Requests priority-tier inference for lower latency and higher reliability at premium pricing.
    /// Supported by Google Gemini. Also returned as a response value by Anthropic and Google (via the <c>x-gemini-service-tier</c> header).
    /// For OpenAI, <c>priority</c> is accepted and automatically mapped to Fast mode.
    /// </summary>
    [EnumMember(Value = "priority")]
    Priority,
    
    /// <summary>
    /// OpenAI Fast mode. Replaces Priority Processing. For GPT-5.6 and later supported models,
    /// Fast mode delivers up to 2.5× faster speeds than standard processing at twice the price.
    /// Requests tagged <see cref="Priority"/> automatically use Fast mode.
    /// </summary>
    [EnumMember(Value = "fast")]
    Fast,
    
    /// <summary>
    /// OpenAI Ultrafast mode. Limited-preview service tier for GPT-5.6 Sol that can run
    /// up to 14× faster than standard processing.
    /// </summary>
    [EnumMember(Value = "ultrafast")]
    Ultrafast
}

/// <summary>
/// Controls the inference speed tier for supported models. Currently supported by Anthropic (Claude Opus 4.8 and Claude Opus 5).
/// Fast mode was removed for Opus 4.6 (falls back to standard) and Opus 4.7 (returns an error).
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatRequestSpeeds
{
    /// <summary>
    /// Standard inference speed. This is the default when no speed is specified.
    /// This value is returned in responses to indicate standard speed was used.
    /// </summary>
    [EnumMember(Value = "standard")]
    Standard,

    /// <summary>
    /// Fast mode. Up to 2.5x higher output tokens per second compared to standard speed. Premium pricing applies.
    /// Supported by Anthropic on Claude Opus 4.8 and Claude Opus 5. Requires the <c>fast-mode-2026-02-01</c> beta header.
    /// </summary>
    [EnumMember(Value = "fast")]
    Fast
}

/// <summary>
/// Represents one token/chunk of a streamed response
/// </summary>
public class StreamedMessageToken
{
    /// <summary>
    /// Text content of the chunk.
    /// </summary>
    public string? Content { get; set; }
    
    /// <summary>
    /// Index of the token.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Text representation
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return Content ?? string.Empty;
    }
}

/// <summary>
///     Reasoning formats.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatReasoningFormats
{
    /// <summary>
    /// Separates reasoning into a dedicated field while keeping the response concise.
    /// </summary>
    [EnumMember(Value = "parsed")]
    Parsed,
    
    /// <summary>
    /// Includes reasoning within think tags in the content.
    /// </summary>
    [EnumMember(Value = "raw")]
    Raw,
    
    /// <summary>
    /// Returns only the final answer.
    /// </summary>
    [EnumMember(Value = "hidden")]
    Hidden
}

/// <summary>
/// Prompt cache retention policy for OpenAI prompt caching on chat completions, responses, and batch.
/// </summary>
/// <remarks>
/// As of 2026-05-29, organizations without zero data retention (ZDR) default to <see cref="TwentyFourHours"/> when this parameter is omitted.
/// Organizations with ZDR enabled default to <see cref="InMemory"/> when omitted.
/// GPT-5.5 and GPT-5.5 Pro only support extended caching (<see cref="TwentyFourHours"/>).
/// </remarks>
[JsonConverter(typeof(StringEnumConverter))]
public enum PromptCacheRetention
{
    /// <summary>
    /// Standard in-memory prompt cache retention.
    /// </summary>
    [EnumMember(Value = "in_memory")]
    InMemory,
    
    /// <summary>
    /// Extended prompt cache retention (up to 24 hours).
    /// </summary>
    [EnumMember(Value = "24h")]
    TwentyFourHours
}

/// <summary>
///     Level of reasoning suggested.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatReasoningEfforts
{
    /// <summary>
    ///     No reasoning - model behaves like a non-reasoning model (GPT-5.1, Groq).
    /// </summary>
    [EnumMember(Value = "none")]
    None,
    
    /// <summary>
    ///     Minimal reasoning. Supported only by GPT-5.
    /// </summary>
    [EnumMember(Value = "minimal")]
    Minimal,
    
    /// <summary>
    ///     Low reasoning - fast responses (O1, O1 Mini, GPT-5.1, Grok 3, Kimi K3)
    /// </summary>
    [EnumMember(Value = "low")]
    Low,
    
    /// <summary>
    ///     Balanced reasoning (O1, O1 Mini, GPT-5.1)
    /// </summary>
    [EnumMember(Value = "medium")]
    Medium,
    
    /// <summary>
    ///     High reasoning - slow responses (O1, O1 Mini, GPT-5.1, Grok 3, Kimi K3)
    /// </summary>
    [EnumMember(Value = "high")]
    High,
    
    /// <summary>
    ///     Extra high reasoning. Available for GPT-6 Astra, GPT-5.6, GPT-5.5, GPT-5.2, GPT-5.2-Codex, GPT-5.3-Codex, GPT-5.4, and GPT-5.1-Codex-Max.
    /// </summary>
    [EnumMember(Value = "xhigh")]
    XHigh,
    
    /// <summary>
    ///     Maximum reasoning with no constraints on token spending.
    ///     Available for GPT-6 Astra, GPT-5.6, Claude Opus 4.6, and Kimi K3 (K3 default). Requests using max on other models will return an error.
    /// </summary>
    [EnumMember(Value = "max")]
    Max,
    
    /// <summary>
    ///     Enable reasoning. Supported only by Groq.
    /// </summary>
    [EnumMember(Value = "default")]
    Default
}

internal enum ChatResultStreamInternalKinds
{
    Unknown,
    None,
    
    /// <summary>
    /// Appends the message to the conversation.
    /// </summary>
    AppendAssistantMessage,
    
    /// <summary>
    /// Similar to <see cref="AppendAssistantMessage"/> but doesn't append the message. Used for reasoning blocks.
    /// </summary>
    AssistantMessageTransientBlock,
    
    /// <summary>
    /// Usage, finish_reason, and other metadata
    /// </summary>
    FinishData
}

/// <summary>
/// Data returned after streaming stops.
/// </summary>
public class ChatStreamFinishedData
{
    /// <summary>
    /// The bill.
    /// </summary>
    public ChatUsage Usage { get; set; }
    
    /// <summary>
    /// Reason why the streaming stopped.
    /// </summary>
    public ChatMessageFinishReasons FinishReason { get; set; }
    
    /// <summary>
    /// The stop string or token ID that caused the completion to stop, if applicable.
    /// Returned by some providers like vLLM.
    /// </summary>
    public string? StopReason { get; set; }
    
    /// <summary>
    /// Additional details for refusal stop reasons. Currently returned by Anthropic Claude Opus 4.7+ models.
    /// </summary>
    public ChatStopDetails? StopDetails { get; set; }

    internal ChatStreamFinishedData(ChatUsage usage, ChatMessageFinishReasons finishReason, string? stopReason = null, ChatStopDetails? stopDetails = null)
    {
        Usage = usage;
        FinishReason = finishReason;
        StopReason = stopReason;
        StopDetails = stopDetails;
    }
}

/// <summary>
/// Wrapper for tool arguments.
/// </summary>
public class ChatFunctionParamsGetter
{
    internal Dictionary<string, object?>? Source { get; set; }

    /// <summary>
    /// Creates an argument getter from a dictionary.
    /// </summary>
    /// <param name="pars"></param>
    public ChatFunctionParamsGetter(Dictionary<string, object?>? pars)
    {
        Source = pars;
    }
}

internal class ToolCallInboundAccumulator
{
    public ToolCall ToolCall { get; set; }
    public StringBuilder ArgumentsBuilder { get; set; }
}

/// <summary>
/// Shared interface for audio objects.
/// </summary>
public interface IChatAudio
{
    /// <summary>
    /// Data of the audio (url, base64..)
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    /// MIME type.
    /// </summary>
    public string? MimeType { get; set; }
    
    /// <summary>
    /// Format.
    /// </summary>
    public ChatAudioFormats? Format { get; set; }
}

/// <summary>
///     Audio block content.
/// </summary>
public class ChatMessageAudio : IChatAudio
{
    /// <summary>
    ///     Unique identifier for this audio response.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; }
    
    /// <summary>
    ///     The Unix timestamp (in seconds) for when this audio response will no longer be accessible on the server for use in multi-turn conversations.
    /// </summary>
    [JsonProperty("expires_at")]
    public long ExpiresAt { get; set; }
    
    /// <summary>
    ///     Base64 encoded audio bytes generated by the model, in the format specified in the request.
    /// </summary>
    [JsonProperty("data")]
    public string? Data { get; set; }

    /// <summary>
    ///     Converts <see cref="Data"/> from base64 to a byte array.
    /// </summary>
    public byte[] ByteData => Data is null ? [] : Convert.FromBase64String(Data);
    
    /// <summary>
    ///     Transcript of the audio generated by the model.
    /// </summary>
    [JsonProperty("transcript")]
    public string Transcript { get; set; }
    
    /// <summary>
    /// MIME type of the audio.
    /// </summary>
    [JsonIgnore]
    public string? MimeType { get; set; }
    
    /// <summary>
    /// Format of the audio.
    /// </summary>
    [JsonIgnore]
    public ChatAudioFormats? Format { get; set; }
}

/// <summary>
///     Strategies for reducing audio modality pricing.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatAudioCompressionStrategies
{
    /// <summary>
    ///     Audio encoding is preferred both for input and output.
    /// </summary>
    Native,
    
    /// <summary>
    ///     Output is encoded as text when possible.
    /// </summary>
    OutputAsText,
    
    /// <summary>
    ///     Output is encoded as previous audio id when not expired; falls to <see cref="OutputAsText"/> otherwise.
    /// </summary>
    PreferNative
}

/// <summary>
///     Audio settings.
/// </summary>
public class ChatRequestAudio
{
    /// <summary>
    ///     The voice to use.
    /// </summary>
    [JsonProperty("voice")]
    public ChatAudioRequestKnownVoices? Voice { get; set; }
    
    /// <summary>
    ///     The output audio format.
    /// </summary>
    [JsonProperty("format")]
    public ChatRequestAudioFormats? Format { get; set; }

    /// <summary>
    ///     The compression strategy to use when serializing requests.
    /// </summary>
    [JsonIgnore] 
    public ChatAudioCompressionStrategies CompressionStrategy { get; set; } = ChatAudioCompressionStrategies.PreferNative;
    
    /// <summary>
    ///     Creates a new audio settings from a known voice.
    /// </summary>
    /// <param name="voice"></param>
    /// <param name="format"></param>
    public ChatRequestAudio(ChatAudioRequestKnownVoices voice, ChatRequestAudioFormats format)
    {
        Voice = voice;
        Format = format;
    }
    
    /// <summary>
    /// Creates an empty audio object.
    /// </summary>
    public ChatRequestAudio()
    {

    }
}

/// <summary>
///     Formats in which the transcription can be returned.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AudioTranscriptionResponseFormats
{
    /// <summary>
    ///     JSON.
    /// </summary>
    [EnumMember(Value = "json")]
    Json,
    
    /// <summary>
    ///     Plaintext.
    /// </summary>
    [EnumMember(Value = "text")]
    Text,
    
    /// <summary>
    ///     SubRip Subtitle.
    /// </summary>
    [EnumMember(Value = "srt")]
    Srt,
    
    /// <summary>
    ///     Json with details.
    /// </summary>
    [EnumMember(Value = "verbose_json")]
    VerboseJson,
    
    /// <summary>
    ///     Video Text to Track.
    /// </summary>
    [EnumMember(Value = "vtt")]
    Vtt,
    
    /// <summary>
    ///     Diarized JSON.
    /// </summary>
    [EnumMember(Value = "diarized_json")]
    DiarizedJson
}

/// <summary>
///     Output audio formats.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatRequestAudioFormats
{
    /// <summary>
    ///     Waveform
    /// </summary>
    [EnumMember(Value = "wav")]
    Wav,
    /// <summary>
    ///     MP3
    /// </summary>
    [EnumMember(Value = "mp3")]
    Mp3,
    /// <summary>
    ///     Flac
    /// </summary>
    [EnumMember(Value = "flac")]
    Flac,
    /// <summary>
    ///     Opus
    /// </summary>
    [EnumMember(Value = "opus")]
    Opus,
    /// <summary>
    ///    Pulse-code modulation. Supported in streaming mode.
    /// </summary>
    [EnumMember(Value = "pcm16")]
    Pcm16
}

/// <summary>
///     Known chat request audio settings voices.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatAudioRequestKnownVoices
{
    /// <summary>
    ///     Male voice, deep.
    /// </summary>
    [EnumMember(Value = "ash")]
    Ash,
    
    /// <summary>
    ///     Male voice, younger.
    /// </summary>
    [EnumMember(Value = "ballad")]
    Ballad,
    
    [EnumMember(Value = "coral")]
    Coral,
    [EnumMember(Value = "sage")]
    Sage,
    [EnumMember(Value = "verse")]
    Verse,
    
    /// <summary>
    ///     Not recommended by OpenAI, less expressive.
    /// </summary>
    [EnumMember(Value = "alloy")]
    Alloy,
    /// <summary>
    ///     Not recommended by OpenAI, less expressive.
    /// </summary>
    [EnumMember(Value = "echo")]
    Echo,
    /// <summary>
    ///     Not recommended by OpenAI, less expressive.
    /// </summary>
    [EnumMember(Value = "summer")]
    Summer
}

/// <summary>
///     Represents modalities of chat models.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatModelModalities
{
    /// <summary>
    ///     Model is capable of generating text.
    /// </summary>
    [EnumMember(Value = "text")]
    Text,
    /// <summary>
    ///     Model is capable of generating audio.
    /// </summary>
    [EnumMember(Value = "audio")]
    Audio,
    /// <summary>
    ///     Model is capable of generating images (currently only Gemini 2.0+)
    /// </summary>
    [EnumMember(Value = "image")]
    Image
}

/// <summary>
///     Aspect ratios for generated images in chat responses.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatImageAspectRatios
{
    /// <summary>
    ///     1:1 - Square format.
    /// </summary>
    [EnumMember(Value = "1:1")]
    Square,
    /// <summary>
    ///     2:3 - Portrait format.
    /// </summary>
    [EnumMember(Value = "2:3")]
    Portrait2x3,
    /// <summary>
    ///     3:2 - Landscape format.
    /// </summary>
    [EnumMember(Value = "3:2")]
    Landscape3x2,
    /// <summary>
    ///     3:4 - Portrait format.
    /// </summary>
    [EnumMember(Value = "3:4")]
    Portrait3x4,
    /// <summary>
    ///     4:3 - Standard landscape format.
    /// </summary>
    [EnumMember(Value = "4:3")]
    Landscape4x3,
    /// <summary>
    ///     4:5 - Portrait format (Instagram-like).
    /// </summary>
    [EnumMember(Value = "4:5")]
    Portrait4x5,
    /// <summary>
    ///     5:4 - Landscape format.
    /// </summary>
    [EnumMember(Value = "5:4")]
    Landscape5x4,
    /// <summary>
    ///     9:16 - Vertical/mobile portrait format.
    /// </summary>
    [EnumMember(Value = "9:16")]
    Portrait9x16,
    /// <summary>
    ///     16:9 - Widescreen landscape format.
    /// </summary>
    [EnumMember(Value = "16:9")]
    Landscape16x9,
    /// <summary>
    ///     21:9 - Ultra-wide cinematic format.
    /// </summary>
    [EnumMember(Value = "21:9")]
    Ultrawide21x9,
    /// <summary>
    ///     1:4 - Tall portrait format. Supported by Gemini 3.1 Flash Image.
    /// </summary>
    [EnumMember(Value = "1:4")]
    Portrait1x4,
    /// <summary>
    ///     4:1 - Wide landscape format. Supported by Gemini 3.1 Flash Image.
    /// </summary>
    [EnumMember(Value = "4:1")]
    Landscape4x1,
    /// <summary>
    ///     1:8 - Extra-tall portrait format. Supported by Gemini 3.1 Flash Image.
    /// </summary>
    [EnumMember(Value = "1:8")]
    Portrait1x8,
    /// <summary>
    ///     8:1 - Extra-wide landscape format. Supported by Gemini 3.1 Flash Image.
    /// </summary>
    [EnumMember(Value = "8:1")]
    Landscape8x1
}

/// <summary>
///     Output resolution for generated images in chat responses.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatImageResolutions
{
    /// <summary>
    ///     512px (0.5K) resolution. Supported by Gemini 3.1 Flash Image only; API value is "512" (no K suffix).
    /// </summary>
    [EnumMember(Value = "512")]
    Resolution512,
    /// <summary>
    ///     1K resolution (default for most models).
    /// </summary>
    [EnumMember(Value = "1K")]
    Resolution1K,
    /// <summary>
    ///     2K resolution.
    /// </summary>
    [EnumMember(Value = "2K")]
    Resolution2K,
    /// <summary>
    ///     4K resolution (highest quality).
    /// </summary>
    [EnumMember(Value = "4K")]
    Resolution4K
}

/// <summary>
///     Configuration for image output modality in chat responses.
///     Used when <see cref="ChatModelModalities.Image"/> is included in the request modalities.
/// </summary>
public class ChatImageOutputConfig
{
    /// <summary>
    ///     The aspect ratio of the generated image.
    ///     Supported values depend on the model.
    /// </summary>
    public ChatImageAspectRatios? AspectRatio { get; set; }
    
    /// <summary>
    ///     The resolution of the generated image.
    ///     Supported by Gemini 3 Pro Image and Gemini 3.1 Flash Image ("512", "1K", "2K", "4K").
    /// </summary>
    public ChatImageResolutions? Resolution { get; set; }
}

/// <summary>
///     Represents an audio part of a chat message.
/// </summary>
public class ChatAudio : IChatAudio
{
    /// <summary>
    ///     Base64 encoded audio data.
    /// </summary>
    public string? Data { get; set; }

    /// <summary>
    ///  Publicly available URL where the audio is stored.
    /// </summary>
    [JsonProperty("audio_url")]
    public Uri? Url { get; set; }

    /// <summary>
    ///     MimeType of the audio.
    /// </summary>
    public string? MimeType { get; set; }
    
    /// <summary>
    ///     Format of the encoded audio data.
    /// </summary>
    public ChatAudioFormats? Format { get; set; }

    /// <summary>
    ///     Creates an empty audio instance.
    /// </summary>
    public ChatAudio()
    {
        
    }

    /// <summary>
    ///     Creates an audio instance from data and format.
    /// </summary>
    /// <param name="data">Base64 encoded audio data</param>
    /// <param name="format">Format of the audio</param>
    public ChatAudio(string data, ChatAudioFormats format)
    {
        Data = data;
        Format = format;
    }

    /// <summary>
    ///     Creates an audio instance from a publicly available URL
    /// </summary>
    /// <param name="uri">Publicly available URL</param>
    public ChatAudio(Uri uri)
    {
        Url = uri;
    }

    /// <summary>
    ///     Creates an audio instance from data and format.
    /// </summary>
    /// <param name="data">Base64 encoded audio data</param>
    /// <param name="format">Format of the audio</param>
    /// <param name="mimeType">Mime type</param>
    public ChatAudio(string data, ChatAudioFormats format, string mimeType)
    {
        Data = data;
        Format = format;
        MimeType = mimeType;
    }
}

public class ToolMetadata
{
    public List<ToolParamDefinition>? Params { get; set; }
    public List<string>? Ignore { get; set; }

    public ToolMetadata()
    {
        
    }
}

internal class ParsedToolCalls
{
    public List<FunctionCall> FunctionCalls { get; set; }
    public List<CustomToolCall> CustomToolCalls { get; set; }
}

public class ToolParamDefinition
{
    public string Name { get; set; }
    public IToolParamType Param { get; set; }

    public ToolParamDefinition(string name, IToolParamType param)
    {
        Name = name;
        Param = param;
    }
}

public class ToolCallsHandler
{
    /// <summary>
    /// Continues the conversation automatically.<br/>
    /// Note: if the request is set to return <see cref="ChatRequestResponseFormatTypes.StructuredJson"/>, there is no tool call generated, and hence the conversation won't be continued automatically.
    /// </summary>
    public static readonly ToolCallsHandler ContinueConversation = new ToolCallsHandler();
    
    private ToolCallsHandler()
    {
        
    }
}

/// <summary>
///     Represents a video part of a chat message.
/// </summary>
public class ChatVideo
{
    /// <summary>
    ///  Publicly available URL for the video
    /// </summary>
    [JsonProperty("video_url")]
    public Uri Url { get; set; }

    /// <summary>
    ///     Raw video location when a custom scheme is required (for example MiniMax <c>mm_file://{file_id}</c>).
    ///     When set, this value is serialized instead of <see cref="Url"/>.
    /// </summary>
    [JsonIgnore]
    public string? UrlOverride { get; set; }

    /// <summary>
    ///     Image/video detail hint. MiniMax-M3 accepts <c>low</c>, <c>default</c>, or <c>high</c>.
    /// </summary>
    [JsonIgnore]
    public ImageDetail? Detail { get; set; }

    /// <summary>
    ///     Frames per second used when sampling a video. MiniMax-M3 defaults to 1 and accepts 0.2–5.
    /// </summary>
    [JsonIgnore]
    public double? Fps { get; set; }

    /// <summary>
    ///     Longest side in pixels for MiniMax-M3 video/image preprocessing.
    /// </summary>
    [JsonIgnore]
    public int? MaxLongSidePixel { get; set; }

    /// <summary>
    ///     Creates a video url instance from the uri.
    /// </summary>
    /// <param name="url">Publicly available URL for the resource</param>
    public ChatVideo(Uri url)
    {
        Url = url;
    }

    /// <summary>
    ///     Creates a video instance from a URL string, including MiniMax <c>mm_file://</c> file references.
    /// </summary>
    public ChatVideo(string url)
    {
        UrlOverride = url;
        Url = Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) ? parsed : new Uri("https://invalid.local/");
    }
}

/// <summary>
///     Supported audio formats.
/// </summary>
public enum ChatAudioFormats
{
    /// <summary>
    /// Wavelet (input, conversion)
    /// </summary>
    Wav,
    /// <summary>
    /// MP3 (input, conversion)
    /// </summary>
    Mp3,
    /// <summary>
    /// L16/PCM (output only)
    /// </summary>
    L16
}

/// <summary>
/// Ways to link a document to a <see cref="ChatMessagePart"/>
/// </summary>
public enum DocumentLinkTypes
{
    /// <summary>
    /// Publicly reachable, absolute URL.
    /// </summary>
    Url,
    
    /// <summary>
    /// Base64 encoded document.
    /// </summary>
    Base64
}

/// <summary>
///     Represents a chat document (PDF).
/// </summary>
public class ChatDocument
{
    /// <summary>
    /// Base64 encoded data.
    /// </summary>
    public string? Base64 { get; set; }
    
    /// <summary>
    /// Publicly reachable URL serving the document.
    /// </summary>
    public Uri? Uri { get; set; }
    
    /// <summary>
    ///     Creates a new chat document
    /// </summary>
    /// <param name="base64">Base64 encoded data.</param>
    public ChatDocument(string base64)
    {
        Base64 = base64;
    }
    
    /// <summary>
    ///     Creates a new chat document
    /// </summary>
    /// <param name="uri">Publicly reachable URL serving the document.</param>
    public ChatDocument(Uri uri)
    {
        Uri = uri;
    }
}

/// <summary>
///     Represents a chat image
/// </summary>
public class ChatImage
{
    /// <summary>
    ///     Creates an empty chat image.
    /// </summary>
    public ChatImage()
    {
    }

    /// <summary>
    ///     Creates a new chat image
    /// </summary>
    /// <param name="content">Publicly available URL to the image or base64 encoded content</param>
    public ChatImage(string content)
    {
        Url = content;
    }

    /// <summary>
    ///     Creates a new chat image.
    /// </summary>
    /// <param name="content">Publicly available URL to the image or base64 encoded content</param>
    /// <param name="detail">The detail level to use, defaults to <see cref="ImageDetail.Auto" /></param>
    public ChatImage(string content, ImageDetail? detail)
    {
        Url = content;
        Detail = detail;
    }
    
    /// <summary>
    ///     Creates a new chat image.
    /// </summary>
    public ChatImage(string content, string mimeType)
    {
        Url = content;
        MimeType = mimeType;
    }
    
    /// <summary>
    ///     When using base64 encoding in <see cref="Url"/>, certain providers such as Google require <see cref="MimeType"/> to be set.
    ///     Values supported by Google: image/png, image/jpeg
    /// </summary>
    [JsonIgnore]
    public string? MimeType { get; set; }

    /// <summary>
    ///     Publicly available URL to the image or base64 encoded content
    /// </summary>
    [JsonProperty("url")]
    public string Url { get; set; }

    /// <summary>
    ///     Publicly available URL to the image or base64 encoded content
    /// </summary>
    [JsonProperty("detail")]
    [JsonConverter(typeof(StringEnumConverter))]
    public ImageDetail? Detail { get; set; }
}

/// <summary>
/// Known LLM providers.
/// </summary>
public enum LLmProviders
{
    /// <summary>
    /// Provider not resolved.
    /// </summary>
    Unknown,
    /// <summary>
    /// OpenAI.
    /// </summary>
    OpenAi,
    /// <summary>
    /// Anthropic.
    /// </summary>
    Anthropic,
    /// <summary>
    /// Azure OpenAI.
    /// </summary>
    AzureOpenAi,
    /// <summary>
    /// Cohere.
    /// </summary>
    Cohere,
    /// <summary>
    /// Ollama, vLLM, KoboldCpp and other (self-hosted) providers.
    /// </summary>
    Custom,
    /// <summary>
    /// Google.
    /// </summary>
    Google,
    /// <summary>
    /// Groq.
    /// </summary>
    Groq,
    /// <summary>
    /// DeepSeek.
    /// </summary>
    DeepSeek,
    /// <summary>
    /// Mistral.
    /// </summary>
    Mistral,
    /// <summary>
    /// xAI.
    /// </summary>
    XAi,
    /// <summary>
    /// Perplexity.
    /// </summary>
    Perplexity,
    /// <summary>
    /// Voyage.
    /// </summary>
    Voyage,
    /// <summary>
    /// DeepInfra.
    /// </summary>
    DeepInfra,
    /// <summary>
    /// Open Router.
    /// </summary>
    OpenRouter,
    /// <summary>
    /// Moonshot AI.
    /// </summary>
    MoonshotAi,
    /// <summary>
    /// ZAI.
    /// </summary>
    Zai,
    /// <summary>
    /// Blablador (Helmholtz).
    /// </summary>
    Blablador,
    /// <summary>
    /// Alibaba.
    /// </summary>
    Alibaba,
    /// <summary>
    /// Requesty.
    /// </summary>
    Requesty,
    /// <summary>
    /// Opper, an EU-hosted OpenAI-compatible gateway.
    /// </summary>
    Opper,
    /// <summary>
    /// Upstage.
    /// </summary>
    Upstage,
    /// <summary>
    /// MiniMax.
    /// </summary>
    MiniMax,
    /// <summary>
    /// LiteLLM AI gateway (OpenAI-compatible proxy for 100+ providers).
    /// </summary>
    LiteLlm,
    /// <summary>
    /// Internal value.
    /// </summary>
    Length
}

/// <summary>
/// Details about HTTP level error.
/// </summary>
public class TornadoHttpException
{
    /// <summary>
    ///     Network exception.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    ///     Status code received.
    /// </summary>
    public HttpStatusCode Code { get; set; }

    /// <summary>
    ///     Raw response from the endpoint.
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    /// Creates an exception from an http result.
    /// </summary>
    public TornadoHttpException(IHttpCallResult result)
    {
        CopyFromHttpCallResult(result);
    }
    
    /// <summary>
    /// Creates an exception from streaming request.
    /// </summary>
    public TornadoHttpException(TornadoStreamRequest result)
    {
        if (result.CallResponse is not null)
        {
            CopyFromHttpCallResult(result.CallResponse);
            return;
        }

        Exception = result.Exception;
    }
    
    void CopyFromHttpCallResult(IHttpCallResult result)
    {
        Exception = result.Exception;
        Code = result.Code;
        Response = result.Response;
    }
}

/// <summary>
/// Capability endpoints - each provider supports a subset of these.
/// </summary>
public enum CapabilityEndpoints
{
    /// <summary>
    /// Returns input url
    /// </summary>
    None,
    
    /// <summary>
    /// Special value returning the base url
    /// </summary>
    BaseUrl,
    
    /// <summary>
    /// Special value returning even shorter shared url prefix
    /// </summary>
    BaseUrlStripped,
    
    /// <summary>
    /// Chat endpoint.
    /// </summary>
    Chat,
    
    /// <summary>
    /// Moderation endpoint.
    /// </summary>
    Moderation,
    
    /// <summary>
    /// Legacy.
    /// </summary>
    Completions,
    
    /// <summary>
    /// Embeddings endpoint.
    /// </summary>
    Embeddings,
    
    /// <summary>
    /// Models endpoint.
    /// </summary>
    Models,
    
    /// <summary>
    /// Files endpoint.
    /// </summary>
    Files,
    
    /// <summary>
    /// Uploads endpoint.
    /// </summary>
    Uploads,
    
    /// <summary>
    /// Images endpoint.
    /// </summary>
    ImageGeneration,
    
    /// <summary>
    /// Audio endpoint.
    /// </summary>
    Audio,
    
    /// <summary>
    /// Assistants endpoint.
    /// </summary>
    Assistants,
    
    /// <summary>
    /// Image editing endpoint.
    /// </summary>
    ImageEdit,
    
    /// <summary>
    /// Threads endpoint.
    /// </summary>
    Threads,
    
    /// <summary>
    /// Fine tuning endpoint.
    /// </summary>
    FineTuning,
    
    /// <summary>
    /// Vector stores endpoint.
    /// </summary>
    VectorStores,
    
    /// <summary>
    /// Caching endpoint.
    /// </summary>
    Caching,
    
    /// <summary>
    /// Responses endpoint.
    /// </summary>
    Responses,
    
    /// <summary>
    /// Contextual embeddings endpoint.
    /// </summary>
    ContextualEmbeddings,
    
    /// <summary>
    /// Multimodal embeddings endpoint.
    /// </summary>
    MultimodalEmbeddings,
    
    /// <summary>
    /// Reranker endpoint.
    /// </summary>
    Rerank,
    
    /// <summary>
    /// Responses conversation endpoint.
    /// </summary>
    ResponsesConversation,
    
    /// <summary>
    /// Skills endpoint.
    /// </summary>
    Skills,
    
    /// <summary>
    /// Tokenize endpoint.
    /// </summary>
    Tokenize,
    
    /// <summary>
    /// Videos endpoint.
    /// </summary>
    Videos,
    
    /// <summary>
    /// Batch endpoint.
    /// </summary>
    Batch,
    
    /// <summary>
    /// Webhooks endpoint (Gemini project-level webhook registration).
    /// </summary>
    Webhooks,
    
    /// <summary>
    /// OCR endpoint.
    /// </summary>
    Ocr,

    /// <summary>
    /// Gemini Interactions API (models and managed agents).
    /// </summary>
    Interactions,

    /// <summary>
    /// Gemini Managed Agents API (saved agent configurations).
    /// </summary>
    ManagedAgents,

    /// <summary>
    /// Claude Managed Agents API (<c>/v1/agents</c>).
    /// </summary>
    AnthropicManagedAgents,

    /// <summary>
    /// Claude Managed Agent sessions API (<c>/v1/sessions</c>).
    /// </summary>
    AnthropicManagedAgentSessions,

    /// <summary>
    /// Claude Managed Agent environments API (<c>/v1/environments</c>).
    /// </summary>
    AnthropicManagedAgentEnvironments,

    /// <summary>
    /// Music generation endpoint.
    /// </summary>
    Music,
    /// <summary>
    /// Lyrics generation endpoint.
    /// </summary>
    Lyrics,

    /// <summary>
    /// OpenAI Realtime API (streaming STT/TTS/voice agents).
    /// </summary>
    Realtime,

    /// <summary>
    /// OpenAI Realtime API (streaming speech translation, transcription, voice agents).
    /// </summary>
    RealtimeTranslations,

    /// <summary>
    /// Gemini Live (<c>/live</c>) and OpenAI GPT-Live (<c>/live/sessions</c>).
    /// </summary>
    Live,

    /// <summary>
    /// Anthropic compaction endpoint (server-side context summarization via Messages API).
    /// </summary>
    Compaction,

    /// <summary>
    /// Anthropic Admin API rate limits endpoint.
    /// </summary>
    RateLimits
}

/// <summary>
/// Shared interface by all chat usages.
/// </summary>
public interface IChatUsage
{
    
}

/// <summary>
/// Represents authentication to a single provider.
/// </summary>
public class ProviderAuthentication
{
    /// <summary>
    /// The provider.
    /// </summary>
    public LLmProviders Provider { get; set; }
    
    /// <summary>
    /// API key, if any.
    /// </summary>
    public string? ApiKey { get; set; }
    
    /// <summary>
    /// Organization, if any.
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// Base URL for self-hosted / custom providers (e.g. Ollama "http://localhost:11434").
    /// Only used by <see cref="LLmProviders.Custom"/>.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Crates a new authentication.
    /// </summary>
    public ProviderAuthentication(LLmProviders provider, string apiKey, string? organization = null)
    {
        Provider = provider;
        ApiKey = apiKey;
        Organization = organization;
    }
    
    /// <summary>
    /// Creates a new authentication. This constructor can be used when creating authentication if context of an existing <see cref="IEndpointProvider"/> instance.
    /// </summary>
    public ProviderAuthentication(string apiKey, string? organization = null)
    {
        ApiKey = apiKey;
        Organization = organization;
    }
}

/// <summary>
/// Types of inbound streams.
/// </summary>
public enum StreamRequestTypes
{
    /// <summary>
    /// Unrecognized stream.
    /// </summary>
    Unknown,
    /// <summary>
    /// Chat/completion stream.
    /// </summary>
    Chat
}

internal interface IModelRequest
{
    internal IModel? RequestModel { get; }
}

internal class TornadoRequestContentWithProvider
{
    public IEndpointProvider Provider { get; set; }
    public TornadoRequestContent Request { get; set; }

    public TornadoRequestContentWithProvider(IEndpointProvider provider, TornadoRequestContent request)
    {
        Provider = provider;
        Request = request;
    }
}

/// <summary>
///  A Tornado HTTP request.
/// </summary>
public class TornadoRequestContent
{
    /// <summary>
    /// Content of the request.
    /// </summary>
    public object Body { get; set; }
    
    /// <summary>
    /// Model associated with this request.
    /// </summary>
    public IModel? Model { get; set; }
    
    /// <summary>
    /// Forces the URl to differ from the one inferred further down the pipeline.
    /// </summary>
    public string? Url { get; set; }
    
    /// <summary>
    /// The provider this request is outbound to.
    /// </summary>
    [JsonIgnore]
    public IEndpointProvider? Provider { get; set; }

    /// <summary>
    /// The endpoint this request corresponds to.
    /// </summary>
    [JsonIgnore]
    public CapabilityEndpoints? CapabilityEndpoint { get; set; }
    
    /// <summary>
    /// Headers are normally not set as they often contain secrets (API keys). For debugging, use <see cref="ChatRequestSerializeOptions.IncludeHeaders"/>
    /// </summary>
    public Dictionary<string, IEnumerable<string>>? Headers { get; set; }
    
    internal TornadoRequestContent(object body, IModel? model, string? url, IEndpointProvider provider, CapabilityEndpoints endpoint)
    {
        Body = body;
        Url = url;
        Provider = provider;
        CapabilityEndpoint = endpoint;
        Model = model;
    }

    /// <summary>
    /// This method can be used to see the final outbound url before executing the request.
    /// </summary>
    /// <returns></returns>
    public string? BuildFinalUrl()
    {
        if (Provider is null || CapabilityEndpoint is null)
        {
            return Url;
        }

        return EndpointBase.BuildRequestUrl(Url, Provider, CapabilityEndpoint.Value, Model);
    }
    
    internal static TornadoRequestContent Dummy => new TornadoRequestContent(new { }, null, null, new OpenAiEndpointProvider(LLmProviders.OpenAi), CapabilityEndpoints.Chat);

    internal TornadoRequestContent()
    {
        
    }

    /// <summary>
    /// Textual representation of the request - URL & Body.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Url: {Url}");

        if (Headers is not null)
        {
            sb.AppendLine("Headers:");
            sb.AppendLine("-------------------");

            foreach (KeyValuePair<string, IEnumerable<string>> header in Headers.OrderBy(x => x.Key, StringComparer.InvariantCulture))
            {
                sb.AppendLine($"{header.Key}: {string.Join(";", header.Value)}");
            }
        }

        sb.AppendLine("Body:");
        sb.AppendLine("-------------------");
        sb.AppendLine(Body.ToString());

        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Provides direct access to the arguments inferred by the model.
/// </summary>
public class ToolArguments
{
    [JsonIgnore] 
    internal readonly Lazy<ChatFunctionParamsGetter> ArgGetter;
    
    [JsonIgnore] 
    internal Dictionary<string, object?>? DecodedArguments;

    public ToolArguments()
    {
        ArgGetter = new Lazy<ChatFunctionParamsGetter>(() => new ChatFunctionParamsGetter(DecodedArguments));
    }
    
    /// <summary>
    /// The raw JSON.
    /// </summary>
    public string Data
    {
        get => _data;
        set
        {
            _data = value;
            DecodedArguments = Data.IsNullOrWhiteSpace() ? [] : JsonConvert.DeserializeObject<Dictionary<string, object?>>(Data);
        }
    }
    
    private string _data;
    
    /// <summary>
    ///     Gets all arguments passed to the function call as a dictionary.
    /// </summary>
    public Dictionary<string, object?> Arguments => ArgGetter.Value.Source ?? [];
    
    /// <summary>
    /// Gets the specified argument or default value.
    /// </summary>
    public T? GetOrDefault<T>(string param, T? defaultValue = default)
    {
        return Get(param, out T? data, out _) ? data : defaultValue;
    }

    /// <summary>
    /// Gets the specified argument. If the conversion to T fails, the exception is ignored.
    /// </summary>
    /// <param name="param">Key</param>
    /// <param name="data">Type to which the argument should be converted.</param>
    public bool Get<T>(string param, out T? data)
    {
        return Get(param, out data, out _);
    }
    
    /// <summary>
    /// Gets the specified argument. If the conversion to T fails, the exception is ignored.
    /// </summary>
    /// <param name="param">Key</param>
    /// <param name="data">Type to which the argument should be converted.</param>
    public bool TryGetArgument<T>(string param, [NotNullWhen(true)] out T? data)
    {
        return Get(param, out data, out _);
    }

    /// <summary>
    /// Gets the specified argument.
    /// </summary>
    /// <param name="param">Key</param>
    /// <param name="data">Type to which the argument should be converted.</param>
    /// <param name="exception">If the conversion fails, the exception is returned here.</param>
    public bool Get<T>(string param, out T? data, out Exception? exception)
    {
        return Clr.Get(param, Arguments, out data, out exception);
    }
}

internal class DelegateMetadata
{
    public ToolFunction ToolFunction { get; set; }
    public ToolDefinition Tool { get; set; }

    public DelegateMetadata(ToolFunction tf, ToolDefinition tool)
    {
        ToolFunction = tf;
        Tool = tool;
    }
}

/// <summary>
/// Options for serializing chat requests.
/// </summary>
public class ChatRequestSerializeOptions
{
    /// <summary>
    /// Instance with <see cref="Pretty"/> set to true.
    /// </summary>
    public static readonly ChatRequestSerializeOptions PresetPretty = new ChatRequestSerializeOptions
    {
        Pretty = true
    };
    
    /// <summary>
    /// Whether the request is streamed.
    /// </summary>
    public bool Stream { get; set; }

    /// <summary>
    /// Forces headers to be included. Warning: headers contain secrets, such as API keys.
    /// </summary>
    public bool IncludeHeaders { get; set;}
    
    /// <summary>
    /// Prettifies the request's body.
    /// </summary>
    public bool Pretty { get; set; }
}

/// <summary>
/// Options for serializing responses requests.
/// </summary>
public class ResponseRequestSerializeOptions
{
    /// <summary>
    /// Whether the request is streamed.
    /// </summary>
    public bool? Stream { get; set; }

    /// <summary>
    /// Forces headers to be included. Warning: headers contain secrets, such as API keys.
    /// </summary>
    public bool IncludeHeaders { get; set;}
    
    /// <summary>
    /// Prettifies the request's body.
    /// </summary>
    public bool Pretty { get; set; }
}

/// <summary>
/// Represents the counts of files in different processing states within a vector store.
/// </summary>
public class VectorStoreFileCountInfo
{
    /// <summary>
    /// The number of files that are currently being processed.
    /// </summary>
    [JsonProperty("in_progress")]
    public int InProgress { get; set; }

    /// <summary>
    /// The number of files that have been successfully processed.
    /// </summary>
    [JsonProperty("completed")]
    public int Completed { get; set; }

    /// <summary>
    /// The number of files that have failed to process.
    /// </summary>
    [JsonProperty("failed")]
    public int Failed { get; set; }

    /// <summary>
    /// The number of files that were cancelled.
    /// </summary>
    [JsonProperty("cancelled")]
    public int Cancelled { get; set; }

    /// <summary>
    /// The total number of files.
    /// </summary>
    [JsonProperty("total")]
    public int Total { get; set; }
}

/// <summary>
/// Tornado input file
/// </summary>
public class TornadoInputFile
{
    /// <summary>
    /// Base64 data
    /// </summary>
    public string? Base64 { get; set; }
    
    /// <summary>
    /// Mime type
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Creates an empty file
    /// </summary>
    public TornadoInputFile()
    {
        
    }

    /// <summary>
    /// Creates a file from base64 & mimetype
    /// </summary>
    public TornadoInputFile(string base64, string mimeType)
    {
        Base64 = base64;
        MimeType = mimeType;
    }
}

internal class TranscriptionSerializedRequest : IDisposable
{
    public readonly MultipartFormDataContent Content = new MultipartFormDataContent();
    public MemoryStream? Ms = null;
    public StreamContent? Sc = null;

    public void Dispose()
    {
        Content.Dispose();
        Ms?.Dispose();
        Sc?.Dispose();
    }
}

internal class AudioStreamEvent
{
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("delta")]
    public string? Delta { get; set; } 
    
    [JsonProperty("logprobs")]
    public List<TranscriptionLogprob>? Logprobs { get; set; }
    
    [JsonProperty("text")]
    public string? Text { get; set; }

    public static readonly Dictionary<string, AudioStreamEventTypes> Map = new Dictionary<string, AudioStreamEventTypes>(2)
    {
        { "transcript.text.delta", AudioStreamEventTypes.TranscriptDelta },
        { "transcript.text.done", AudioStreamEventTypes.TranscriptDone }
    };
}

internal enum AudioStreamEventTypes
{
    Unknown,
    TranscriptDelta,
    TranscriptDone
}

/// <summary>
/// Search result.
/// </summary>
public class ChatSearchResult
{
    /// <summary>
    /// The source URL or identifier for the content.
    /// </summary>
    [JsonProperty("source")]
    public string Source { get; set; }
    
    /// <summary>
    /// A descriptive title for the search result.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; set; }

    /// <summary>
    /// An array of text blocks containing the actual content.
    /// </summary>
    [JsonProperty("content")]
    public List<ChatSearchResultContent> Content { get; set; } = [];
    
    /// <summary>
    /// Citation configuration with <c>enabled</c> boolean field.
    /// </summary>
    [JsonProperty("citations")]
    public ChatSearchResultCitations? Citations { get; set; }
    
    /// <summary>
    /// Cache settings.
    /// </summary>
    [JsonProperty("cache_control")]
    public AnthropicCacheSettings? Cache { get; set; } 
}

/// <summary>
/// Citation configuration.
/// </summary>
public class ChatSearchResultCitations
{
    /// <summary>
    /// Whether citing from this source is enabled or not.
    /// </summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Instance with enabled citations.
    /// </summary>
    public static readonly ChatSearchResultCitations InstanceEnabled = new ChatSearchResultCitations
    {
        Enabled = true
    };
    
    /// <summary>
    /// Instance with disabled citations.
    /// </summary>
    public static readonly ChatSearchResultCitations InstanceDisabled = new ChatSearchResultCitations
    {
        Enabled = false
    };
}

/// <summary>
/// Search result content block.
/// </summary>
public abstract class ChatSearchResultContent
{
    /// <summary>
    /// Type of the block
    /// </summary>
    [JsonProperty("type")]
    public abstract ChatSearchResultContentTypes Type { get; set; }
}

/// <summary>
/// Text block.
/// </summary>
public class ChatSearchResultContentText : ChatSearchResultContent
{
    /// <inheritdoc />
    public override ChatSearchResultContentTypes Type { get; set; } = ChatSearchResultContentTypes.Text;
    
    /// <summary>
    /// Content.
    /// </summary>
    [JsonProperty("text")]
    public string Text { get; set; }

    /// <summary>
    /// Creates a new empty text block.
    /// </summary>
    public ChatSearchResultContentText()
    {
        
    }

    /// <summary>
    /// Creates a new text block with a given text.
    /// </summary>
    /// <param name="text"></param>
    public ChatSearchResultContentText(string text)
    {
        Text = text;
    }
}

/// <summary>
/// Interface for function call result rich blocks.
/// </summary>
[JsonConverter(typeof(FunctionResultBlockJsonConverter))]
public interface IFunctionResultBlock
{
    /// <summary>
    /// Type of the block
    /// </summary>
    public FunctionResultBlockTypes Type { get; }
}

/// <summary>
/// Text block.
/// </summary>
public class FunctionResultBlockText : IFunctionResultBlock
{
    /// <inheritdoc />
    public FunctionResultBlockTypes Type => FunctionResultBlockTypes.Text;
    
    /// <summary>
    /// JSON serialized output.
    /// </summary>
    public string Text { get; set; }
    
    /// <summary>
    /// Creates a new result block, encoding the given data to a JSON string. The data should be an object that can be encoded into JSON.
    /// </summary>
    public FunctionResultBlockText(object data)
    {
        Text = JsonConvert.SerializeObject(data);
    }
    
    /// <summary>
    /// Creates a new result block. The data won't be JSON encoded.
    /// </summary>
    public FunctionResultBlockText(string data)
    {
        Text = data;
    }
}

/// <summary>
/// Image block.
/// </summary>
public class FunctionResultBlockImage : IFunctionResultBlock
{
    /// <inheritdoc />
    public FunctionResultBlockTypes Type => FunctionResultBlockTypes.Image;
    
    /// <summary>
    /// Image source.
    /// </summary>
    [JsonProperty("source")]
    public IFunctionResultBlockImageSource Source { get; set; }

    /// <summary>
    /// Creates an empty image part.
    /// </summary>
    public FunctionResultBlockImage()
    {
        
    }

    /// <summary>
    /// Creates an image part with the given source.
    /// </summary>
    /// <param name="source"></param>
    public FunctionResultBlockImage(IFunctionResultBlockImageSource source)
    {
        Source = source;
    }
}

/// <summary>
/// Shared interface for image sources.
/// </summary>
public interface IFunctionResultBlockImageSource
{
    /// <summary>
    /// Type of the image.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; }
}

/// <summary>
/// Base64 image part.
/// </summary>
public class FunctionResultBlockImageSourceBase64 : IFunctionResultBlockImageSource
{
    /// <inheritdoc />
    public string Type => "base64";
    
    /// <summary>
    /// Base64 encoded image.
    /// </summary>
    [JsonProperty("data")]
    public string Data { get; set; }
    
    /// <summary>
    /// Display name for the image, used for referencing in multimodal responses.
    /// </summary>
    [JsonProperty("display_name")]
    public string? DisplayName { get; set; }
    
    /// <summary>
    /// image/jpeg, image/png, image/gif, image/webp 
    /// </summary>
    [JsonProperty("media_type")]
    public string MediaType { get; set; }
}

/// <summary>
/// Base64 image part.
/// </summary>
public class FunctionResultBlockImageSourceUrl : IFunctionResultBlockImageSource
{
    /// <inheritdoc />
    public string Type => "url";
    
    /// <summary>
    /// Publicly reachable url.
    /// </summary>
    [JsonProperty("url")]
    public string Url { get; set; }
    
    /// <summary>
    /// Display name for the image, used for referencing in multimodal responses.
    /// </summary>
    [JsonProperty("display_name")]
    public string? DisplayName { get; set; }
    
    /// <summary>
    /// MIME type of the image.
    /// </summary>
    [JsonProperty("mime_type")]
    public string? MimeType { get; set; }
}

/// <summary>
/// File image part.
/// </summary>
public class FunctionResultBlockImageSourceFile : IFunctionResultBlockImageSource
{
    /// <inheritdoc />
    public string Type => "file";
    
    /// <summary>
    /// Id of the file.
    /// </summary>
    [JsonProperty("file_id")]
    public string FileId { get; set; }
    
    /// <summary>
    /// Display name for the image, used for referencing in multimodal responses.
    /// </summary>
    [JsonProperty("display_name")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// MIME type of the image.
    /// </summary>
    [JsonProperty("mime_type")]
    public string? MimeType { get; set; }
}

/// <summary>
/// Image block.
/// </summary>
public class FunctionResultBlockSearchResult : IFunctionResultBlock
{
    /// <inheritdoc />
    public FunctionResultBlockTypes Type => FunctionResultBlockTypes.SearchResult;
    
    /// <summary>
    /// The source URL or identifier for the content.
    /// </summary>
    [JsonProperty("source")]
    public string Source { get; set; }
    
    /// <summary>
    /// A descriptive title for the search result.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; set; }

    /// <summary>
    /// An array of text blocks containing the actual content.
    /// </summary>
    [JsonProperty("content")]
    public List<ChatSearchResultContent> Content { get; set; } = [];
    
    /// <summary>
    /// Citation configuration with <c>enabled</c> boolean field.
    /// </summary>
    [JsonProperty("citations")]
    public ChatSearchResultCitations? Citations { get; set; }
}

/// <summary>
/// Types of function call result blocks.
/// </summary>
public enum FunctionResultBlockTypes
{
    /// <summary>
    /// Text block, supported by all providers.
    /// </summary>
    Text,
    /// <summary>
    /// Image block. Supported only by Anthropic.
    /// </summary>
    Image,
    /// <summary>
    /// Search result block. Supported only by Anthropic.
    /// </summary>
    SearchResult
}

/// <summary>
/// Types of search result content blocks.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ChatSearchResultContentTypes
{
    /// <summary>
    /// Text block.
    /// </summary>
    [EnumMember(Value = "text")] 
    Text
}

internal class FunctionResultBlockJsonConverter : JsonConverter<IFunctionResultBlock>
{
    public override void WriteJson(JsonWriter writer, IFunctionResultBlock? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("type");
        
        string typeStr = value.Type switch
        {
            FunctionResultBlockTypes.Text => "text",
            FunctionResultBlockTypes.Image => "image",
            FunctionResultBlockTypes.SearchResult => "search_result",
            _ => "unknown"
        };
        
        writer.WriteValue(typeStr);
        
        switch (value)
        {
            case FunctionResultBlockText textBlock:
            {
                writer.WritePropertyName("text");
                writer.WriteValue(textBlock.Text);
                break;
            }
            case FunctionResultBlockImage imageBlock:
            {
                writer.WritePropertyName("source");
                serializer.Serialize(writer, imageBlock.Source);
                
                break;
            }
            case FunctionResultBlockSearchResult searchResultBlock:
            {
                if (!string.IsNullOrEmpty(searchResultBlock.Source))
                {
                    writer.WritePropertyName("source");
                    writer.WriteValue(searchResultBlock.Source);
                }

                if (!string.IsNullOrEmpty(searchResultBlock.Title))
                {
                    writer.WritePropertyName("title");
                    writer.WriteValue(searchResultBlock.Title);
                }

                if (searchResultBlock.Content is { Count: > 0 })
                {
                    writer.WritePropertyName("content");
                    serializer.Serialize(writer, searchResultBlock.Content);
                }

                if (searchResultBlock.Citations != null)
                {
                    writer.WritePropertyName("citations");
                    serializer.Serialize(writer, searchResultBlock.Citations);
                }
                break;
            }
        }

        writer.WriteEndObject();
    }

    public override IFunctionResultBlock? ReadJson(JsonReader reader, Type objectType, IFunctionResultBlock? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        JObject obj = JObject.Load(reader);
        string? type = obj["type"]?.Value<string>();

        switch (type)
        {
            case "text":
                string txt = obj["text"]?.Value<string>() ?? string.Empty;
                return new FunctionResultBlockText(txt);

            case "image":
            {
                JObject? srcToken = obj["source"] as JObject;
                IFunctionResultBlockImageSource? src = null;

                if (srcToken is not null)
                {
                    string? srcType = srcToken["type"]?.Value<string>();
                    src = srcType switch
                    {
                        "base64" => new FunctionResultBlockImageSourceBase64
                        {
                            Data = srcToken["data"]?.Value<string>()!,
                            MediaType = srcToken["media_type"]?.Value<string>()!
                        },
                        "url" => new FunctionResultBlockImageSourceUrl
                        {
                            Url = srcToken["url"]?.Value<string>()!
                        },
                        "file" => new FunctionResultBlockImageSourceFile
                        {
                            FileId = srcToken["file_id"]?.Value<string>()!
                        },
                        _ => src
                    };
                }

                return src is not null ? new FunctionResultBlockImage(src) : new FunctionResultBlockImage();
            }
            case "search_result":
            {
                FunctionResultBlockSearchResult sr = new FunctionResultBlockSearchResult
                {
                    Source = obj["source"]?.Value<string>(),
                    Title = obj["title"]?.Value<string>()
                };

                if (obj["content"] is JArray contentArr)
                {
                    sr.Content = contentArr.ToObject<List<ChatSearchResultContent>>(serializer) ?? [];
                }

                if (obj["citations"] is JObject citationsObj)
                {
                    sr.Citations = citationsObj.ToObject<ChatSearchResultCitations>(serializer);
                }

                return sr;
            }
            default:
            {
                return new FunctionResultBlockText(obj.ToString(Formatting.None));
            }
        }
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;
}

public static class ResponseOutputTypes
{
    public const string Reasoning = "reasoning";
    public const string FunctionCall = "function_call";
    public const string ToolSearchCall = "tool_search_call";
    public const string ToolSearchOutput = "tool_search_output";
}

internal class IModelConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(IModel).IsAssignableFrom(objectType);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is IModel model)
        {
            writer.WriteValue(model.Name);
        }
        else
        {
            writer.WriteNull();
        }
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        return existingValue;
    }

    public override bool CanRead => false;
}

/// <summary>
/// Shared interface for serializable requests.
/// </summary>
public interface ISerializableRequest
{
    /// <summary>
    /// Serializes the request.
    /// </summary>
    public TornadoRequestContent Serialize(IEndpointProvider provider);
    
    /// <summary>
    /// Serializes the request with options.
    /// </summary>
    public TornadoRequestContent Serialize(IEndpointProvider provider, RequestSerializeOptions options);
}

/// <summary>
/// Options for serializing requests.
/// </summary>
public class RequestSerializeOptions
{
    /// <summary>
    /// Should the JSON be prettified.
    /// </summary>
    public bool Pretty { get; set; }
}

public class SchemaIgnoreAttribute : Attribute
{
    /// <summary>
    /// Indicates that the property should be ignored during serialization.
    /// </summary>
    public SchemaIgnoreAttribute()
    {
    }
}
