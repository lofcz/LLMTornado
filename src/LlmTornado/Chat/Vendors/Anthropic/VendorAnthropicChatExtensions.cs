using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using LlmTornado.Code;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Chat.Vendors.Anthropic;

/// <summary>
/// Cache settings used by Anthropic.
/// </summary>
public class AnthropicCacheSettings
{
    /// <summary>
    /// "ephemeral" type of cache, shared object.
    /// </summary>
    public static readonly AnthropicCacheSettings Ephemeral = new AnthropicCacheSettings();

    /// <summary>
    /// "ephemeral" type of cache, with variable time to live.
    /// </summary>
    public static AnthropicCacheSettings EphemeralWithTtl(ChatRequestCacheTtl ttl)
    {
        return new AnthropicCacheSettings
        {
            Type = AnthropicCacheTypes.Ephemeral,
            Ttl = ttl
        };
    }

    /// <summary>
    /// Cache type.
    /// </summary>
    [JsonProperty("type")]
    public AnthropicCacheTypes Type { get; set; } = AnthropicCacheTypes.Ephemeral;

    /// <summary>
    /// Time to live. Increasing this increases the price multiplier.
    /// </summary>
    [JsonProperty("ttl")]
    public ChatRequestCacheTtl? Ttl { get; set; }

    private AnthropicCacheSettings()
    {
        
    }
}

/// <summary>
/// Anthropic cache types.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnthropicCacheTypes
{
    /// <summary>
    /// Ephemeral cache.
    /// </summary>
    [EnumMember(Value = "ephemeral")]
    Ephemeral
}


/// <summary>
/// Anthropic extended thinking modes sent as <c>thinking.type</c>.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnthropicThinkingTypes
{
    /// <summary>
    /// Manual extended thinking with a fixed <c>budget_tokens</c> limit.
    /// Deprecated on Claude Opus 4.6+, Sonnet 4.6, and rejected on Opus 4.7+.
    /// </summary>
    [EnumMember(Value = "enabled")]
    Enabled,

    /// <summary>
    /// Thinking disabled.
    /// </summary>
    [EnumMember(Value = "disabled")]
    Disabled,

    /// <summary>
    /// Adaptive thinking. Claude decides when and how much to think.
    /// Recommended for Claude Opus 4.6+, Sonnet 4.6, and required on Opus 4.7+ (NextOpus).
    /// </summary>
    [EnumMember(Value = "adaptive")]
    Adaptive,

    /// <summary>
    /// Sonnet 5.5: think only between tool calls, at low, medium, or high effort.
    /// This mode accepts no display, budget, or block-binding options.
    /// </summary>
    [EnumMember(Value = "between_tools")]
    BetweenTools
}

/// <summary>
/// Controls how extended thinking content is returned in Anthropic API responses.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnthropicThinkingDisplay
{
    /// <summary>
    /// Thinking blocks contain summarized thinking text. Default on Claude Opus 4.6, Sonnet 4.6, and earlier Claude 4 models.
    /// </summary>
    [EnumMember(Value = "summarized")]
    Summarized,

    /// <summary>
    /// Thinking blocks are returned with an empty <c>thinking</c> field while preserving the <c>signature</c> for multi-turn continuity.
    /// </summary>
    [EnumMember(Value = "omitted")]
    Omitted,

    /// <summary>
    /// Reasoning text is omitted (same as <see cref="Omitted"/>) and short progress updates between tool calls
    /// are returned as text. Requires the <c>thinking-display-updates-2026-08-18</c> beta header (added automatically).
    /// Supported on Claude Fable 5, Fable 5.1, and Mythos 5.1.
    /// </summary>
    [EnumMember(Value = "updates")]
    Updates
}

/// <summary>
/// How the API treats replayed thinking blocks whose prefix (system, tools, or earlier messages) changed.
/// Requires the <c>thinking-binding-controls-2026-08-01</c> beta header (added automatically).
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnthropicThinkingPrefixMismatchBehavior
{
    /// <summary>
    /// Reject the request with HTTP 400 when a thinking block's history changed.
    /// </summary>
    [EnumMember(Value = "error")]
    Reject,

    /// <summary>
    /// Drop the mismatched thinking block and continue. Dropped blocks are reported in <c>input_transformations</c>.
    /// </summary>
    [EnumMember(Value = "drop_block")]
    Drop
}

/// <summary>
/// Controls thinking-block binding when replaying signatures across turns.
/// </summary>
public class AnthropicThinkingBlockBinding
{
    /// <summary>
    /// Whether to reject or drop thinking blocks whose prefix changed.
    /// </summary>
    [JsonProperty("prefix_mismatch_behavior")]
    public AnthropicThinkingPrefixMismatchBehavior? PrefixMismatchBehavior { get; set; }
}

/// <summary>
/// When a mid-conversation system message should stop rendering.
/// Requires the <c>mid-conversation-system-clear-at-2026-08-21</c> beta header (added automatically).
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnthropicSystemClearAt
{
    /// <summary>
    /// Render for the current turn only, then keep the message in history at no token cost.
    /// </summary>
    [EnumMember(Value = "next_user_message")]
    NextUserMessage
}

/// <summary>
/// Thinking settings for Claude 3.7+ models.
/// </summary>
public class AnthropicThinkingSettings
{
    /// <summary>
    /// Thinking mode sent as <c>thinking.type</c>.
    /// When unset, legacy <see cref="Adaptive"/> / <see cref="Enabled"/> flags are used.
    /// </summary>
    public AnthropicThinkingTypes? Type { get; set; }

    /// <summary>
    /// The budget_tokens parameter determines the maximum number of tokens Claude is allowed use for its internal reasoning process. Larger budgets can improve response quality by enabling more thorough analysis for complex problems, although Claude may not use the entire budget allocated, especially at ranges above 32K.
    /// <br/><b>Note: budget_tokens must always be less than the max_tokens specified.</b>
    /// <br/><b>Deprecated on Claude 4.6+:</b> Use <see cref="AnthropicThinkingTypes.Adaptive"/> with the effort parameter instead.
    /// </summary>
    public int? BudgetTokens { get; set; }
    
    /// <summary>
    /// Whether thinking is enabled with manual budget control (type: "enabled").
    /// <br/><b>Deprecated:</b> Prefer <see cref="Type"/> = <see cref="AnthropicThinkingTypes.Enabled"/>.
    /// </summary>
    public bool Enabled { get; set; }
    
    /// <summary>
    /// Whether adaptive thinking is enabled (type: "adaptive").
    /// <br/><b>Deprecated:</b> Prefer <see cref="Type"/> = <see cref="AnthropicThinkingTypes.Adaptive"/>.
    /// </summary>
    public bool Adaptive { get; set; }

    /// <summary>
    /// Controls whether thinking content is returned in responses.
    /// Use <see cref="AnthropicThinkingDisplay.Omitted"/> to preserve signatures without streaming thinking text.
    /// Use <see cref="AnthropicThinkingDisplay.Updates"/> for progress updates between tool calls (beta).
    /// </summary>
    public AnthropicThinkingDisplay? Display { get; set; }

    /// <summary>
    /// Thinking-block binding controls (beta). Requires <c>thinking-binding-controls-2026-08-01</c>.
    /// </summary>
    public AnthropicThinkingBlockBinding? BlockBinding { get; set; }

    /// <summary>
    /// Resolves the effective thinking mode from explicit <see cref="Type"/> or legacy flags.
    /// </summary>
    internal AnthropicThinkingTypes ResolvedType
    {
        get
        {
            if (Type is not null)
            {
                return Type.Value;
            }

            if (Adaptive)
            {
                return AnthropicThinkingTypes.Adaptive;
            }

            if (Enabled || BudgetTokens > 0)
            {
                return AnthropicThinkingTypes.Enabled;
            }

            return AnthropicThinkingTypes.Disabled;
        }
    }

    /// <summary>
    /// Creates adaptive thinking settings (<c>thinking: {"type":"adaptive"}</c>).
    /// Combine with <see cref="ChatRequestVendorAnthropicExtensions.Effort"/> to guide thinking depth.
    /// On Claude Opus 4.7/4.8, API responses default to <c>display: "omitted"</c> unless you set
    /// <see cref="Display"/> to <see cref="AnthropicThinkingDisplay.Summarized"/>.
    /// </summary>
    public static AnthropicThinkingSettings CreateAdaptive() => new AnthropicThinkingSettings
    {
        Type = AnthropicThinkingTypes.Adaptive
    };

    /// <summary>
    /// Creates manual extended thinking settings with a token budget.
    /// </summary>
    public static AnthropicThinkingSettings CreateEnabled(int budgetTokens) => new AnthropicThinkingSettings
    {
        Type = AnthropicThinkingTypes.Enabled,
        BudgetTokens = budgetTokens
    };
}

/// <summary>
/// Anthropic chat request item.
/// </summary>
public interface IAnthropicChatRequestItem
{
    
}

/// <summary>
/// Represents a skill that can be loaded in the container.
/// </summary>
public class AnthropicSkill
{
    /// <summary>
    /// Type of the skill (typically "anthropic" for built-in skills).
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "anthropic";
    
    /// <summary>
    /// Skill identifier (e.g., "xlsx", "pptx", "pdf").
    /// </summary>
    [JsonProperty("skill_id")]
    public string SkillId { get; set; }
    
    /// <summary>
    /// Version of the skill (typically "latest" or a specific version ID).
    /// </summary>
    [JsonProperty("version")]
    public string Version { get; set; } = "latest";
    
    /// <summary>
    /// Creates a new Anthropic skill.
    /// </summary>
    /// <param name="skillId">Skill identifier (e.g., "xlsx", "pptx", "pdf")</param>
    /// <param name="version">Version of the skill (default: "latest")</param>
    public AnthropicSkill(string skillId, string version = "latest")
    {
        SkillId = skillId;
        Version = version;
        if(skillId == "xlsx" || skillId == "pptx" || skillId == "pdf" || skillId == "docx")
        {
            Type = "anthropic";
        }
        else
        {
            Type = "custom";
        }
    }
}

/// <summary>
/// Container configuration for loading skills and resources.
/// </summary>
public class AnthropicContainer
{
    /// <summary>
    /// List of skills to load in the container max 8
    /// </summary>
    [JsonProperty("skills")]
    public List<AnthropicSkill>? Skills { get; set; }
}

/// <summary>
/// MCP servers to be utilized in this request (max 20).
/// </summary>
public class AnthropicMcpServer
{
    /// <summary>
    /// Connection type. Currently only <c>url</c> is supported.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; } = "url";

    /// <summary>
    /// Unique identifier for this MCP server. Referenced by an <see cref="AnthropicMcpToolset"/>.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Remote MCP server URL, or a routed tunnel URL such as <c>https://echo.example.tunnel.anthropic.com/mcp</c>.
    /// When <see cref="Tunnel"/> is set and this is empty, the URL is resolved automatically during serialization.
    /// </summary>
    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// OAuth authorization token for the upstream MCP server, if required.
    /// </summary>
    [JsonProperty("authorization_token")]
    public string? AuthorizationToken { get; set; }

    /// <summary>
    /// Optional tunnel routing configuration for private-network MCP servers.
    /// </summary>
    [JsonIgnore]
    public AnthropicMcpTunnelConfig? Tunnel { get; set; }

    /// <summary>
    /// Deprecated MCP server tool configuration (mcp-client-2025-04-04).
    /// Migrated automatically to <see cref="AnthropicMcpToolset"/> entries in the outbound <c>tools</c> array.
    /// </summary>
    [JsonIgnore]
    public AnthropicMcpConfiguration? Configuration { get; set; }

    /// <summary>
    /// Resolves the effective MCP server URL, building it from <see cref="Tunnel"/> when needed.
    /// </summary>
    public string ResolveUrl() =>
        !string.IsNullOrWhiteSpace(Url) ? Url : Tunnel?.BuildUrl()
        ?? throw new InvalidOperationException($"MCP server '{Name}' requires Url or Tunnel configuration.");

    /// <summary>
    /// Creates an MCP server definition for a tunneled private-network server.
    /// </summary>
    public static AnthropicMcpServer ForTunnel(string name, AnthropicMcpTunnelConfig tunnel, string? authorizationToken = null) =>
        new()
        {
            Name = name,
            Tunnel = tunnel,
            Url = tunnel.BuildUrl(),
            AuthorizationToken = authorizationToken
        };

    /// <summary>
    /// Creates an MCP server definition for a tunneled private-network server.
    /// </summary>
    public static AnthropicMcpServer ForTunnel(string name, string subdomain, string tunnelDomain, string path = "/mcp", string? authorizationToken = null) =>
        ForTunnel(name, AnthropicMcpTunnelConfig.Create(subdomain, tunnelDomain, path), authorizationToken);
}

/// <summary>
/// Deprecated MCP server tool configuration from mcp-client-2025-04-04.
/// Prefer configuring tools with <see cref="AnthropicMcpToolset"/>.
/// </summary>
public class AnthropicMcpConfiguration
{
    /// <summary>
    /// Allowlisted tool names.
    /// </summary>
    [JsonProperty("allowed_tools")]
    public string[]? AllowedTools { get; set; }

    /// <summary>
    /// Whether MCP tools are enabled for this server.
    /// </summary>
    [JsonProperty("enabled")]
    public bool? Enabled { get; set; } = true;
}

/// <summary>
/// Supported trigger types for compaction.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnthropicCompactionTriggerTypes
{
    /// <summary>
    /// Trigger compaction based on input token count.
    /// </summary>
    [EnumMember(Value = "input_tokens")]
    InputTokens
}

/// <summary>
/// Supported compaction edit types.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnthropicCompactionEditTypes
{
    /// <summary>
    /// Compact context using server-side summarization (version 2026-01-12).
    /// </summary>
    [EnumMember(Value = "compact_20260112")]
    Compact20260112
}

/// <summary>
/// Supported inference geographic regions for data residency controls.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnthropicInferenceGeoOptions
{
    /// <summary>
    /// Global routing (default). Inference may run in any available geography for optimal performance and availability.
    /// </summary>
    [EnumMember(Value = "global")]
    Global,
    
    /// <summary>
    /// US-only inference. Priced at 1.1x on Claude Opus 4.6 and newer models.
    /// </summary>
    [EnumMember(Value = "us")]
    Us,
    
    /// <summary>
    /// Inference geography was not reported for this response.
    /// </summary>
    [EnumMember(Value = "not_available")]
    NotAvailable
}

/// <summary>
/// Trigger configuration for when compaction should activate.
/// </summary>
public class AnthropicCompactionTrigger
{
    /// <summary>
    /// The type of trigger. Currently only <see cref="AnthropicCompactionTriggerTypes.InputTokens"/> is supported.
    /// </summary>
    [JsonProperty("type")]
    public AnthropicCompactionTriggerTypes Type { get; set; } = AnthropicCompactionTriggerTypes.InputTokens;
    
    /// <summary>
    /// The token count threshold at which compaction triggers. Must be at least 50,000.
    /// Default is 150,000 tokens.
    /// </summary>
    [JsonProperty("value")]
    public int Value { get; set; } = 150_000;
    
    /// <summary>
    /// Creates a trigger based on input token count.
    /// </summary>
    /// <param name="tokenThreshold">Token count threshold (minimum 50,000).</param>
    public AnthropicCompactionTrigger(int tokenThreshold)
    {
        Value = tokenThreshold;
    }
    
    /// <summary>
    /// Creates a trigger with default threshold (150,000 tokens).
    /// </summary>
    public AnthropicCompactionTrigger()
    {
    }
}

/// <summary>
/// A compaction edit entry for automatic context summarization.
/// </summary>
public class AnthropicCompactionEdit
{
    /// <summary>
    /// The type of edit. Currently only <see cref="AnthropicCompactionEditTypes.Compact20260112"/> is supported.
    /// </summary>
    [JsonProperty("type")]
    public AnthropicCompactionEditTypes Type { get; set; } = AnthropicCompactionEditTypes.Compact20260112;
    
    /// <summary>
    /// When to trigger compaction. Defaults to 150,000 tokens if not set.
    /// </summary>
    [JsonProperty("trigger")]
    public AnthropicCompactionTrigger? Trigger { get; set; }
    
    /// <summary>
    /// Whether to pause after generating the compaction summary, returning a response with the "compaction" stop reason.
    /// This allows you to add additional content blocks before the API continues.
    /// </summary>
    [JsonProperty("pause_after_compaction")]
    public bool? PauseAfterCompaction { get; set; }
    
    /// <summary>
    /// Custom summarization instructions. Completely replaces the default summarization prompt when provided.
    /// </summary>
    [JsonProperty("instructions")]
    public string? Instructions { get; set; }
}

/// <summary>
/// Context management configuration for server-side compaction.
/// Enables effectively infinite conversations by automatically summarizing older context.
/// </summary>
public class AnthropicContextManagement
{
    /// <summary>
    /// List of context management edits to apply.
    /// </summary>
    [JsonProperty("edits")]
    public List<AnthropicCompactionEdit> Edits { get; set; } = [];
    
    /// <summary>
    /// Creates a context management configuration with a default compaction edit.
    /// </summary>
    public static AnthropicContextManagement Default()
    {
        return new AnthropicContextManagement
        {
            Edits = [new AnthropicCompactionEdit()]
        };
    }
    
    /// <summary>
    /// Creates a context management configuration with a custom token threshold trigger.
    /// </summary>
    /// <param name="tokenThreshold">Token count threshold for triggering compaction (minimum 50,000).</param>
    public static AnthropicContextManagement WithTrigger(int tokenThreshold)
    {
        return new AnthropicContextManagement
        {
            Edits = [new AnthropicCompactionEdit { Trigger = new AnthropicCompactionTrigger(tokenThreshold) }]
        };
    }
}

/// <summary>
/// Task budget types for Anthropic agentic loops.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnthropicTaskBudgetTypes
{
    /// <summary>
    /// Token-based task budget.
    /// </summary>
    [EnumMember(Value = "tokens")]
    Tokens
}

/// <summary>
/// Advisory token budget for a full agentic loop (thinking, tool calls, tool results, and output).
/// Requires the task-budgets-2026-03-13 beta header (added automatically).
/// Supported on Claude Opus 4.7+.
/// </summary>
public class AnthropicTaskBudget
{
    /// <summary>
    /// Budget type. Currently only <see cref="AnthropicTaskBudgetTypes.Tokens"/> is supported.
    /// </summary>
    [JsonProperty("type")]
    public AnthropicTaskBudgetTypes Type { get; set; } = AnthropicTaskBudgetTypes.Tokens;
    
    /// <summary>
    /// Total tokens Claude can spend across the agentic loop.
    /// Minimum accepted value is 20,000.
    /// </summary>
    [JsonProperty("total")]
    public int Total { get; set; }
    
    /// <summary>
    /// Budget remainder carried over from a prior request (e.g. after compaction).
    /// Defaults to <see cref="Total"/> when omitted.
    /// </summary>
    [JsonProperty("remaining", NullValueHandling = NullValueHandling.Ignore)]
    public int? Remaining { get; set; }
    
    /// <summary>
    /// Creates a token-based task budget.
    /// </summary>
    /// <param name="total">Total token budget (minimum 20,000).</param>
    public AnthropicTaskBudget(int total)
    {
        Total = total;
    }
    
    /// <summary>
    /// Creates a token-based task budget with an explicit remaining value.
    /// </summary>
    /// <param name="total">Total token budget (minimum 20,000).</param>
    /// <param name="remaining">Remaining budget after prior turns.</param>
    public AnthropicTaskBudget(int total, int remaining)
    {
        Total = total;
        Remaining = remaining;
    }
    
    /// <summary>
    /// Creates an empty task budget for deserialization.
    /// </summary>
    public AnthropicTaskBudget()
    {
    }
    
    /// <summary>
    /// Creates a token-based task budget.
    /// </summary>
    /// <param name="total">Total token budget (minimum 20,000).</param>
    /// <param name="remaining">Optional remaining budget after prior turns.</param>
    public static AnthropicTaskBudget Tokens(int total, int? remaining = null)
    {
        return new AnthropicTaskBudget
        {
            Type = AnthropicTaskBudgetTypes.Tokens,
            Total = total,
            Remaining = remaining
        };
    }
}

/// <summary>
///     Chat features supported only by Anthropic.
/// </summary>
public class ChatRequestVendorAnthropicExtensions
{
    /// <summary>Optional server-held history. The application retains its own replay history.</summary>
    public AnthropicMessageThread? Thread { get; set; }

    /// <summary>End-customer attribution, sent in the anthropic-user-profile-id header.</summary>
    public string? UserProfileId { get; set; }

    /// <summary>Summarize the conversation on demand. Cannot be combined with ContextManagement.</summary>
    public AnthropicOnDemandCompaction? Compaction { get; set; }

    /// <summary>
    ///     Enables modification of the outbound chat request just before sending it. Use this to control cache in chat-like scenarios.<br/>
    ///     Arguments: <b>System message</b>; <b>User, Assistant messages</b>; <b>Tools</b>
    /// </summary>
    public Action<VendorAnthropicChatRequestMessageContent?, List<VendorAnthropicChatRequestMessageContent>, List<VendorAnthropicToolFunction>?>? OutboundRequest;
    
    /// <summary>
    /// Effort level for <c>output_config.effort</c>. Use with
    /// <see cref="AnthropicThinkingSettings.CreateAdaptive"/> or <see cref="ChatRequest.ReasoningBudget"/> = -1 on
    /// Claude Opus 4.6+, Sonnet 4.6, Opus 4.7/4.8, and Claude 5. Takes precedence over
    /// <see cref="ChatRequest.ReasoningEffort"/> when both are set. No beta header on GA models.
    /// </summary>
    public AnthropicEffortLevels? Effort { get; set; }
    
    /// <summary>
    /// Thinking settings for Claude 3.7+ models.<br/>
    /// Important: while supported, please use <see cref="ChatRequest.ReasoningBudget"/> instead.
    /// </summary>
    public AnthropicThinkingSettings? Thinking { get; set; }
    
    /// <summary>
    /// Server-side tools.
    /// </summary>
    public List<IVendorAnthropicChatRequestBuiltInTool>? BuiltInTools { get; set; }
    
    /// <summary>
    /// Container configuration for loading skills. Skills allow Claude to perform specialized tasks like creating PowerPoint presentations, Excel spreadsheets, or PDF documents.<br/>
    /// <b>Note:</b> When using skills, you must also include code execution in your tools and use the beta Messages API.
    /// </summary>
    public AnthropicContainer? Container { get; set; }

    /// <summary>
    /// List of MCP servers to be utilized in this request. (Max length 20)
    /// </summary>
    public List<AnthropicMcpServer>? McpServers { get; set; }

    /// <summary>
    /// MCP toolset definitions for the Messages API <c>tools</c> array.
    /// When omitted, toolsets are generated automatically from <see cref="McpServers"/>.
    /// </summary>
    public List<AnthropicMcpToolset>? McpToolsets { get; set; }
    
    /// <summary>
    /// Server-side context management configuration for automatic compaction.
    /// Enables effectively infinite conversations by summarizing older context when approaching window limits.
    /// Requires the compact-2026-01-12 beta header (added automatically).
    /// Available on Claude Opus 4.6 only.
    /// </summary>
    public AnthropicContextManagement? ContextManagement { get; set; }
    
    /// <summary>
    /// Controls where model inference runs for this request.
    /// Available on Claude Opus 4.6 and newer models only.
    /// </summary>
    public AnthropicInferenceGeoOptions? InferenceGeo { get; set; }
    
    /// <summary>
    /// Advisory token budget for the full agentic loop.
    /// Requires the task-budgets-2026-03-13 beta header (added automatically).
    /// Supported on Claude Opus 4.7+.
    /// </summary>
    public AnthropicTaskBudget? TaskBudget { get; set; }
    
    /// <summary>
    /// Cache diagnostics configuration. When set, enables cache miss diagnosis.
    /// Pass <see cref="AnthropicCacheDiagnosticsRequest.PreviousMessageId"/> as
    /// <c>null</c> on the first turn to opt in without a prior message to compare against; on subsequent turns pass the
    /// <c>id</c> from the previous response.
    /// </summary>
    public AnthropicCacheDiagnosticsRequest? CacheDiagnostics { get; set; }

    /// <summary>
    /// Advisor tool configuration. Pairs <see cref="ChatRequest.Model"/> (executor) with a stronger advisor model.
    /// Requires the <c>advisor-tool-2026-03-01</c> beta header (added automatically).
    /// </summary>
    public AnthropicAdvisorToolRequest? AdvisorTool { get; set; }

    /// <summary>
    /// Server-side fallback when the primary model refuses a request (beta).
    /// Use <see cref="AnthropicFallbacks.Default"/> or <see cref="AnthropicFallbacks.FromModels"/>.
    /// Requires the <c>server-side-fallback-2026-07-01</c> beta header (added automatically).
    /// </summary>
    public AnthropicFallbacks? Fallbacks { get; set; }

    /// <summary>
    /// When true, adds the <c>mid-conversation-tool-changes-2026-07-01</c> beta header so tools can
    /// be added or removed between turns without invalidating the prompt cache.
    /// </summary>
    public bool EnableMidConversationToolChanges { get; set; }

    /// <summary>Request fetched MCP tool listings and use the September 2026 MCP connector beta.</summary>
    public bool EnableMcpToolListing { get; set; }

    /// <summary>Request thinking transformation diagnostics without opting into prefix mismatch enforcement.</summary>
    public bool EnableThinkingBindingDiagnostics { get; set; }
}

/// <summary>
/// Anthropic <c>fallbacks</c> parameter (beta). Serializes to the string <c>"default"</c> or a model ID list.
/// </summary>
[JsonConverter(typeof(AnthropicFallbacksJsonConverter))]
public class AnthropicFallbacks
{
    /// <summary>
    /// Apply Anthropic's recommended fallback models by refusal category.
    /// </summary>
    public static AnthropicFallbacks Default { get; } = new AnthropicFallbacks { UseDefault = true };

    /// <summary>
    /// When true, the API value is <c>"default"</c>.
    /// </summary>
    public bool UseDefault { get; init; }

    /// <summary>
    /// Explicit fallback model IDs (up to three). Ignored when <see cref="UseDefault"/> is true.
    /// </summary>
    public List<string>? Models { get; init; }

    /// <summary>
    /// Creates an explicit fallback model list.
    /// </summary>
    public static AnthropicFallbacks FromModels(params string[] models) => new AnthropicFallbacks
    {
        Models = models is { Length: > 0 } ? [..models] : []
    };
}

internal sealed class AnthropicFallbacksJsonConverter : JsonConverter<AnthropicFallbacks>
{
    public override void WriteJson(JsonWriter writer, AnthropicFallbacks? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        if (value.UseDefault)
        {
            writer.WriteValue("default");
            return;
        }

        serializer.Serialize(writer, value.Models ?? []);
    }

    public override AnthropicFallbacks? ReadJson(JsonReader reader, Type objectType, AnthropicFallbacks? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);
        if (token.Type == JTokenType.String && string.Equals(token.Value<string>(), "default", StringComparison.OrdinalIgnoreCase))
        {
            return AnthropicFallbacks.Default;
        }

        if (token.Type == JTokenType.Array)
        {
            return new AnthropicFallbacks { Models = token.ToObject<List<string>>(serializer) };
        }

        return null;
    }
}

/// <summary>
/// Request-side cache diagnostics configuration for Anthropic Messages API.
/// </summary>
public class AnthropicCacheDiagnosticsRequest
{
    /// <summary>
    /// The response <c>id</c> from the previous turn to compare against. Pass <c>null</c> on the first turn to opt in
    /// without a prior message to compare against.
    /// </summary>
    [JsonProperty("previous_message_id", NullValueHandling = NullValueHandling.Include)]
    public string? PreviousMessageId { get; set; }
}

/// <summary>
/// Response-side cache diagnostics from Anthropic Messages API.
/// </summary>
public class AnthropicCacheDiagnosticsResponse
{
    /// <summary>
    /// When non-null, identifies the first point of divergence from the previous request. When null on a non-null
    /// diagnostics object, the comparison was still running when the response was serialized.
    /// </summary>
    [JsonProperty("cache_miss_reason")]
    public AnthropicCacheMissReason? CacheMissReason { get; set; }
}

/// <summary>
/// Describes why a prompt cache miss occurred relative to the previous request.
/// </summary>
public class AnthropicCacheMissReason
{
    /// <summary>
    /// The type of divergence detected.
    /// </summary>
    [JsonProperty("type")]
    [JsonConverter(typeof(StringEnumConverter))]
    public AnthropicCacheMissReasonTypes Type { get; set; }
    
    /// <summary>
    /// Estimate of input tokens after the divergence point. Present for <c>*_changed</c> types only.
    /// </summary>
    [JsonProperty("cache_missed_input_tokens")]
    public int? CacheMissedInputTokens { get; set; }
}

/// <summary>
/// Cache miss reason types returned by Anthropic cache diagnostics.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnthropicCacheMissReasonTypes
{
    /// <summary>
    /// The model differs from the previous request.
    /// </summary>
    [EnumMember(Value = "model_changed")]
    ModelChanged,
    
    /// <summary>
    /// The system prompt differs from the previous request.
    /// </summary>
    [EnumMember(Value = "system_changed")]
    SystemChanged,
    
    /// <summary>
    /// The tools array differs from the previous request.
    /// </summary>
    [EnumMember(Value = "tools_changed")]
    ToolsChanged,
    
    /// <summary>
    /// An earlier message was altered, reordered, or removed rather than appended to.
    /// </summary>
    [EnumMember(Value = "messages_changed")]
    MessagesChanged,
    
    /// <summary>
    /// No stored fingerprint exists for the supplied previous message id.
    /// </summary>
    [EnumMember(Value = "previous_message_not_found")]
    PreviousMessageNotFound,
    
    /// <summary>
    /// Diagnostic information was not available for this request.
    /// </summary>
    [EnumMember(Value = "unavailable")]
    Unavailable
}
