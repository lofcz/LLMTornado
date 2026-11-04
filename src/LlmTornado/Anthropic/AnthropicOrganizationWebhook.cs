using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.Anthropic;

/// <summary>A newly generated webhook signing secret; returned only once.</summary>
public class AnthropicWebhookSigningSecret
{
    /// <summary>The whsec_ secret used to verify webhook deliveries.</summary>
    [JsonProperty("signing_secret")]
    public string? SigningSecret { get; set; }
}

/// <summary>Webhook endpoint; signing_secret is returned only on creation or regeneration.</summary>
public class AnthropicOrganizationWebhook
{
    /// <summary>id.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }

    /// <summary>url.</summary>
    [JsonProperty("url")]
    public string? Url { get; set; }

    /// <summary>name.</summary>
    [JsonProperty("name")]
    public string? Name { get; set; }

    /// <summary>description.</summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>enabled events.</summary>
    [JsonProperty("enabled_events")]
    public List<string>? EnabledEvents { get; set; }

    /// <summary>state.</summary>
    [JsonProperty("state")]
    public string? State { get; set; }

    /// <summary>disabled reason.</summary>
    [JsonProperty("disabled_reason")]
    public string? DisabledReason { get; set; }

    /// <summary>created at.</summary>
    [JsonProperty("created_at")]
    public string? CreatedAt { get; set; }

    /// <summary>updated at.</summary>
    [JsonProperty("updated_at")]
    public string? UpdatedAt { get; set; }

    /// <summary>signing secret.</summary>
    [JsonProperty("signing_secret")]
    public string? SigningSecret { get; set; }
}
/// <summary>Registers a public HTTPS endpoint for profile enforcement events.</summary>
public class AnthropicOrganizationWebhookCreateRequest
{
    /// <summary>url.</summary>
    [JsonProperty("url")]
    public string? Url { get; set; }

    /// <summary>name.</summary>
    [JsonProperty("name")]
    public string? Name { get; set; }

    /// <summary>description.</summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>At least one supported enforcement event is required.</summary>
    [JsonProperty("enabled_events")]
    public List<string>? EnabledEvents { get; set; }
}
/// <summary>Updates supplied fields. Empty enabled_events preserves subscriptions; state=disabled stops delivery.</summary>
public class AnthropicOrganizationWebhookUpdateRequest : AnthropicOrganizationWebhookCreateRequest
{
    /// <summary>enabled or disabled.</summary>
    [JsonProperty("state")]
    public string? State { get; set; }
}
/// <summary>Webhook pagination uses after_id and before_id, rather than page.</summary>
public class AnthropicOrganizationWebhookListQuery
{
    /// <summary>after id.</summary>
    [JsonProperty("after_id")]
    public string? AfterId { get; set; }

    /// <summary>before id.</summary>
    [JsonProperty("before_id")]
    public string? BeforeId { get; set; }

    /// <summary>limit.</summary>
    [JsonProperty("limit")]
    public int? Limit { get; set; }
}
/// <summary>A webhook list with cursor IDs.</summary>
public class AnthropicOrganizationWebhookList
{
    /// <summary>data.</summary>
    [JsonProperty("data")]
    public List<AnthropicOrganizationWebhook> Data { get; set; } = [];

    /// <summary>has more.</summary>
    [JsonProperty("has_more")]
    public bool HasMore { get; set; }

    /// <summary>first id.</summary>
    [JsonProperty("first_id")]
    public string? FirstId { get; set; }

    /// <summary>last id.</summary>
    [JsonProperty("last_id")]
    public string? LastId { get; set; }
}
/// <summary>Enforcement notification. Retrieve the current profile; events may arrive late or out of order.</summary>
public class AnthropicOrganizationWebhookEvent
{
    /// <summary>type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>id.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }

    /// <summary>created at.</summary>
    [JsonProperty("created_at")]
    public string? CreatedAt { get; set; }

    /// <summary>data.</summary>
    [JsonProperty("data")]
    public AnthropicOrganizationWebhookEventData? Data { get; set; }
}
/// <summary>IDs identifying the changed profile and enforcement action.</summary>
public class AnthropicOrganizationWebhookEventData
{
    /// <summary>type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>The user-profile ID, not the webhook endpoint ID.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }

    /// <summary>enforcement action id.</summary>
    [JsonProperty("enforcement_action_id")]
    public string? EnforcementActionId { get; set; }

    /// <summary>organization id.</summary>
    [JsonProperty("organization_id")]
    public string? OrganizationId { get; set; }
}
/// <summary>Supported organization webhook event types.</summary>
public static class AnthropicOrganizationWebhookEventTypes
{
    /// <summary>A restriction or ban was opened.</summary>
    public const string EnforcementActionOpened = "user_profile.enforcement_action_opened";
    /// <summary>A restriction or ban ended.</summary>
    public const string EnforcementActionResolved = "user_profile.enforcement_action_resolved";
}
