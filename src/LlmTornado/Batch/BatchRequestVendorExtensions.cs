using System.Collections.Generic;
using LlmTornado.Webhooks;
using Newtonsoft.Json;

namespace LlmTornado.Batch;

/// <summary>
/// Vendor-specific extensions for batch requests.
/// </summary>
public class BatchRequestVendorExtensions
{
    /// <summary>Anthropic batch-wide request attribution.</summary>
    public BatchRequestVendorAnthropicExtensions? Anthropic { get; set; }

    /// <summary>
    /// OpenAI-specific extensions.
    /// </summary>
    public BatchRequestVendorOpenAiExtensions? OpenAi { get; set; }
    
    /// <summary>
    /// Google/Gemini-specific extensions.
    /// </summary>
    public BatchRequestVendorGoogleExtensions? Google { get; set; }
}

/// <summary>Anthropic batch options; a profile applies to every item in the batch.</summary>
public class BatchRequestVendorAnthropicExtensions
{
    /// <summary>End-customer profile ID, sent as an HTTP header.</summary>
    public string? UserProfileId { get; set; }
}

/// <summary>
/// OpenAI-specific batch request extensions.
/// </summary>
public class BatchRequestVendorOpenAiExtensions
{
    /// <summary>
    /// Set of key-value pairs that can be attached to the batch.
    /// Keys are strings with a maximum length of 64 characters.
    /// Values are strings with a maximum length of 512 characters.
    /// </summary>
    [JsonProperty("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Google/Gemini-specific batch request extensions.
/// </summary>
public class BatchRequestVendorGoogleExtensions
{
    /// <summary>
    /// User-defined display name for the batch job.
    /// If not provided, a unique name will be generated automatically.
    /// </summary>
    public string? DisplayName { get; set; }
    
    /// <summary>
    /// Priority of the batch job. Higher values mean higher priority.
    /// Batches with higher priority values will be processed before 
    /// batches with lower priority values. Negative values are allowed.
    /// Default is 0.
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Per-request dynamic webhook configuration. When set, Gemini POSTs completion events to the given URIs instead of requiring polling.
    /// </summary>
    public GeminiWebhookConfig? WebhookConfig { get; set; }
}
