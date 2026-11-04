using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Anthropic;

/// <summary>Native error envelope, available in HttpCallResult.Response on unsuccessful calls.</summary>
public class AnthropicErrorResponse
{
    /// <summary>Envelope discriminator.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }
    /// <summary>Provider request ID for diagnostics.</summary>
    [JsonProperty("request_id")]
    public string? RequestId { get; set; }
    /// <summary>Native error and structured recovery details.</summary>
    [JsonProperty("error")]
    public AnthropicApiError? Error { get; set; }

    /// <summary>Parses the raw response returned by an unsuccessful native API call.</summary>
    public static AnthropicErrorResponse? Parse(string json) => JsonConvert.DeserializeObject<AnthropicErrorResponse>(json);
}

/// <summary>Provider error. Only explicit thread recovery codes should trigger conversation replay.</summary>
public class AnthropicApiError
{
    /// <summary>Native error type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }
    /// <summary>Human-readable explanation.</summary>
    [JsonProperty("message")]
    public string? Message { get; set; }
    /// <summary>Structured recovery information, when provided.</summary>
    [JsonProperty("details")]
    public AnthropicApiErrorDetails? Details { get; set; }
}

/// <summary>Native thread and resource error details; preserves unrecognized fields.</summary>
public class AnthropicApiErrorDetails
{
    /// <summary>For example thread_not_found, thread_already_continued or thread_continue_in_flight.</summary>
    [JsonProperty("error_code")]
    public string? ErrorCode { get; set; }
    /// <summary>Features incompatible with a message thread.</summary>
    [JsonProperty("unsupported")]
    public List<string>? Unsupported { get; set; }
    /// <summary>Channel created before a Claude Tag setup request's settings were rejected.</summary>
    [JsonProperty("slack_channel_id")]
    public string? SlackChannelId { get; set; }
    /// <summary>Other provider-specific error details.</summary>
    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }
}
