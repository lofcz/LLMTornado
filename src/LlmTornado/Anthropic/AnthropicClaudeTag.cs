using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.Anthropic;

/// <summary>A channel or workspace with stored or inherited settings and their sources.</summary>
public class AnthropicClaudeTagResource
{
    /// <summary>type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>id.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }

    /// <summary>slack.</summary>
    [JsonProperty("slack")]
    public AnthropicClaudeTagSlackIds? Slack { get; set; }

    /// <summary>settings.</summary>
    [JsonProperty("settings")]
    public AnthropicClaudeTagSettings? Settings { get; set; }

    /// <summary>sources.</summary>
    [JsonProperty("sources")]
    public AnthropicClaudeTagSources? Sources { get; set; }

    /// <summary>created at.</summary>
    [JsonProperty("created_at")]
    public string? CreatedAt { get; set; }

    /// <summary>updated at.</summary>
    [JsonProperty("updated_at")]
    public string? UpdatedAt { get; set; }
}
/// <summary>Slack's own IDs, distinct from Claude Tag resource IDs.</summary>
public class AnthropicClaudeTagSlackIds
{
    /// <summary>workspace id.</summary>
    [JsonProperty("workspace_id")]
    public string? WorkspaceId { get; set; }

    /// <summary>channel id.</summary>
    [JsonProperty("channel_id")]
    public string? ChannelId { get; set; }
}
/// <summary>Stored settings; null means unset. With include_inherited, settings reflect their effective values.</summary>
public class AnthropicClaudeTagSettings
{
    /// <summary>enabled.</summary>
    [JsonProperty("enabled")]
    public bool? Enabled { get; set; }

    /// <summary>instructions.</summary>
    [JsonProperty("instructions")]
    public string? Instructions { get; set; }

    /// <summary>model.</summary>
    [JsonProperty("model")]
    public string? Model { get; set; }

    /// <summary>environment id.</summary>
    [JsonProperty("environment_id")]
    public string? EnvironmentId { get; set; }

    /// <summary>guest policy.</summary>
    [JsonProperty("guest_policy")]
    public AnthropicClaudeTagGuestPolicy? GuestPolicy { get; set; }
}
/// <summary>Guest access policy: allow, restrict, or channel_only.</summary>
public class AnthropicClaudeTagGuestPolicy
{
    /// <summary>type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }
}
/// <summary>Level providing a setting. Preserve source order for additive instructions.</summary>
public class AnthropicClaudeTagSource
{
    /// <summary>type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>slack channel id.</summary>
    [JsonProperty("slack_channel_id")]
    public string? SlackChannelId { get; set; }

    /// <summary>slack workspace id.</summary>
    [JsonProperty("slack_workspace_id")]
    public string? SlackWorkspaceId { get; set; }

    /// <summary>slack.</summary>
    [JsonProperty("slack")]
    public AnthropicClaudeTagSlackIds? Slack { get; set; }
}
/// <summary>Per-setting provenance; instruction sources are ordered from broadest to nearest.</summary>
public class AnthropicClaudeTagSources
{
    /// <summary>enabled.</summary>
    [JsonProperty("enabled")]
    public AnthropicClaudeTagSource? Enabled { get; set; }

    /// <summary>instructions.</summary>
    [JsonProperty("instructions")]
    public List<AnthropicClaudeTagSource>? Instructions { get; set; }

    /// <summary>model.</summary>
    [JsonProperty("model")]
    public AnthropicClaudeTagSource? Model { get; set; }

    /// <summary>environment id.</summary>
    [JsonProperty("environment_id")]
    public AnthropicClaudeTagSource? EnvironmentId { get; set; }

    /// <summary>guest policy.</summary>
    [JsonProperty("guest_policy")]
    public AnthropicClaudeTagSource? GuestPolicy { get; set; }
}
/// <summary>List filters. SlackWorkspaceId is required when listing channels.</summary>
public class AnthropicClaudeTagListQuery : AnthropicPageQuery
{
    /// <summary>Slack's own T/E ID, not a slws_ resource ID.</summary>
    [JsonProperty("slack_workspace_id")]
    public string? SlackWorkspaceId { get; set; }

    /// <summary>Slack's own C/G ID, not a slch_ resource ID.</summary>
    [JsonProperty("slack_channel_id")]
    public string? SlackChannelId { get; set; }

    /// <summary>include inherited.</summary>
    [JsonProperty("include_inherited")]
    public bool? IncludeInherited { get; set; }
}
/// <summary>Sets up a channel; optional settings update the stored configuration.</summary>
public class AnthropicClaudeTagSetupRequest
{
    /// <summary>Required Slack workspace and channel IDs.</summary>
    [JsonProperty("slack")]
    public AnthropicClaudeTagSlackIds Slack { get; set; } = new();

    /// <summary>settings.</summary>
    [JsonProperty("settings")]
    public AnthropicClaudeTagSettingsUpdate? Settings { get; set; }
}
/// <summary>An Access bundle's public metadata; never includes the access or credentials it grants.</summary>
public class AnthropicClaudeTagBundle
{
    /// <summary>type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>id.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }

    /// <summary>display name.</summary>
    [JsonProperty("display_name")]
    public string? DisplayName { get; set; }

    /// <summary>description.</summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>created at.</summary>
    [JsonProperty("created_at")]
    public string? CreatedAt { get; set; }

    /// <summary>updated at.</summary>
    [JsonProperty("updated_at")]
    public string? UpdatedAt { get; set; }
}
/// <summary>Exactly one target: slack_channel with SlackChannelId, or slack_workspace with SlackWorkspaceId.</summary>
public class AnthropicClaudeTagTargetRequest
{
    /// <summary>type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>slack channel id.</summary>
    [JsonProperty("slack_channel_id")]
    public string? SlackChannelId { get; set; }

    /// <summary>slack workspace id.</summary>
    [JsonProperty("slack_workspace_id")]
    public string? SlackWorkspaceId { get; set; }
}
/// <summary>A bundle attachment. Its btgt_ ID is distinct from its channel/workspace ID.</summary>
public class AnthropicClaudeTagTarget : AnthropicClaudeTagTargetRequest
{
    /// <summary>id.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }

    /// <summary>created at.</summary>
    [JsonProperty("created_at")]
    public string? CreatedAt { get; set; }
}
/// <summary>Patch semantics: unassigned properties are omitted; explicitly assigning null restores inheritance.</summary>
public class AnthropicClaudeTagSettingsUpdate
{
    private bool? enabled;
    private bool enabledSet;
    /// <summary>Sets enabled, or clears it when explicitly assigned null.</summary>
    [JsonProperty("enabled", NullValueHandling = NullValueHandling.Include)]
    public bool? Enabled { get => enabled; set { enabled = value; enabledSet = true; } }
    /// <summary>Whether the patch assigns this setting.</summary>
    public bool ShouldSerializeEnabled() => enabledSet;

    private string? instructions;
    private bool instructionsSet;
    /// <summary>Sets instructions, or clears it when explicitly assigned null.</summary>
    [JsonProperty("instructions", NullValueHandling = NullValueHandling.Include)]
    public string? Instructions { get => instructions; set { instructions = value; instructionsSet = true; } }
    /// <summary>Whether the patch assigns this setting.</summary>
    public bool ShouldSerializeInstructions() => instructionsSet;

    private string? model;
    private bool modelSet;
    /// <summary>Sets model, or clears it when explicitly assigned null.</summary>
    [JsonProperty("model", NullValueHandling = NullValueHandling.Include)]
    public string? Model { get => model; set { model = value; modelSet = true; } }
    /// <summary>Whether the patch assigns this setting.</summary>
    public bool ShouldSerializeModel() => modelSet;

    private string? environmentId;
    private bool environmentIdSet;
    /// <summary>Sets environment_id, or clears it when explicitly assigned null.</summary>
    [JsonProperty("environment_id", NullValueHandling = NullValueHandling.Include)]
    public string? EnvironmentId { get => environmentId; set { environmentId = value; environmentIdSet = true; } }
    /// <summary>Whether the patch assigns this setting.</summary>
    public bool ShouldSerializeEnvironmentId() => environmentIdSet;

    private AnthropicClaudeTagGuestPolicy? guestPolicy;
    private bool guestPolicySet;
    /// <summary>Sets guest_policy, or clears it when explicitly assigned null.</summary>
    [JsonProperty("guest_policy", NullValueHandling = NullValueHandling.Include)]
    public AnthropicClaudeTagGuestPolicy? GuestPolicy { get => guestPolicy; set { guestPolicy = value; guestPolicySet = true; } }
    /// <summary>Whether the patch assigns this setting.</summary>
    public bool ShouldSerializeGuestPolicy() => guestPolicySet;

}
