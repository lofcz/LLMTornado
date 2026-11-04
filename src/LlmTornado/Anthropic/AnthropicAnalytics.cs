using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Anthropic;

/// <summary>Usage and cost filters. Array filters use repeated bracket parameters; empty API-key IDs select unattributed requests.</summary>
public class AnthropicAnalyticsQuery : AnthropicPageQuery
{
    /// <summary>Required inclusive RFC 3339 start.</summary>
    [JsonProperty("starting_at")]
    public string? StartingAt { get; set; }

    /// <summary>Exclusive end. Required with time buckets; at most 31 days after the start.</summary>
    [JsonProperty("ending_at")]
    public string? EndingAt { get; set; }

    /// <summary>1m, 1h or 1d. Minute buckets cover at most 24 hours.</summary>
    [JsonProperty("time_bucket_width")]
    public string? TimeBucketWidth { get; set; }

    /// <summary>Up to five dimensions; cost_type and token_type on cost do not count toward that limit.</summary>
    [JsonProperty("group_by")]
    public List<string>? GroupBy { get; set; }

    /// <summary>asc or desc.</summary>
    [JsonProperty("order")]
    public string? Order { get; set; }

    /// <summary>Usage: total_tokens, output_tokens, uncached_input_tokens, requests, time. Cost: amount, list_amount, time.</summary>
    [JsonProperty("order_by")]
    public string? OrderBy { get; set; }

    /// <summary>Requires grouping by actor.</summary>
    [JsonProperty("exclude_deleted_users")]
    public bool? ExcludeDeletedUsers { get; set; }

    /// <summary>actor ids.</summary>
    [JsonProperty("actor_ids")]
    public List<string>? ActorIds { get; set; }

    /// <summary>actor types.</summary>
    [JsonProperty("actor_types")]
    public List<string>? ActorTypes { get; set; }

    /// <summary>agent ids.</summary>
    [JsonProperty("agent_ids")]
    public List<string>? AgentIds { get; set; }

    /// <summary>api key ids.</summary>
    [JsonProperty("api_key_ids")]
    public List<string>? ApiKeyIds { get; set; }

    /// <summary>claude tag categories.</summary>
    [JsonProperty("claude_tag_categories")]
    public List<string>? ClaudeTagCategories { get; set; }

    /// <summary>claude tag user ids.</summary>
    [JsonProperty("claude_tag_user_ids")]
    public List<string>? ClaudeTagUserIds { get; set; }

    /// <summary>context windows.</summary>
    [JsonProperty("context_windows")]
    public List<string>? ContextWindows { get; set; }

    /// <summary>deployment ids.</summary>
    [JsonProperty("deployment_ids")]
    public List<string>? DeploymentIds { get; set; }

    /// <summary>environment ids.</summary>
    [JsonProperty("environment_ids")]
    public List<string>? EnvironmentIds { get; set; }

    /// <summary>inference geos.</summary>
    [JsonProperty("inference_geos")]
    public List<string>? InferenceGeos { get; set; }

    /// <summary>models.</summary>
    [JsonProperty("models")]
    public List<string>? Models { get; set; }

    /// <summary>products.</summary>
    [JsonProperty("products")]
    public List<string>? Products { get; set; }

    /// <summary>rbac group ids.</summary>
    [JsonProperty("rbac_group_ids")]
    public List<string>? RbacGroupIds { get; set; }

    /// <summary>service tiers.</summary>
    [JsonProperty("service_tiers")]
    public List<string>? ServiceTiers { get; set; }

    /// <summary>session ids.</summary>
    [JsonProperty("session_ids")]
    public List<string>? SessionIds { get; set; }

    /// <summary>slack channel ids.</summary>
    [JsonProperty("slack_channel_ids")]
    public List<string>? SlackChannelIds { get; set; }

    /// <summary>speeds.</summary>
    [JsonProperty("speeds")]
    public List<string>? Speeds { get; set; }

    /// <summary>user profile ids.</summary>
    [JsonProperty("user_profile_ids")]
    public List<string>? UserProfileIds { get; set; }

    /// <summary>workspace ids.</summary>
    [JsonProperty("workspace_ids")]
    public List<string>? WorkspaceIds { get; set; }
}
/// <summary>Usage for a time range and combination of grouped dimensions.</summary>
public class AnthropicAnalyticsUsageRow
{
    /// <summary>starting at.</summary>
    [JsonProperty("starting_at")]
    public string? StartingAt { get; set; }

    /// <summary>ending at.</summary>
    [JsonProperty("ending_at")]
    public string? EndingAt { get; set; }

    /// <summary>group.</summary>
    [JsonProperty("group")]
    public AnthropicAnalyticsGroup? Group { get; set; }

    /// <summary>usage.</summary>
    [JsonProperty("usage")]
    public AnthropicAnalyticsUsage? Usage { get; set; }
}
/// <summary>Cost for a time range and combination of grouped dimensions.</summary>
public class AnthropicAnalyticsCostRow
{
    /// <summary>starting at.</summary>
    [JsonProperty("starting_at")]
    public string? StartingAt { get; set; }

    /// <summary>ending at.</summary>
    [JsonProperty("ending_at")]
    public string? EndingAt { get; set; }

    /// <summary>group.</summary>
    [JsonProperty("group")]
    public AnthropicAnalyticsGroup? Group { get; set; }

    /// <summary>cost.</summary>
    [JsonProperty("cost")]
    public AnthropicAnalyticsCost? Cost { get; set; }
}
/// <summary>Dimensions are null unless requested with group_by, or when no value applies.</summary>
public class AnthropicAnalyticsGroup
{
    /// <summary>product.</summary>
    [JsonProperty("product")]
    public string? Product { get; set; }

    /// <summary>model.</summary>
    [JsonProperty("model")]
    public string? Model { get; set; }

    /// <summary>context window.</summary>
    [JsonProperty("context_window")]
    public string? ContextWindow { get; set; }

    /// <summary>inference geo.</summary>
    [JsonProperty("inference_geo")]
    public string? InferenceGeo { get; set; }

    /// <summary>speed.</summary>
    [JsonProperty("speed")]
    public string? Speed { get; set; }

    /// <summary>slack channel id.</summary>
    [JsonProperty("slack_channel_id")]
    public string? SlackChannelId { get; set; }

    /// <summary>claude tag category.</summary>
    [JsonProperty("claude_tag_category")]
    public string? ClaudeTagCategory { get; set; }

    /// <summary>claude tag user id.</summary>
    [JsonProperty("claude_tag_user_id")]
    public string? ClaudeTagUserId { get; set; }

    /// <summary>rbac group id.</summary>
    [JsonProperty("rbac_group_id")]
    public string? RbacGroupId { get; set; }

    /// <summary>workspace id.</summary>
    [JsonProperty("workspace_id")]
    public string? WorkspaceId { get; set; }

    /// <summary>service tier.</summary>
    [JsonProperty("service_tier")]
    public string? ServiceTier { get; set; }

    /// <summary>user profile id.</summary>
    [JsonProperty("user_profile_id")]
    public string? UserProfileId { get; set; }

    /// <summary>agent id.</summary>
    [JsonProperty("agent_id")]
    public string? AgentId { get; set; }

    /// <summary>environment id.</summary>
    [JsonProperty("environment_id")]
    public string? EnvironmentId { get; set; }

    /// <summary>deployment id.</summary>
    [JsonProperty("deployment_id")]
    public string? DeploymentId { get; set; }

    /// <summary>session id.</summary>
    [JsonProperty("session_id")]
    public string? SessionId { get; set; }

    /// <summary>actor type.</summary>
    [JsonProperty("actor_type")]
    public string? ActorType { get; set; }

    /// <summary>api key id.</summary>
    [JsonProperty("api_key_id")]
    public string? ApiKeyId { get; set; }

    /// <summary>cost type.</summary>
    [JsonProperty("cost_type")]
    public string? CostType { get; set; }

    /// <summary>token type.</summary>
    [JsonProperty("token_type")]
    public string? TokenType { get; set; }

    /// <summary>actor.</summary>
    [JsonProperty("actor")]
    public AnthropicAnalyticsActor? Actor { get; set; }
}
/// <summary>Identity responsible for usage. Unknown identity fields are retained.</summary>
public class AnthropicAnalyticsActor
{
    /// <summary>type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>user id.</summary>
    [JsonProperty("user_id")]
    public string? UserId { get; set; }

    /// <summary>service account id.</summary>
    [JsonProperty("service_account_id")]
    public string? ServiceAccountId { get; set; }

    /// <summary>api key id.</summary>
    [JsonProperty("api_key_id")]
    public string? ApiKeyId { get; set; }

    /// <summary>email address.</summary>
    [JsonProperty("email_address")]
    public string? EmailAddress { get; set; }

    /// <summary>name.</summary>
    [JsonProperty("name")]
    public string? Name { get; set; }

    /// <summary>deleted.</summary>
    [JsonProperty("deleted")]
    public bool? Deleted { get; set; }
    /// <summary>Additional identity fields, including federated cloud principals.</summary>
    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }
}
/// <summary>Native token and request counts; token counters use 64-bit integers.</summary>
public class AnthropicAnalyticsUsage
{
    /// <summary>uncached input tokens.</summary>
    [JsonProperty("uncached_input_tokens")]
    public long UncachedInputTokens { get; set; }

    /// <summary>cache read input tokens.</summary>
    [JsonProperty("cache_read_input_tokens")]
    public long CacheReadInputTokens { get; set; }

    /// <summary>output tokens.</summary>
    [JsonProperty("output_tokens")]
    public long OutputTokens { get; set; }

    /// <summary>total tokens.</summary>
    [JsonProperty("total_tokens")]
    public long TotalTokens { get; set; }

    /// <summary>requests.</summary>
    [JsonProperty("requests")]
    public long Requests { get; set; }

    /// <summary>cache creation.</summary>
    [JsonProperty("cache_creation")]
    public AnthropicAnalyticsCacheCreation? CacheCreation { get; set; }

    /// <summary>server tool use.</summary>
    [JsonProperty("server_tool_use")]
    public AnthropicAnalyticsServerToolUse? ServerToolUse { get; set; }
}
/// <summary>Cache creation token counts by lifetime.</summary>
public class AnthropicAnalyticsCacheCreation
{
    /// <summary>ephemeral 1h input tokens.</summary>
    [JsonProperty("ephemeral_1h_input_tokens")]
    public long Ephemeral1hInputTokens { get; set; }

    /// <summary>ephemeral 5m input tokens.</summary>
    [JsonProperty("ephemeral_5m_input_tokens")]
    public long Ephemeral5mInputTokens { get; set; }
}
/// <summary>Server-tool usage.</summary>
public class AnthropicAnalyticsServerToolUse
{
    /// <summary>web search requests.</summary>
    [JsonProperty("web_search_requests")]
    public long WebSearchRequests { get; set; }
}
/// <summary>Amounts are exact decimal strings in USD cents, not dollars.</summary>
public class AnthropicAnalyticsCost
{
    /// <summary>Cost after discounts and before credits, in cents.</summary>
    [JsonProperty("amount")]
    public string? Amount { get; set; }

    /// <summary>Cost at list prices, in cents.</summary>
    [JsonProperty("list_amount")]
    public string? ListAmount { get; set; }

    /// <summary>currency.</summary>
    [JsonProperty("currency")]
    public string? Currency { get; set; }

    /// <summary>requests.</summary>
    [JsonProperty("requests")]
    public long? Requests { get; set; }
}
