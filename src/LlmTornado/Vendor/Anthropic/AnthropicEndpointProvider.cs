using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using LlmTornado.Batch;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Code.Models;
using LlmTornado.Code.Sse;
using LlmTornado.Models;
using LlmTornado.Models.Vendors;
using LlmTornado.Models.Vendors.Anthropic;
using LlmTornado.Threads;
using LlmTornado.Tokenize;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FunctionCall = LlmTornado.ChatFunctions.FunctionCall;
using ToolCall = LlmTornado.ChatFunctions.ToolCall;

namespace LlmTornado.Code.Vendor;

/// <summary>
/// Built-in Anthropic provider.
/// </summary>
public class AnthropicEndpointProvider : BaseEndpointProvider, IEndpointProvider, IEndpointProviderExtended
{
    private const string StreamMsgStart = $"message_start";
    private const string StreamMsgStop = $"message_stop";
    private const string StreamMsgDelta = $"message_delta";
    private const string StreamError = $"error";
    private const string StreamPing = $"ping";
    private const string StreamContentBlockDelta = $"content_block_delta";
    private const string StreamContentBlockStart = $"content_block_start";
    private const string StreamContentBlockStop = $"content_block_stop";

    private static readonly Dictionary<string, StreamRawActions> StreamEventsMap = new Dictionary<string, StreamRawActions>
    {
        { StreamMsgStart, StreamRawActions.MsgStart },
        { StreamMsgStop, StreamRawActions.MsgStop },
        { StreamMsgDelta, StreamRawActions.MsgDelta },
        { StreamError, StreamRawActions.Error },
        { StreamPing, StreamRawActions.Ping },
        { StreamContentBlockDelta, StreamRawActions.ContentBlockDelta },
        { StreamContentBlockStart, StreamRawActions.ContentBlockStart },
        { StreamContentBlockStop, StreamRawActions.ContentBlockStop }
    };


    public static Version OutboundVersion { get; set; } = OutboundDefaultVersion;

    /// <summary>Anthropic protocol revision. Set October2026 to opt into the 2026-10-21 contract.</summary>
    public string ApiRevision { get; set; } = AnthropicApiVersions.Legacy;

    /// <summary>Optional Google Cloud routing and caller-owned token refresh.</summary>
    public AnthropicGoogleCloudOptions? GoogleCloud { get; set; }

    /// <summary>Optional OAuth bearer token source. Called for every request; replaces x-api-key authentication.</summary>
    public Func<string>? BearerTokenProvider { get; set; }

    public Func<CapabilityEndpoints, string?, RequestUrlContext, string>? UrlResolver { get; set; } 
    
    public Action<HttpRequestMessage, object?, bool>? RequestResolver { get; set; }
    
    public Action<JObject, RequestSerializerContext>? RequestSerializer { get; set; }
    
    private enum StreamRawActions
    {
        Unknown,
        MsgStart,
        MsgStop,
        MsgDelta,
        Error,
        Ping,
        ContentBlockDelta,
        ContentBlockStart,
        ContentBlockStop
    }
    
    public AnthropicEndpointProvider()
    {
        Provider = LLmProviders.Anthropic;
        StoreApiAuth();
    }
    
    public override JsonSchemaCapabilities GetJsonSchemaCapabilities()
    {
        return new JsonSchemaCapabilities
        {
            Const = true // Anthropic supports const keyword in JSON schemas
        };
    }

    private enum AnthropicStreamBlockStartTypes
    {
        Unknown,
        Text,
        ToolUse,
        Thinking,
        RedactedThinking,
        Compaction,
        McpToolListing
    }

    private class AnthropicStreamBlockStart
    {
        public static readonly FrozenDictionary<string, AnthropicStreamBlockStartTypes> Map = new Dictionary<string, AnthropicStreamBlockStartTypes>
        {
            { "text", AnthropicStreamBlockStartTypes.Text },
            { "tool_use", AnthropicStreamBlockStartTypes.ToolUse },
            { "thinking", AnthropicStreamBlockStartTypes.Thinking },
            { "redacted_thinking", AnthropicStreamBlockStartTypes.RedactedThinking },
            { "compaction", AnthropicStreamBlockStartTypes.Compaction },
            { "mcp_tool_listing", AnthropicStreamBlockStartTypes.McpToolListing }
        }.ToFrozenDictionary();
        
        public class AnthropicStreamBlockStartData
        {
            [JsonProperty("mcp_server_name")]
            public string? McpServerName { get; set; }

            [JsonProperty("tools")]
            public List<AnthropicMcpToolDefinition>? Tools { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
            
            [JsonProperty("text")]
            public string Text { get; set; }

            [JsonProperty("thinking")]
            public string? Thinking { get; set; }

            [JsonProperty("input")]
            public JObject? Input { get; set; }

            [JsonProperty("content")]
            public string? Content { get; set; }

            [JsonProperty("signature")]
            public string? Signature { get; set; }

            
            /// <summary>
            /// For tools
            /// </summary>
            [JsonProperty("name")]
            public string? Name { get; set; }
            
            /// <summary>
            /// For tools
            /// </summary>
            [JsonProperty("id")]
            public string? Id { get; set; }
            
            /// <summary>
            /// For redacted thinking blocks
            /// </summary>
            public string? Data { get; set; }
        }
        
        [JsonProperty("index")]
        public int Index { get; set; }
        
        [JsonProperty("content_block")]
        public AnthropicStreamBlockStartData ContentBlock { get; set; }
    }

    private enum AnthropicStreamBlockDeltaTypes
    {
        Unknown,
        TextDelta,
        ThinkingDelta,
        SignatureDelta,
        InputJsonDelta,
        CitationDelta,
        CompactionDelta
    }

    private class AnthropicStreamBlockDelta
    {
        public static readonly FrozenDictionary<string, AnthropicStreamBlockDeltaTypes> Map = new Dictionary<string, AnthropicStreamBlockDeltaTypes>
        {
            { "text_delta", AnthropicStreamBlockDeltaTypes.TextDelta },
            { "citations_delta", AnthropicStreamBlockDeltaTypes.CitationDelta },
            { "thinking_delta", AnthropicStreamBlockDeltaTypes.ThinkingDelta },
            { "signature_delta", AnthropicStreamBlockDeltaTypes.SignatureDelta },
            { "input_json_delta", AnthropicStreamBlockDeltaTypes.InputJsonDelta },
            { "compaction_delta", AnthropicStreamBlockDeltaTypes.CompactionDelta }
        }.ToFrozenDictionary();
        
        public class AnthropicStreamBlockDeltaData
        {
            [JsonProperty("type")]
            public string Type { get; set; }
            
            [JsonProperty("text")]
            public string Text { get; set; }
            
            [JsonProperty("partial_json")]
            public string? PartialJson { get; set; }
            
            [JsonProperty("thinking")]
            public string? Thinking { get; set; }
            
            [JsonProperty("signature")]
            public string? Signature { get; set; }
            
            [JsonProperty("citation")]
            public IChatMessagePartCitation? Citation { get; set; }
            
            [JsonProperty("content")]
            public string? Content { get; set; }
        }
        
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("index")]
        public int Index { get; set; }
        
        [JsonProperty("delta")]
        public AnthropicStreamBlockDeltaData? Delta { get; set; }
        
        [JsonProperty("citation")]
        public IChatMessagePartCitation? Citation { get; set; }
    }

    private class AnthropicStreamBlockStop
    {
        [JsonProperty("index")]
        public int Index { get; set; }
    }

    private class AnthropicStreamMsgStart
    {
        [JsonProperty("message")]
        public VendorAnthropicChatResult Message { get; set; }
    }
    
    private class AnthropicStreamMsgDelta
    {
        [JsonProperty("input_transformations")]
        public List<AnthropicInputTransformation>? InputTransformations { get; set; }

        [JsonProperty("delta")]
        public AnthropicStreamMsgDeltaData Delta { get; set; }
        
        [JsonProperty("usage")]
        public AnthropicStreamMsgDeltaUsage Usage { get; set; }
    }
    
    private class AnthropicStreamMsgDeltaUsage
    {
        [JsonProperty("output_tokens")]
        public int OutputTokens { get; set; }
        
        [JsonProperty("input_tokens")]
        public int? InputTokens { get; set; }
        
        [JsonProperty("cache_creation_input_tokens")]
        public int? CacheCreationInputTokens { get; set; }
        
        [JsonProperty("cache_read_input_tokens")]
        public int? CacheReadInputTokens { get; set; }
    }

    private class AnthropicStreamMsgDeltaData
    {
        [JsonProperty("input_transformations")]
        public List<AnthropicInputTransformation>? InputTransformations { get; set; }

        [JsonProperty("stop_reason")]
        public string? StopReason { get; set; }
        
        [JsonProperty("stop_sequence")]
        public string? StopSequence { get; set; }
        
        [JsonProperty("stop_details")]
        public VendorAnthropicStopDetails? StopDetails { get; set; }
    }
    
    private static bool RequiresOnDemandCompactionHeader(object? data)
    {
        if (data is Compaction.CompactionRequest compaction)
        {
            return compaction.OnDemand || compaction.Messages?.Any(m => m.Parts?.Any(p => p.CompactionSignature is not null) == true) == true;
        }
        if (data is not ChatRequest chat) return false;
        return chat.VendorExtensions?.Anthropic?.Compaction is not null
            || chat.Messages?.Any(m => m.Parts?.Any(p => p.CompactionSignature is not null) == true) == true;
    }

    private static bool RequiresCompactionHeader(object? data)
    {
        if (data is Compaction.CompactionRequest)
        {
            return true;
        }
        
        if (data is not ChatRequest chatRequest)
        {
            return false;
        }
        
        return chatRequest.VendorExtensions?.Anthropic?.ContextManagement is not null;
    }
    
    private static bool RequiresTaskBudgetHeader(object? data)
    {
        if (data is not ChatRequest chatRequest)
        {
            return false;
        }
        
        return chatRequest.VendorExtensions?.Anthropic?.TaskBudget is not null;
    }

    private static bool RequiresAdvisorToolHeader(object? data)
    {
        if (data is not ChatRequest chatRequest)
        {
            return false;
        }

        if (chatRequest.VendorExtensions?.Anthropic?.AdvisorTool is not null)
        {
            return true;
        }

        return chatRequest.VendorExtensions?.Anthropic?.BuiltInTools?.Any(x =>
            x.Type is VendorAnthropicChatRequestBuiltInToolTypes.Advisor20260301) == true;
    }
    
    private static bool IsOpus46OrNewer(string? modelName)
        => ChatModelAnthropicHelper.IsOpus46OrNewer(modelName);

    private static bool RequiresThinkingDisplayUpdatesHeader(object? data)
    {
        return data is ChatRequest chatRequest
            && chatRequest.VendorExtensions?.Anthropic?.Thinking?.Display is AnthropicThinkingDisplay.Updates;
    }

    private static bool RequiresThinkingBindingHeader(object? data)
    {
        return data is ChatRequest chatRequest
            && (chatRequest.VendorExtensions?.Anthropic?.Thinking?.BlockBinding is not null
                || chatRequest.VendorExtensions?.Anthropic?.EnableThinkingBindingDiagnostics == true);
    }

    private static bool RequiresServerSideFallbackHeader(object? data)
    {
        return data is ChatRequest chatRequest
            && chatRequest.VendorExtensions?.Anthropic?.Fallbacks is not null;
    }

    private static bool RequiresMidConversationToolChangesHeader(object? data)
    {
        return data is ChatRequest chatRequest
            && chatRequest.VendorExtensions?.Anthropic?.EnableMidConversationToolChanges is true;
    }

    private static bool RequiresMidConversationOutputConfigHeader(object? data)
    {
        return data is ChatRequest chatRequest
            && chatRequest.Messages?.Any(x =>
                x.VendorExtensions is ChatMessageVendorExtensionsAnthropic { Effort: not null }) is true;
    }

    private static bool RequiresMidConversationSystemClearAtHeader(object? data)
    {
        return data is ChatRequest chatRequest
            && chatRequest.Messages?.Any(x =>
                x.VendorExtensions is ChatMessageVendorExtensionsAnthropic { ClearAt: not null }) is true;
    }
    
    private static bool RequiresFastModeHeader(object? data)
    {
        if (data is not ChatRequest chatRequest)
        {
            return false;
        }
        
        return chatRequest.Speed is ChatRequestSpeeds.Fast
            && chatRequest.Model?.Name.StartsWith(ChatModelAnthropicHelper.Opus55ModelId, StringComparison.OrdinalIgnoreCase) != true;
    }
    
    private static bool RequiresDynamicWebToolsHeader(object? data)
    {
        if (data is not ChatRequest chatRequest)
        {
            return false;
        }
        
        return chatRequest.VendorExtensions?.Anthropic?.BuiltInTools?.Any(x => 
            x.Type is VendorAnthropicChatRequestBuiltInToolTypes.WebSearch20260209 
                or VendorAnthropicChatRequestBuiltInToolTypes.WebFetch20260209) == true;
    }
    
    private static bool RequiresEffortHeader(object? data)
    {
        if (data is not ChatRequest chatRequest || chatRequest.ReasoningEffort is null)
        {
            return false;
        }

        string? modelName = chatRequest.Model?.Name;
        return modelName is not null
            && modelName.StartsWith("claude-opus-4-5", StringComparison.OrdinalIgnoreCase);
    }


    private static bool RequiresAdvancedToolUseHeader(object? data)
    {
        if (data is not ChatRequest chatRequest)
        {
            return false;
        }
        
        // Check if any tools have allowed_callers or defer_loading set
        if (chatRequest.Tools?.Any(x => x.AllowedCallers?.Count > 0 || x.DeferLoading == true) == true)
        {
            return true;
        }
        
        // Check if any built-in tools are tool search tools
        if (chatRequest.VendorExtensions?.Anthropic?.BuiltInTools?.Any(x => 
            x.Type is Chat.Vendors.Anthropic.VendorAnthropicChatRequestBuiltInToolTypes.ToolSearchRegex20251119 or 
                      Chat.Vendors.Anthropic.VendorAnthropicChatRequestBuiltInToolTypes.ToolSearchBm2520251119) == true)
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Gets endpoint url for a given capability.
    /// </summary>
    public static string GetEndpointUrlFragment(CapabilityEndpoints endpoint)
    {
        return endpoint switch
        {
            CapabilityEndpoints.Chat => "messages",
            CapabilityEndpoints.Completions => "complete",
            CapabilityEndpoints.Models => "models",
            CapabilityEndpoints.Files => "files",
            CapabilityEndpoints.Skills => "skills",
            CapabilityEndpoints.Tokenize => "messages/count_tokens",
            CapabilityEndpoints.Compaction => "messages",
            CapabilityEndpoints.Batch => "messages/batches",
            CapabilityEndpoints.AnthropicManagedAgents => "agents",
            CapabilityEndpoints.AnthropicManagedAgentSessions => "sessions",
            CapabilityEndpoints.AnthropicManagedAgentEnvironments => "environments",
            CapabilityEndpoints.AnthropicUserProfiles => "user_profiles",
            CapabilityEndpoints.AnthropicAnalytics => "organizations/analytics",
            CapabilityEndpoints.AnthropicOrganizationWebhooks => "organizations/webhooks",
            CapabilityEndpoints.AnthropicClaudeTag => "organizations/claude_tag",
            _ => throw new Exception($"Anthropic doesn't support endpoint {endpoint}")
        };
    }
    
    public override string ApiUrl(CapabilityEndpoints endpoint, string? url, IModel? model = null)
    {
        if (endpoint is CapabilityEndpoints.RateLimits)
        {
            string path = string.IsNullOrWhiteSpace(url)
                ? "organizations/rate_limits"
                : $"organizations/workspaces/{url.Trim('/')}/rate_limits";

            return UrlResolver is not null
                ? UrlResolver.Invoke(endpoint, url, new RequestUrlContext(path, url, model))
                : GoogleCloud?.GetUrl(path) ?? $"https://api.anthropic.com/v1/{path}";
        }

        string eStr = GetEndpointUrlFragment(endpoint);
        return UrlResolver is not null ? string.Format(UrlResolver.Invoke(endpoint, url, new RequestUrlContext(eStr, url, model)), eStr, url, model?.Name) : GoogleCloud?.GetUrl($"{eStr}{url}") ?? $"https://api.anthropic.com/v1/{eStr}{url}";
    }
    
    public override async IAsyncEnumerable<ChatResult?> InboundStream(StreamReader reader, ChatRequest request, ChatStreamEventHandler? eventHandler)
    {
        ChatMessage? accuToolsMessage = null;
        ChatMessage? accuMessage = null;
        ChatUsage? plaintextUsage = null;
        ChatMessage? accuThinking = null;
        ChatMessagePart? currentThinkingPart = null;
        ChatMessageFinishReasons finishReason = ChatMessageFinishReasons.Unknown;
        ChatStopDetails? stopDetails = null;
        string? stopSequence = null;
        ChatMessagePart? currentPart = null;
        StringBuilder currentPartTextBuilder = new StringBuilder();
        ChatRequestServiceTiers? serviceTier = null;
        ChatRequestSpeeds? speed = null;
        ChatResponseVendorExtensions? vendorExtensions = null;
        string? messageId = null;
        
        #if DEBUG
        List<string> items = [];
        #endif
        
        await foreach (SseItem<string> item in SseParser.Create(reader.BaseStream).EnumerateAsync(request.CancellationToken))
        {
            #if DEBUG
            items.Add(item.Data);
            #endif

            if (eventHandler?.OnSse is not null)
            {
                await eventHandler.OnSse.Invoke(new ServerSentEvent
                {
                    Data = item.Data,
                    EventType = item.EventType
                });
            }
            
            if (request.CancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            string line = item.Data;

            StreamRawActions rawAction = StreamEventsMap.GetValueOrDefault(item.EventType, StreamRawActions.Unknown);
            
            if (rawAction is StreamRawActions.Error)
            {
                continue;
            }
            
            switch (rawAction)
            {
                case StreamRawActions.ContentBlockStart:
                {
                    AnthropicStreamBlockStart? res = JsonConvert.DeserializeObject<AnthropicStreamBlockStart>(line);

                    if (res is null)
                    {
                        continue;
                    }

                    AnthropicStreamBlockStartTypes type = AnthropicStreamBlockStart.Map.GetValueOrDefault(res.ContentBlock.Type, AnthropicStreamBlockStartTypes.Unknown);
                    currentPart = null;

                    if (type is AnthropicStreamBlockStartTypes.Text)
                    {
                        currentPart = new ChatMessagePart
                        {
                            Type = ChatMessageTypes.Text,
                            Text = res.ContentBlock.Text ?? string.Empty
                        };

                        accuMessage ??= new ChatMessage(ChatMessageRoles.Assistant);
                        accuMessage.Parts ??= [];
                        accuMessage.Parts.Add(currentPart);
                        currentPartTextBuilder.Clear();
                        currentPartTextBuilder.Append(currentPart.Text);
                        if (!string.IsNullOrEmpty(currentPart.Text))
                        {
                            (accuMessage.ContentBuilder ??= new StringBuilder()).Append(currentPart.Text);
                            yield return new ChatResult { Choices = [new ChatChoice { Delta = new ChatMessage(ChatMessageRoles.Assistant, currentPart.Text) }] };
                        }
                    }
                    
                    switch (type)
                    {
                        case AnthropicStreamBlockStartTypes.McpToolListing:
                        {
                            ChatMessagePart part = new(new AnthropicMcpToolListing
                            {
                                McpServerName = res.ContentBlock.McpServerName ?? string.Empty,
                                Tools = res.ContentBlock.Tools ?? []
                            });
                            accuMessage ??= new ChatMessage(ChatMessageRoles.Assistant);
                            accuMessage.Parts ??= [];
                            accuMessage.Parts.Add(part);
                            yield return new ChatResult
                            {
                                Choices = [new ChatChoice { Delta = new ChatMessage(ChatMessageRoles.Assistant, [part]) }]
                            };
                            break;
                        }
                        case AnthropicStreamBlockStartTypes.ToolUse:
                        {
                            ToolCall tc = new ToolCall
                            {
                                Id = res.ContentBlock.Id,
                                Index = res.Index
                            };

                            FunctionCall fc = new FunctionCall
                            {
                                Name = res.ContentBlock.Name ?? string.Empty,
                                ToolCall = tc,
                                Arguments = res.ContentBlock.Input?.ToString(Formatting.None) ?? "{}"
                            };

                            tc.FunctionCall = fc;
                        
                            accuToolsMessage ??= new ChatMessage(ChatMessageRoles.Assistant)
                            {
                                ToolCalls = []
                            };

                            accuToolsMessage.ToolCalls?.Add(tc);
                            accuMessage ??= new ChatMessage(ChatMessageRoles.Assistant);
                            (accuMessage.Parts ??= []).Add(new(ChatMessageTypes.AnthropicToolUse) { AnthropicToolUseId = tc.Id });
                        
                            accuToolsMessage.ContentBuilder ??= new StringBuilder();
                            accuToolsMessage.ContentBuilder.Clear();
                            break;
                        }
                        case AnthropicStreamBlockStartTypes.Thinking:
                        {
                            currentThinkingPart = new(ChatMessageTypes.Reasoning)
                            {
                                Reasoning = new() { Content = res.ContentBlock.Thinking ?? string.Empty, Signature = res.ContentBlock.Signature, Provider = LLmProviders.Anthropic }
                            };
                            accuThinking = new ChatMessage(ChatMessageRoles.Assistant, [currentThinkingPart]) { ContentBuilder = new StringBuilder(res.ContentBlock.Thinking ?? string.Empty) };
                            accuMessage ??= new ChatMessage(ChatMessageRoles.Assistant);
                            (accuMessage.Parts ??= []).Add(currentThinkingPart);
                            break;
                        }
                        case AnthropicStreamBlockStartTypes.RedactedThinking:
                        {
                            string token = res.ContentBlock.Data ?? string.Empty;
                        
                            ChatMessagePart redacted = new(ChatMessageTypes.Reasoning)
                            {
                                Reasoning = new ChatMessageReasoningData
                                {
                                    Signature = token,
                                    Provider = LLmProviders.Anthropic
                                }
                            };
                            accuMessage ??= new ChatMessage(ChatMessageRoles.Assistant);
                            (accuMessage.Parts ??= []).Add(redacted);
                        
                            yield return new ChatResult
                            {
                                Choices =
                                [
                                    new ChatChoice
                                    {
                                        Delta = new ChatMessage(ChatMessageRoles.Assistant, [
                                            new ChatMessagePart(ChatMessageTypes.Reasoning)
                                            {
                                                Reasoning = new ChatMessageReasoningData
                                                {
                                                    Signature = token,
                                                    Provider = LLmProviders.Anthropic
                                                }
                                            }
                                        ])
                                    }
                                ]
                            };
                            break;
                        }
                        case AnthropicStreamBlockStartTypes.Compaction:
                        {
                            // On-demand compaction arrives whole in content_block_start, without deltas.
                            if (res.ContentBlock.Content is not null)
                            {
                                yield return new ChatResult
                                {
                                    Choices = [new ChatChoice
                                    {
                                        Delta = new ChatMessage(ChatMessageRoles.Assistant, [new ChatMessagePart
                                        {
                                            Type = ChatMessageTypes.Compaction,
                                            Text = res.ContentBlock.Content,
                                            CompactionSignature = res.ContentBlock.Signature
                                        }])
                                    }]
                                };
                            }
                            break;
                        }
                    }
                    
                    break;
                }
                case StreamRawActions.ContentBlockDelta:
                {
                    AnthropicStreamBlockDelta? res = JsonConvert.DeserializeObject<AnthropicStreamBlockDelta>(line);

                    if (accuToolsMessage is not null && res?.Delta?.Type == "input_json_delta")
                    {
                        accuToolsMessage.ContentBuilder ??= new StringBuilder();
                        string? argumentsDelta = res?.Delta?.PartialJson;
                        accuToolsMessage.ContentBuilder.Append(argumentsDelta);

                        if (!string.IsNullOrEmpty(argumentsDelta) && eventHandler?.FunctionCallDeltaHandler is not null)
                        {
                            ToolCall? call = accuToolsMessage.ToolCalls?.FirstOrDefault(x => x.Index == res?.Index)
                                             ?? accuToolsMessage.ToolCalls?.LastOrDefault();
                            await eventHandler.FunctionCallDeltaHandler.Invoke(new FunctionCallStreamUpdate
                            {
                                Name = call?.FunctionCall?.Name,
                                ArgumentsDelta = argumentsDelta,
                                ArgumentsSnapshot = accuToolsMessage.ContentBuilder.ToString(),
                                CallId = call?.Id,
                                Index = call?.Index
                            });
                        }
                    }
                    else if (res?.Delta is not null)
                    {
                        AnthropicStreamBlockDeltaTypes type = AnthropicStreamBlockDelta.Map.GetValueOrDefault(res.Delta.Type, AnthropicStreamBlockDeltaTypes.Unknown);

                        switch (type)
                        {
                            case AnthropicStreamBlockDeltaTypes.TextDelta:
                            {
                                accuMessage ??= new ChatMessage(ChatMessageRoles.Assistant);
                                accuMessage.ContentBuilder ??= new StringBuilder();
                                accuMessage.ContentBuilder.Append(res.Delta.Text);

                                currentPartTextBuilder.Append(res.Delta.Text);
                                
                                yield return new ChatResult
                                {
                                    Choices =
                                    [
                                        new ChatChoice
                                        {
                                            Delta = new ChatMessage(ChatMessageRoles.Assistant, res.Delta.Text)
                                        }
                                    ]
                                };
                                break;
                            }
                            case AnthropicStreamBlockDeltaTypes.CitationDelta:
                            {
                                IChatMessagePartCitation? cit = res.Delta?.Citation;

                                if (cit is not null)
                                {
                                    if (currentPart is not null)
                                    {
                                        currentPart.Citations ??= [];
                                        currentPart.Citations.Add(cit);
                                    }

                                    yield return new ChatResult
                                    {
                                        Choices =
                                        [
                                            new ChatChoice
                                            {
                                                Delta = new ChatMessage(ChatMessageRoles.Assistant)
                                                {
                                                    Parts = [
                                                        new ChatMessagePart
                                                        {
                                                            Type = ChatMessageTypes.Text,
                                                            Citations = [ cit ]
                                                        }
                                                    ]
                                                }
                                            }
                                        ]
                                    };
                                }

                                break;
                            }
                            case AnthropicStreamBlockDeltaTypes.SignatureDelta:
                            {
                                if (currentThinkingPart?.Reasoning is not null && accuThinking is not null)
                                {
                                    currentThinkingPart.Reasoning.Signature = (currentThinkingPart.Reasoning.Signature ?? string.Empty) + res.Delta.Signature;
                                    accuThinking.VendorExtensions = new ChatMessageVendorExtensionsAnthropic { Signature = currentThinkingPart.Reasoning.Signature };
                                }

                                break;
                            }
                            case AnthropicStreamBlockDeltaTypes.ThinkingDelta:
                            {
                                accuThinking ??= new ChatMessage(ChatMessageRoles.Assistant);
                                accuThinking.ContentBuilder ??= new StringBuilder();
                                accuThinking.ContentBuilder.Append(res.Delta.Thinking);

                                yield return new ChatResult
                                {
                                    Choices =
                                    [
                                        new ChatChoice
                                        {
                                            Delta = new ChatMessage(ChatMessageRoles.Assistant, [
                                                new ChatMessagePart(ChatMessageTypes.Reasoning)
                                                {
                                                    Reasoning = new ChatMessageReasoningData
                                                    {
                                                        Content = res.Delta.Thinking ?? string.Empty,
                                                        Signature = currentThinkingPart?.Reasoning?.Signature,
                                                        Provider = LLmProviders.Anthropic
                                                    }
                                                }
                                            ])
                                        }
                                    ]
                                };

                                break;
                            }
                            case AnthropicStreamBlockDeltaTypes.CompactionDelta:
                            {
                                // Compaction delta delivers the complete summary content in a single delta
                                string compactionContent = res.Delta.Content ?? string.Empty;
                                
                                yield return new ChatResult
                                {
                                    Choices =
                                    [
                                        new ChatChoice
                                        {
                                            Delta = new ChatMessage(ChatMessageRoles.Assistant, [
                                                new ChatMessagePart
                                                {
                                                    Type = ChatMessageTypes.Compaction,
                                                    Text = compactionContent
                                                }
                                            ])
                                        }
                                    ]
                                };

                                break;
                            }
                        }
                    }

                    break;
                }
                case StreamRawActions.ContentBlockStop:
                {
                    AnthropicStreamBlockStop? res = JsonConvert.DeserializeObject<AnthropicStreamBlockStop>(line);

                    if (currentPart is not null)
                    {
                        currentPart.Text = currentPartTextBuilder.ToString();
                    }

                    if (accuToolsMessage is not null)
                    {
                        ToolCall? lastCall = accuToolsMessage.ToolCalls?.FirstOrDefault(x => x.Index == res?.Index);

                        if (lastCall is not null && accuToolsMessage.ContentBuilder?.Length > 0)
                        {
                            lastCall.FunctionCall.Arguments = accuToolsMessage.ContentBuilder?.ToString() ?? string.Empty;
                        }

                        accuToolsMessage.ContentBuilder?.Clear();
                    }
                    
                    if (accuThinking is not null)
                    {
                        if (currentThinkingPart?.Reasoning is not null)
                            currentThinkingPart.Reasoning.Content = accuThinking.ContentBuilder?.ToString() ?? string.Empty;
                        
                        yield return new ChatResult
                        {
                            Choices =
                            [
                                new ChatChoice
                                {
                                    Delta = accuThinking
                                }
                            ],
                            StreamInternalKind = ChatResultStreamInternalKinds.AssistantMessageTransientBlock
                        };

                        accuThinking = null;
                        currentThinkingPart = null;
                    }
                    
                    break;
                }
                case StreamRawActions.MsgDelta:
                {
                    AnthropicStreamMsgDelta? res = JsonConvert.DeserializeObject<AnthropicStreamMsgDelta>(line);

                    if (res is not null)
                    {
                        List<AnthropicInputTransformation>? transformations = res.Delta.InputTransformations ?? res.InputTransformations;
                        if (transformations is not null)
                        {
                            vendorExtensions ??= new ChatResponseVendorExtensions();
                            vendorExtensions.Anthropic ??= new ChatResponseVendorAnthropicExtensions();
                            vendorExtensions.Anthropic.InputTransformations = transformations;
                        }
                        plaintextUsage ??= new ChatUsage(LLmProviders.Anthropic);
                        plaintextUsage.CompletionTokens = res.Usage.OutputTokens;

                        // the usage of message_delta is cumulative; with server tools it carries the final input and cache counts
                        if (res.Usage.InputTokens is not null)
                        {
                            plaintextUsage.PromptTokens = res.Usage.InputTokens.Value;
                        }

                        if (res.Usage.CacheCreationInputTokens is not null)
                        {
                            plaintextUsage.CacheCreationTokens = res.Usage.CacheCreationInputTokens;
                        }

                        if (res.Usage.CacheReadInputTokens is not null)
                        {
                            plaintextUsage.CacheReadTokens = res.Usage.CacheReadInputTokens;
                        }

                        if (res.Delta.StopReason is not null)
                        {
                            finishReason = ChatMessageFinishReasonsConverter.Map.GetValueOrDefault(res.Delta.StopReason, ChatMessageFinishReasons.Unknown);
                        }

                        if (res.Delta.StopDetails is not null)
                        {
                            stopDetails = res.Delta.StopDetails.ToChatStopDetails();
                        }
                        
                        if (res.Delta.StopSequence is not null)
                        {
                            stopSequence = res.Delta.StopSequence;
                        }
                    }
                    
                    break;
                }
                case StreamRawActions.MsgStart:
                {
                    AnthropicStreamMsgStart? res = JsonConvert.DeserializeObject<AnthropicStreamMsgStart>(line);
  
                    if (res is not null)
                    {
                        messageId = res.Message.Id;
                        
                        if (res.Message.Diagnostics is not null || res.Message.InputTransformations is not null)
                        {
                            vendorExtensions = new ChatResponseVendorExtensions
                            {
                                Anthropic = new ChatResponseVendorAnthropicExtensions
                                {
                                    CacheDiagnostics = res.Message.Diagnostics,
                                    InputTransformations = res.Message.InputTransformations
                                }
                            };
                        }
                        
                        if (res.Message.Usage.InputTokens + res.Message.Usage.OutputTokens > 0)
                        {
                            plaintextUsage = new ChatUsage(LLmProviders.Anthropic)
                            {
                                TotalTokens = res.Message.Usage.InputTokens + res.Message.Usage.OutputTokens,
                                CompletionTokens = res.Message.Usage.OutputTokens,
                                PromptTokens = res.Message.Usage.InputTokens,
                                CacheCreationTokens = res.Message.Usage.CacheCreationInputTokens,
                                CacheReadTokens = res.Message.Usage.CacheReadInputTokens
                            };
                            
                            serviceTier = res.Message.Usage.ServiceTier switch
                            {
                                "priority" => ChatRequestServiceTiers.Priority,
                                _ => null
                            };
                            
                            speed = res.Message.Usage.Speed switch
                            {
                                "fast" => ChatRequestSpeeds.Fast,
                                "standard" => ChatRequestSpeeds.Standard,
                                _ => null
                            };
                        }
                    }
                    
                    break;
                }
                case StreamRawActions.MsgStop:
                {
                    break;
                }
            }
        }

        plaintextUsage ??= new ChatUsage(LLmProviders.Anthropic);
        plaintextUsage.TotalTokens = plaintextUsage.CompletionTokens + plaintextUsage.PromptTokens;

        // yielded once with every call of the turn: yielding after each tool_use block re-sent the calls collected so far
        if (accuToolsMessage is not null)
        {
            accuMessage ??= new ChatMessage(ChatMessageRoles.Assistant);
            accuMessage.ToolCalls = accuToolsMessage.ToolCalls;
            yield return new ChatResult
            {
                Choices = [
                    new ChatChoice
                    {
                        Delta = accuMessage
                    }
                ],
                Usage = plaintextUsage
            };
        }

        if (accuMessage is not null)
        {
            accuMessage.Content = accuMessage.ContentBuilder?.ToString();   
        }
        
        yield return new ChatResult
        {
            Id = messageId,
            Choices =
            [
                new ChatChoice
                {
                    Delta = accuMessage
                }
            ],
            StreamInternalKind = ChatResultStreamInternalKinds.AppendAssistantMessage,
            Usage = plaintextUsage,
            ServiceTier = serviceTier,
            Speed = speed,
            VendorExtensions = vendorExtensions
        };
        
        yield return new ChatResult
        {
            Id = messageId,
            Usage = plaintextUsage,
            Choices = [
                new ChatChoice
                {
                    FinishReason = finishReason,
                    StopReason = stopSequence,
                    StopDetails = stopDetails
                }
            ],
            StreamInternalKind = ChatResultStreamInternalKinds.FinishData,
            ServiceTier = serviceTier,
            Speed = speed,
            VendorExtensions = vendorExtensions
        };
    }
    
    public override async IAsyncEnumerable<object?> InboundStream(Type type, StreamReader reader)
    {
        yield break;
    }

    public override async IAsyncEnumerable<T?> InboundStream<T>(StreamReader reader) where T : class
    {
        yield break;
    }
    
    public override HttpRequestMessage OutboundMessage(string url, HttpMethod verb, object? data, bool streaming, object? sourceObject)
    {
        HttpRequestMessage req = new HttpRequestMessage(verb, url)
        {
            Version = OutboundVersion
        };
        
        req.Headers.Add("User-Agent", EndpointBase.ResolveUserAgent(Api));
        req.Headers.Add("anthropic-version", (sourceObject as AnthropicThreadRequestContext)?.ApiRevision ?? ApiRevision);
        
        if (IsDirectBrowserAccessEnabled())
        {
            req.Headers.Add("anthropic-dangerous-direct-browser-access", "true");
        }

        ProviderAuthentication? auth = Api?.GetProvider(LLmProviders.Anthropic).Auth;

        Func<string>? tokenProvider = GoogleCloud?.AccessTokenProvider ?? BearerTokenProvider;
        if (GoogleCloud is not null && tokenProvider is null)
            throw new InvalidOperationException("Google Cloud requires an access token provider.");
        if (tokenProvider is not null)
        {
            string token = tokenProvider();
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("The access token provider returned an empty token.");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        else if (auth?.ApiKey is not null)
        {
            req.Headers.Add("x-api-key", auth.ApiKey);
        }

        string? profileId = sourceObject switch
        {
            AnthropicThreadRequestContext thread => thread.UserProfileId,
            ChatRequest chat => chat.VendorExtensions?.Anthropic?.UserProfileId,
            TokenizeRequest tokenize => tokenize.AnthropicUserProfileId ?? tokenize.SourceChatRequest?.VendorExtensions?.Anthropic?.UserProfileId,
            BatchRequest batch => batch.VendorExtensions?.Anthropic?.UserProfileId,
            _ => null
        };
        if (profileId is not null) req.Headers.Add("anthropic-user-profile-id", profileId);

        if (RequestResolver is not null)
        {
            RequestResolver.Invoke(req, data, streaming);
            if (sourceObject is AnthropicThreadRequestContext context)
            {
                req.Headers.Remove("anthropic-beta");
                req.Headers.Add("anthropic-beta", context.BetaHeaders);
            }
        }
        else if (sourceObject is AnthropicThreadRequestContext threadContext)
        {
            req.Headers.Add("anthropic-beta", threadContext.BetaHeaders);
        }
        else if (!url.Contains("/organizations/", StringComparison.OrdinalIgnoreCase) && !url.Contains("/user_profiles", StringComparison.OrdinalIgnoreCase))
        {
            req.Headers.Add("anthropic-beta", GetBetaHeaders(sourceObject));
        }

        return req;
    }

    internal static HashSet<string> GetBetaHeaders(object? sourceObject)
    {
        string? currentModel = (sourceObject as ChatRequest)?.Model?.Name;
        bool isOpus46Plus = IsOpus46OrNewer(currentModel);

        HashSet<string> betaHeaders = [
            "files-api-2025-04-14",
            "code-execution-2025-08-25",
            "search-results-2025-06-09"
        ];

        if (sourceObject is ChatRequest { VendorExtensions.Anthropic.Thread: not null })
            betaHeaders.Add(AnthropicMessageThread.BetaHeader);

        // Interleaved thinking header is deprecated on Opus 4.6+ (adaptive thinking auto-enables it)
        if (!isOpus46Plus)
        {
            betaHeaders.Add("interleaved-thinking-2025-05-14");
        }

        // Add effort beta header if applicable (Claude Opus 4.5 only; GA on 4.6+)
        if (RequiresEffortHeader(sourceObject))
        {
            betaHeaders.Add("effort-2025-11-24");
        }

        // Add compaction beta header if context management is configured
        if (RequiresOnDemandCompactionHeader(sourceObject))
        {
            betaHeaders.Add(Compaction.CompactionRequest.OnDemandBetaHeader);
        }
        else if (RequiresCompactionHeader(sourceObject))
        {
            betaHeaders.Add(Compaction.CompactionRequest.BetaHeader);
        }

        // Add task budget beta header if task budget is configured
        if (RequiresTaskBudgetHeader(sourceObject))
        {
            betaHeaders.Add("task-budgets-2026-03-13");
        }

        // Add advanced tool use beta header if applicable (programmatic tool calling, tool search)
        if (RequiresAdvancedToolUseHeader(sourceObject))
        {
            betaHeaders.Add("advanced-tool-use-2025-11-20");
        }

        // Add fast mode beta header if applicable
        if (RequiresFastModeHeader(sourceObject))
        {
            betaHeaders.Add("fast-mode-2026-02-01");
        }

        // Add dynamic web tools beta header when web_search_20260209 or web_fetch_20260209 are used
        if (RequiresDynamicWebToolsHeader(sourceObject))
        {
            betaHeaders.Add("code-execution-web-tools-2026-02-09");
        }

        if (RequiresAdvisorToolHeader(sourceObject))
        {
            betaHeaders.Add("advisor-tool-2026-03-01");
        }

        if (RequiresThinkingDisplayUpdatesHeader(sourceObject))
        {
            betaHeaders.Add("thinking-display-updates-2026-08-18");
        }

        if (RequiresThinkingBindingHeader(sourceObject))
        {
            betaHeaders.Add("thinking-binding-controls-2026-08-01");
        }

        if (RequiresServerSideFallbackHeader(sourceObject))
        {
            betaHeaders.Add("server-side-fallback-2026-07-01");
        }

        if (sourceObject is ChatRequest inlineRequest && AnthropicConversationTools.HasInlineChanges(inlineRequest))
        {
            betaHeaders.Add("inline-tools-2026-09-15");
        }
        else if (RequiresMidConversationToolChangesHeader(sourceObject))
        {
            betaHeaders.Add("mid-conversation-tool-changes-2026-07-01");
        }

        if (sourceObject is ChatRequest mcpListingRequest && AnthropicConversationTools.UsesMcpListings(mcpListingRequest))
            betaHeaders.Add(AnthropicMcpBetaHeaders.McpClientSeptember2026);

        if (RequiresMidConversationOutputConfigHeader(sourceObject))
        {
            betaHeaders.Add("mid-conversation-output-config-2026-07-01");
        }

        if (RequiresMidConversationSystemClearAtHeader(sourceObject))
        {
            betaHeaders.Add("mid-conversation-system-clear-at-2026-08-21");
        }

        return AccumulateHeaders(betaHeaders, sourceObject);
    }

    public override void ParseInboundHeaders<T>(T res, HttpResponseMessage response)
    {
        res.Provider = this;
    }
    
    public override void ParseInboundHeaders(object? res, HttpResponseMessage response)
    {
        
    }

    private static readonly Dictionary<Type, Func<string, string?, object?, object?>> InboundMap = new Dictionary<Type, Func<string, string?, object?, object?>>
    {
        { typeof(RetrievedModelsResult), (jsonData, postData, req) => VendorAnthropicRetrievedModelsDeserializer.DeserializeList(jsonData, postData) },
        { typeof(RetrievedModel), (jsonData, postData, req) => VendorAnthropicRetrievedModelsDeserializer.DeserializeModel(jsonData, postData) }
    };
    
    public override T? InboundMessage<T>(string jsonData, string? postData, object? requestObject) where T : default
    {
        Type type = typeof(T);

        if (InboundMap.TryGetValue(type, out Func<string, string?, object?, object?>? fn))
        {
            return (T?)fn.Invoke(jsonData, postData, requestObject);
        }
    
        return type switch
        {
            _ when type == typeof(ChatResult) => 
                (T?)(dynamic)ChatResult.Deserialize(LLmProviders.Anthropic, jsonData, postData, requestObject),
            _ when type == typeof(TokenizeResult) => 
                (T?)(dynamic)TokenizeResult.Deserialize(LLmProviders.Anthropic, jsonData, postData),
            _ when type == typeof(BatchResult) => 
                (T?)(dynamic)BatchResult.Deserialize(LLmProviders.Anthropic, jsonData),
            _ when type == typeof(Compaction.CompactionResult) =>
                (T?)(dynamic)Compaction.CompactionResult.Deserialize(LLmProviders.Anthropic, jsonData, postData, requestObject),
            _ => JsonConvert.DeserializeObject<T>(jsonData)
        };
    }
    
    public override object? InboundMessage(Type type, string jsonData, string? postData, object? requestObject)
    {
        if (InboundMap.TryGetValue(type, out Func<string, string?, object?, object?>? fn))
        {
            return fn.Invoke(jsonData, postData, requestObject);
        }

        return JsonConvert.DeserializeObject(jsonData, type);
    }
}
