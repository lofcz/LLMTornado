using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.Decision.Vendors.OpenRouter;

/// <summary>OpenRouter-specific fields for the alpha Decisions endpoint.</summary>
public class DecisionRequestVendorOpenRouterExtensions
{
    /// <summary>Provider routing preferences, including ZDR restrictions.</summary>
    [JsonProperty("provider", NullValueHandling = NullValueHandling.Ignore)]
    public DecisionOpenRouterProviderPreferences? Provider { get; set; }

    /// <summary>Observability grouping identifier, with a maximum of 256 characters.</summary>
    [JsonProperty("session_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? SessionId { get; set; }

    /// <summary>Trace identifiers and custom observability metadata.</summary>
    [JsonProperty("trace", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, object?>? Trace { get; set; }

    /// <summary>User identifier, with a maximum of 256 characters.</summary>
    [JsonProperty("user", NullValueHandling = NullValueHandling.Ignore)]
    public string? User { get; set; }
}

/// <summary>OpenRouter provider routing preferences. Unset fields use the server and account defaults.</summary>
public class DecisionOpenRouterProviderPreferences
{
    /// <summary>Restrict requests to zero-data-retention endpoints. The SDK does not relax this setting.</summary>
    [JsonProperty("zdr", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Zdr { get; set; }

    /// <summary>Allow alternate providers that meet the request restrictions.</summary>
    [JsonProperty("allow_fallbacks", NullValueHandling = NullValueHandling.Ignore)]
    public bool? AllowFallbacks { get; set; }

    /// <summary>Data collection policy: allow or deny.</summary>
    [JsonProperty("data_collection", NullValueHandling = NullValueHandling.Ignore)]
    public string? DataCollection { get; set; }

    /// <summary>Ordered provider slugs.</summary>
    [JsonProperty("order", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Order { get; set; }

    /// <summary>Allowed provider slugs.</summary>
    [JsonProperty("only", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Only { get; set; }

    /// <summary>Provider slugs to exclude.</summary>
    [JsonProperty("ignore", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Ignore { get; set; }

    /// <summary>Require support for the supplied request parameters.</summary>
    [JsonProperty("require_parameters", NullValueHandling = NullValueHandling.Ignore)]
    public bool? RequireParameters { get; set; }

    /// <summary>Restrict requests to models that permit text distillation.</summary>
    [JsonProperty("enforce_distillable_text", NullValueHandling = NullValueHandling.Ignore)]
    public bool? EnforceDistillableText { get; set; }

    /// <summary>Maximum prices, with string values in the units documented by OpenRouter.</summary>
    [JsonProperty("max_price", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string>? MaxPrice { get; set; }

    /// <summary>Provider-specific request options.</summary>
    [JsonProperty("options", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, object?>? Options { get; set; }

    /// <summary>Preferred latency limit, as a number or percentile configuration.</summary>
    [JsonProperty("preferred_max_latency", NullValueHandling = NullValueHandling.Ignore)]
    public object? PreferredMaxLatency { get; set; }

    /// <summary>Preferred minimum throughput, as a number or percentile configuration.</summary>
    [JsonProperty("preferred_min_throughput", NullValueHandling = NullValueHandling.Ignore)]
    public object? PreferredMinThroughput { get; set; }

    /// <summary>Allowed quantization identifiers.</summary>
    [JsonProperty("quantizations", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Quantizations { get; set; }

    /// <summary>Provider sorting strategy, as a string or structured configuration.</summary>
    [JsonProperty("sort", NullValueHandling = NullValueHandling.Ignore)]
    public object? Sort { get; set; }
}
