using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.Anthropic;

/// <summary>End-customer identity and current safety enforcement status.</summary>
public class AnthropicUserProfile
{
    /// <summary>type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>id.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }

    /// <summary>name.</summary>
    [JsonProperty("name")]
    public string? Name { get; set; }

    /// <summary>access type.</summary>
    [JsonProperty("access_type")]
    public string? AccessType { get; set; }

    /// <summary>external user details.</summary>
    [JsonProperty("external_user_details")]
    public AnthropicExternalUserDetails? ExternalUserDetails { get; set; }

    /// <summary>metadata.</summary>
    [JsonProperty("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>programs.</summary>
    [JsonProperty("programs")]
    public Dictionary<string, AnthropicProfileProgram?>? Programs { get; set; }

    /// <summary>Current status: unrestricted, pending, restricted or banned. Unknown statuses must be treated as restricted.</summary>
    [JsonProperty("enforcement_status")]
    public string? EnforcementStatus { get; set; }

    /// <summary>Current open actions. Resolved actions are no longer listed.</summary>
    [JsonProperty("enforcement_actions")]
    public List<AnthropicEnforcementAction>? EnforcementActions { get; set; }

    /// <summary>created at.</summary>
    [JsonProperty("created_at")]
    public string? CreatedAt { get; set; }

    /// <summary>updated at.</summary>
    [JsonProperty("updated_at")]
    public string? UpdatedAt { get; set; }
}
/// <summary>Caller-supplied identity details; omit unknown values. Existing values cannot be cleared.</summary>
public class AnthropicExternalUserDetails
{
    /// <summary>reference id.</summary>
    [JsonProperty("reference_id")]
    public string? ReferenceId { get; set; }

    /// <summary>onboarded at.</summary>
    [JsonProperty("onboarded_at")]
    public string? OnboardedAt { get; set; }

    /// <summary>account status.</summary>
    [JsonProperty("account_status")]
    public string? AccountStatus { get; set; }

    /// <summary>entity type.</summary>
    [JsonProperty("entity_type")]
    public string? EntityType { get; set; }

    /// <summary>country.</summary>
    [JsonProperty("country")]
    public string? Country { get; set; }

    /// <summary>name hash.</summary>
    [JsonProperty("name_hash")]
    public string? NameHash { get; set; }

    /// <summary>email hash.</summary>
    [JsonProperty("email_hash")]
    public string? EmailHash { get; set; }
}
/// <summary>An open restriction or ban; read the profile again when a webhook arrives.</summary>
public class AnthropicEnforcementAction
{
    /// <summary>type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>id.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }

    /// <summary>action.</summary>
    [JsonProperty("action")]
    public string? Action { get; set; }

    /// <summary>reason.</summary>
    [JsonProperty("reason")]
    public string? Reason { get; set; }

    /// <summary>opened at.</summary>
    [JsonProperty("opened_at")]
    public string? OpenedAt { get; set; }

    /// <summary>expires at.</summary>
    [JsonProperty("expires_at")]
    public string? ExpiresAt { get; set; }
}
/// <summary>Program eligibility, requirements, tiers and features.</summary>
public class AnthropicProfileProgram
{
    /// <summary>status.</summary>
    [JsonProperty("status")]
    public string? Status { get; set; }

    /// <summary>requirements.</summary>
    [JsonProperty("requirements")]
    public Dictionary<string, bool>? Requirements { get; set; }

    /// <summary>tiers.</summary>
    [JsonProperty("tiers")]
    public Dictionary<string, AnthropicProfileProgram>? Tiers { get; set; }

    /// <summary>features.</summary>
    [JsonProperty("features")]
    public Dictionary<string, AnthropicProfileProgram>? Features { get; set; }
}
/// <summary>Registers a new end customer. Creation is not idempotent, including when reference_id matches.</summary>
public class AnthropicUserProfileCreateRequest
{
    /// <summary>name.</summary>
    [JsonProperty("name")]
    public string? Name { get; set; }

    /// <summary>application for an end user; passthrough for a company buying API access.</summary>
    [JsonProperty("access_type")]
    public string? AccessType { get; set; }

    /// <summary>external user details.</summary>
    [JsonProperty("external_user_details")]
    public AnthropicExternalUserDetails? ExternalUserDetails { get; set; }

    /// <summary>metadata.</summary>
    [JsonProperty("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}
/// <summary>Filters and ordering for user profiles.</summary>
public class AnthropicUserProfileListQuery : AnthropicPageQuery
{
    /// <summary>Match any listed enforcement status. Continue through empty pages while next_page is present.</summary>
    [JsonProperty("enforcement_status")]
    public List<string>? EnforcementStatus { get; set; }

    /// <summary>order.</summary>
    [JsonProperty("order")]
    public string? Order { get; set; }

    /// <summary>order by.</summary>
    [JsonProperty("order_by")]
    public string? OrderBy { get; set; }
}
/// <summary>Updates only supplied fields; metadata merges, and an empty metadata value removes that key.</summary>
public class AnthropicUserProfileUpdateRequest : AnthropicUserProfileCreateRequest
{
    /// <summary>Explicitly clear the profile name; false leaves an omitted name unchanged.</summary>
    [JsonIgnore]
    public bool ClearName { get; set; }

    [JsonProperty("name", NullValueHandling = NullValueHandling.Include)]
    public new string? Name { get => ClearName ? null : base.Name; set => base.Name = value; }
    /// <summary>Includes the name only when assigned or explicitly cleared.</summary>
    public bool ShouldSerializeName() => ClearName || Name is not null;
}
