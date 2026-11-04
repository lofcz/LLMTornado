using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LlmTornado.RateLimits;

/// <summary>
/// Query parameters for listing Anthropic organization or workspace rate limits.
/// </summary>
public sealed class AnthropicRateLimitsListQuery
{
    /// <summary>Maximum entries per page.</summary>
    [JsonProperty("limit")]
    public int? Limit { get; set; }
    /// <summary>
    /// Restricts the response to a single rate limit group category.
    /// </summary>
    [JsonProperty("group_type")]
    [JsonConverter(typeof(StringEnumConverter))]
    public AnthropicRateLimitGroupType? GroupType { get; set; }

    /// <summary>
    /// Organization endpoint only: filter to the single entry containing this model ID or alias.
    /// Returns 404 if the model is not found.
    /// </summary>
    [JsonProperty("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Opaque cursor from a previous response's <see cref="AnthropicRateLimitsListResponse.NextPage"/>.
    /// </summary>
    [JsonProperty("page")]
    public string? Page { get; set; }

    /// <summary>
    /// Builds query string parameters for HTTP requests.
    /// </summary>
    public Dictionary<string, object>? ToQueryParams(bool includeModelFilter = true)
    {
        Dictionary<string, object> parameters = [];
        if (Limit is not null) parameters["limit"] = Limit.Value;

        if (GroupType is not null)
        {
            parameters["group_type"] = GroupType.Value switch
            {
                AnthropicRateLimitGroupType.ModelGroup => "model_group",
                AnthropicRateLimitGroupType.Batch => "batch",
                AnthropicRateLimitGroupType.TokenCount => "token_count",
                AnthropicRateLimitGroupType.Files => "files",
                AnthropicRateLimitGroupType.Skills => "skills",
                AnthropicRateLimitGroupType.WebSearch => "web_search",
                _ => "model_group"
            };
        }

        if (includeModelFilter && !string.IsNullOrWhiteSpace(Model))
        {
            parameters["model"] = Model;
        }

        if (!string.IsNullOrWhiteSpace(Page))
        {
            parameters["page"] = Page;
        }

        return parameters.Count > 0 ? parameters : null;
    }
}

/// <summary>
/// Paginated list of rate limit entries from the Anthropic Admin API.
/// </summary>
public class AnthropicRateLimitsListResponse
{
    /// <summary>
    /// Rate limit entries for the organization or workspace.
    /// </summary>
    [JsonProperty("data")]
    public List<AnthropicRateLimitEntry> Data { get; set; } = [];

    /// <summary>
    /// Cursor for the next page, or null when there are no further pages.
    /// </summary>
    [JsonProperty("next_page")]
    public string? NextPage { get; set; }
}

/// <summary>
/// A single rate limit group entry (organization or workspace override).
/// </summary>
public class AnthropicRateLimitEntry
{
    /// <summary>Rate limit resource ID.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }

    /// <summary>Canonical group metadata returned by API version 2026-10-21.</summary>
    [JsonProperty("group")]
    public AnthropicRateLimitGroup? Group { get; set; }

    /// <summary>
    /// Object type: <c>rate_limit</c> or <c>workspace_rate_limit</c>.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Category of limits this entry represents.
    /// </summary>
    [JsonProperty("group_type")]
    [JsonConverter(typeof(StringEnumConverter))]
    public AnthropicRateLimitGroupType GroupType { get => Group?.Type ?? legacyGroupType; set => legacyGroupType = value; }
    private AnthropicRateLimitGroupType legacyGroupType;

    /// <summary>
    /// Model IDs and aliases for <see cref="AnthropicRateLimitGroupType.ModelGroup"/> entries; otherwise null.
    /// </summary>
    [JsonProperty("models")]
    public List<string>? Models { get; set; }

    /// <summary>
    /// Limiter values for this group.
    /// </summary>
    [JsonProperty("limits")]
    public List<AnthropicRateLimitValue> Limits { get; set; } = [];
}

/// <summary>Rate-limit group metadata, replacing the legacy top-level group_type.</summary>
public class AnthropicRateLimitGroup
{
    /// <summary>Group category.</summary>
    [JsonProperty("type")]
    public AnthropicRateLimitGroupType Type { get; set; }
    /// <summary>Native group ID.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }
    /// <summary>Model group's human-readable name, when provided.</summary>
    [JsonProperty("display_name")]
    public string? DisplayName { get; set; }
}

/// <summary>
/// A configured limiter value. Workspace entries may include <see cref="OrgLimit"/>.
/// </summary>
public class AnthropicRateLimitValue
{
    /// <summary>
    /// Limiter type (for example, <c>requests_per_minute</c> or <c>input_tokens_per_minute</c>).
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Configured limit value for this limiter type (workspace override when listing workspace limits).
    /// </summary>
    [JsonProperty("value")]
    public double Value { get; set; }

    /// <summary>
    /// Organization-level value for the same limiter (workspace endpoint only). Null when not configured at org level.
    /// </summary>
    [JsonProperty("org_limit")]
    public double? OrgLimit { get; set; }
}
