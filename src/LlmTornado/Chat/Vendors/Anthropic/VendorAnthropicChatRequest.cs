using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Chat.Vendors.Anthropic;

public interface IChatMessagePartVendorExtensions
{
    
}

/// <summary>
///     Tool functionality specific to a vendor
/// </summary>
public interface IToolVendorExtensions
{
    
}

public class AnthropicToolVendorExtensions : IToolVendorExtensions
{
    [JsonProperty("cache_control")]
    public AnthropicCacheSettings? Cache { get; set; } 
}

public class ToolVendorExtensions
{
    public AnthropicToolVendorExtensions? Anthropic { get; set; }
    
    public ToolVendorExtensions(AnthropicToolVendorExtensions anthropic)
    {
        Anthropic = anthropic;
    }
}

/// <summary>
/// Anthropic extensions to chat message parts.
/// </summary>
public class ChatMessagePartAnthropicExtensions : IChatMessagePartVendorExtensions
{
    /// <summary>
    /// Cache settings.
    /// </summary>
    public AnthropicCacheSettings? Cache { get; set; }

    /// <summary>
    /// Parsed advisor tool result payload when the part type is <c>advisor_tool_result</c>.
    /// </summary>
    public AnthropicAdvisorToolResultData? AdvisorToolResult { get; set; }
}

public partial class VendorAnthropicChatRequestMessageContent
{
    [JsonIgnore]
    internal ChatMessage Msg { get; set; }
    
    public List<ChatMessagePart> Parts { get; internal set; }
    public ChatMessageRoles Role { get; internal set; }
    
    public VendorAnthropicChatRequestMessageContent(ChatMessage msg)
    {
        Msg = msg;
        Parts = msg.Parts?.Count > 0 ? msg.Parts.ToList() : msg.Content is not null ? [
            new ChatMessagePart(msg.Content)
        ] : [];
        Role = msg.Role ?? ChatMessageRoles.Unknown;
    }

    public VendorAnthropicChatRequestMessageContent()
    {
        
    }

    internal partial class VendorAnthropicChatRequestMessageContentJsonConverter : JsonConverter<VendorAnthropicChatRequestMessageContent>
    {
        public override void WriteJson(JsonWriter writer, VendorAnthropicChatRequestMessageContent value, JsonSerializer serializer)
        {
            if (value.Msg.ChatMessageSerializeData is VendorAnthropicChatMessageToolResults vd)
            {
                writer.WriteStartArray();
                
                foreach (VendorAnthropicChatMessageToolResult block in vd.ToolResults)
                {
                    if (block.ToolCallId?.Length is 0)
                    {
                        continue;
                    }
                    
                    writer.WriteStartObject();
                    
                    writer.WritePropertyName("type");
                    writer.WriteValue("tool_result");
                    
                    writer.WritePropertyName("tool_use_id");
                    writer.WriteValue(block.ToolCallId);
                    
                    if (block.SourceMessage.FunctionCall?.Result?.RawContentBlocks is not null)
                    {
                        writer.WritePropertyName("content");    
                        writer.WriteStartArray();

                        foreach (IFunctionResultBlock resultBlock in block.SourceMessage.FunctionCall.Result.RawContentBlocks)
                        {
                            writer.Serialize(resultBlock);
                        }
                        
                        writer.WriteEndArray();
                    }
                    else if (block.Content is not null)
                    {
                        writer.WritePropertyName("content");
                        
                        if (block.Content.TrimStart().StartsWith('"'))
                        {
                            writer.WriteRawValue(block.Content);
                        }
                        else
                        {
                            writer.WriteValue(block.Content);   
                        }     
                    }

                    switch (block.ToolInvocationSucceeded)
                    {
                        case false:
                        {
                            writer.WritePropertyName("is_error");
                            writer.WriteValue(true);
                            break;
                        }
                        case true:
                        {
                            writer.WritePropertyName("is_error");
                            writer.WriteValue(false);
                            break;
                        }
                    }

                    // A cache breakpoint set on the tool-result message's parts belongs on the tool_result block
                    // itself: the API accepts cache_control there, and dropping it loses the incremental
                    // history breakpoint whenever the conversation tail is a tool result.
                    ChatMessagePart? cachePart = block.SourceMessage.Parts?.LastOrDefault(p => p.VendorExtensions is ChatMessagePartAnthropicExtensions { Cache: not null });

                    if (cachePart is not null)
                    {
                        SerializeCache(cachePart);
                    }
                
                    writer.WriteEndObject();
                }
                
                writer.WriteEndArray();
            }
            else if (value.Parts.Count > 0)
            {
                writer.WriteStartArray();
                HashSet<string> positionedToolCalls = [];
                
                foreach (ChatMessagePart part in value.Parts)
                {
                    if (part.Type == ChatMessageTypes.AnthropicToolUse)
                    {
                        ToolCall? call = value.Msg.ToolCalls?.FirstOrDefault(x => x.Id == part.AnthropicToolUseId);
                        if (value.Role != ChatMessageRoles.Assistant || part.AnthropicToolUseId is null || call?.FunctionCall is null || !positionedToolCalls.Add(part.AnthropicToolUseId))
                            throw new ArgumentException("An Anthropic tool-use position requires a unique matching function call in an assistant message.");
                        SerializeToolCall(writer, call);
                        continue;
                    }
                    writer.WriteStartObject();
                    
                    string? type = part.Type switch
                    {
                        ChatMessageTypes.Text => "text",
                        ChatMessageTypes.Image => "image",
                        _ => null
                    };

                    if (type is not null)
                    {
                        writer.WritePropertyName("type");
                        writer.WriteValue(type);
                    }
                    
                    switch (part.Type)
                    {
                        case ChatMessageTypes.AnthropicToolChange:
                        {
                            if (value.Msg.Role != ChatMessageRoles.System)
                                throw new ArgumentException("Anthropic tool changes require a mid-conversation system message.");
                            AnthropicToolChange change = part.AnthropicToolChange ?? throw new ArgumentException("Tool change data is required.");
                            change.Validate();
                            writer.WritePropertyName("type");
                            writer.WriteValue(change.Type);
                            writer.WritePropertyName("tool");
                            serializer.Serialize(writer, change.Tool);
                            break;
                        }
                        case ChatMessageTypes.AnthropicMcpToolListing:
                        {
                            AnthropicMcpToolListing listing = part.AnthropicMcpToolListing ?? throw new ArgumentException("MCP listing data is required.");
                            writer.WritePropertyName("type");
                            writer.WriteValue(listing.Type);
                            writer.WritePropertyName("mcp_server_name");
                            writer.WriteValue(listing.McpServerName);
                            writer.WritePropertyName("tools");
                            serializer.Serialize(writer, listing.Tools);
                            break;
                        }
                        case ChatMessageTypes.Text:
                        {
                            writer.WritePropertyName("text");
                            writer.WriteValue(part.Text);
                         
                            if (part.Citations?.Count > 0)
                            {
                                writer.WritePropertyName("citations");
                                writer.WriteStartArray();
                                
                                foreach (IChatMessagePartCitation cit in part.Citations)
                                {
                                    cit.Serialize(LLmProviders.Anthropic, writer);
                                }
                                
                                writer.WriteEndArray();
                            }
                            break;
                        }
                        case ChatMessageTypes.Image:
                        {
                            if (part.Image is null)
                            {
                                throw new Exception("Image property of ChatMessagePart is null and cannot be encoded.");
                            }

                            writer.WritePropertyName("source");
                            writer.WriteStartObject();

                            bool dataPrefix = part.Image.Url.StartsWith("data:");
                            
                            if (dataPrefix || !Uri.TryCreate(part.Image.Url, UriKind.Absolute, out _))
                            {
                                if (part.Image.MimeType is null)
                                {
                                    throw new Exception("MIME type of the image must be set, supported values for Anthropic are: image/jpeg, image/png, image/gif, image/webp");
                                }

                                // anthropic expects bare64, remove the prefix
                                string img = part.Image.Url;
                                
                                if (dataPrefix)
                                {
#if MODERN
                                    img = Base64HeaderRegex().Replace(img, string.Empty, 1);

#else
                                    img = Base64HeaderRegex.Replace(img, string.Empty, 1);
#endif
                                    
                                }
                        
                                writer.WritePropertyName("type");
                                writer.WriteValue("base64");
                        
                                writer.WritePropertyName("media_type");
                                writer.WriteValue(part.Image.MimeType);
                            
                                writer.WritePropertyName("data");
                                writer.WriteValue(img);
                            }
                            else
                            {
                                writer.WritePropertyName("type");
                                writer.WriteValue("url");
     
                                writer.WritePropertyName("url");
                                writer.WriteValue(part.Image.Url);   
                            }
                        
                            writer.WriteEndObject();
                            break;
                        }
                        case ChatMessageTypes.Reasoning:
                        {
                            if (part.Reasoning is not null)
                            {
                                if (part.Reasoning.Content is not null)
                                {
                                    writer.WritePropertyName("type");
                                    writer.WriteValue("thinking");
                            
                                    writer.WritePropertyName("thinking");
                                    writer.WriteValue(part.Reasoning.Content);
                            
                                    writer.WritePropertyName("signature");
                                }
                                else
                                {
                                    writer.WritePropertyName("type");
                                    writer.WriteValue("redacted_thinking");
                                    
                                    writer.WritePropertyName("data");
                                }

                                writer.WriteValue(part.Reasoning.Signature);
                            }

                            break;
                        }
                        case ChatMessageTypes.Compaction:
                        {
                            writer.WritePropertyName("type");
                            writer.WriteValue("compaction");
                            
                            writer.WritePropertyName("content");
                            writer.WriteValue(part.Text ?? string.Empty);
                            if (part.CompactionSignature is not null)
                            {
                                writer.WritePropertyName("signature");
                                writer.WriteValue(part.CompactionSignature);
                            }
                            if (part.EncryptedContent is not null)
                            {
                                writer.WritePropertyName("encrypted_content");
                                writer.WriteValue(part.EncryptedContent);
                            }
                            SerializeCache(part);

                            break;
                        }
                        case ChatMessageTypes.Document:
                        {
                            if (part.Document is not null)
                            {
                                bool ok = part.Document.Base64 is not null || part.Document.Uri is not null;

                                if (!ok)
                                {
                                    break;
                                }
                                
                                writer.WritePropertyName("type");
                                writer.WriteValue("document");
                                
                                writer.WritePropertyName("source");
                                writer.WriteStartObject();
                                
                                if (part.Document.Base64 is not null)
                                {
                                    writer.WritePropertyName("media_type");
                                    writer.WriteValue("application/pdf");
                                    
                                    writer.WritePropertyName("type");
                                    writer.WriteValue("base64");
                                    
                                    writer.WritePropertyName("data");
                                    writer.WriteValue(part.Document.Base64);
                                }
                                else if (part.Document.Uri is not null)
                                {
                                    writer.WritePropertyName("type");
                                    writer.WriteValue("url");
                                    
                                    writer.WritePropertyName("url");
                                    writer.WriteValue(part.Document.Uri);
                                }
                                
                                writer.WriteEndObject();
                            }

                            break;
                        }
                        case ChatMessageTypes.FileLink:
                        {
                            writer.WritePropertyName("type");
                            writer.WriteValue("document");
                            
                            writer.WritePropertyName("source");
                            writer.WriteStartObject();
                            
                            writer.WritePropertyName("type");
                            writer.WriteValue("file");
                            
                            writer.WritePropertyName("file_id");
                            writer.WriteValue(part.FileLinkData?.File?.Id ?? part.FileLinkData?.FileUri);
                            
                            writer.WriteEndObject();
                            
                            break;
                        }
                        case ChatMessageTypes.ContainerUpload:
                        {
                            writer.WritePropertyName("type");
                            writer.WriteValue("container_upload");
                            
                            writer.WritePropertyName("file_id");
                            writer.WriteValue(part.ContainerUploadData?.FileId);
                            
                            if (part.ContainerUploadData?.Cache is not null)
                            {
                                writer.WritePropertyName("cache_control");
                                JToken cacheToken = JToken.FromObject(part.ContainerUploadData.Cache);
                                cacheToken.WriteTo(writer);
                            }
                            
                            break;
                        }
                        case ChatMessageTypes.SearchResult:
                        {
                            writer.WritePropertyName("type");
                            writer.WriteValue("search_result");

                            if (part.SearchResult is null)
                            {
                                throw new Exception("SearchResult of this part is empty, expected not null");
                            }
                            
                            writer.WritePropertyName("source");
                            writer.WriteValue(part.SearchResult.Source);
                            
                            writer.WritePropertyName("title");
                            writer.WriteValue(part.SearchResult.Title);
                            
                            writer.WritePropertyName("content");
                            writer.WriteStartArray();

                            foreach (ChatSearchResultContent item in part.SearchResult.Content)
                            {
                                writer.Serialize(item);
                            }
                            
                            writer.WriteEndArray();

                            if (part.SearchResult.Citations is not null)
                            {
                                writer.WritePropertyName("citations");
                                writer.WriteStartObject();
                                
                                writer.WritePropertyName("enabled");
                                writer.WriteValue(part.SearchResult.Citations.Enabled);
                                
                                writer.WriteEndObject();
                            }

                            if (part.SearchResult.Cache is not null)
                            {
                                writer.WritePropertyName("cache_control");
                                JToken cacheToken = JToken.FromObject(part.SearchResult.Cache);
                                cacheToken.WriteTo(writer);
                            }
                            
                            break;
                        }
                    }
                    
                    SerializeCache(part);
                    
                    writer.WriteEndObject();
                }

                if (value.Msg.ToolCalls?.Count > 0)
                {
                    foreach (ToolCall toolCall in value.Msg.ToolCalls)
                    {
                        if (toolCall.Id is null || !positionedToolCalls.Contains(toolCall.Id)) SerializeToolCall(writer, toolCall);
                    }
                }
                
                writer.WriteEndArray();
            }
            else if (value.Msg.ToolCallId is not null)
            {
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WritePropertyName("type");
                writer.WriteValue("tool_result");
                writer.WritePropertyName("tool_use_id");
                writer.WriteValue(value.Msg.ToolCallId);
                writer.WritePropertyName("content");
                writer.WriteRawValue(value.Msg.Content);

                if (value.Msg.ToolInvocationSucceeded is false)
                {
                    writer.WritePropertyName("is_error");
                    writer.WriteValue(true);
                } 
                
                writer.WriteEndObject();
                writer.WriteEndArray();
            }
            else if (value.Msg.ToolCalls?.Count > 0)
            {
                writer.WriteStartArray();
                
                if (value.Msg.Content is not null)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("text");
                    writer.WritePropertyName("text");
                    writer.WriteValue(value.Msg.Content);
                    writer.WriteEndObject();
                }

                foreach (ToolCall toolCall in value.Msg.ToolCalls)
                {
                    SerializeToolCall(writer, toolCall);
                }
                
                writer.WriteEndArray();
            }
            
            void SerializeCache(ChatMessagePart part)
            {
                if (part.VendorExtensions is ChatMessagePartAnthropicExtensions { Cache: not null } ac)
                {
                    writer.WritePropertyName("cache_control");
                    JToken cacheToken = JToken.FromObject(ac.Cache);
                    cacheToken.WriteTo(writer);
                }
            }
        }

        void SerializeToolCall(JsonWriter writer, ToolCall toolCall)
        {
            if (toolCall.FunctionCall is null)
            {
                return;
            }
            
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteValue("tool_use");
            writer.WritePropertyName("id");
            writer.WriteValue(toolCall.Id);
            writer.WritePropertyName("name");
            writer.WriteValue(toolCall.FunctionCall.Name);
            writer.WritePropertyName("input");
            writer.WriteRawValue(toolCall.FunctionCall.Arguments.IsNullOrWhiteSpace() ? "{}" : toolCall.FunctionCall.Arguments);
            writer.WriteEndObject();
        }

        public override VendorAnthropicChatRequestMessageContent ReadJson(JsonReader reader, Type objectType, VendorAnthropicChatRequestMessageContent existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return new VendorAnthropicChatRequestMessageContent();
        }

#if !MODERN
        private static readonly Regex Base64HeaderRegex = new Regex(@"^data:image\/[a-zA-Z]+;base64,", RegexOptions.Compiled);
#else
        [GeneratedRegex(@"^data:image\/[a-zA-Z]+;base64,", RegexOptions.Compiled)]
        private static partial Regex Base64HeaderRegex();
#endif

    }
}

internal class VendorAnthropicChatRequestMessage
{
    [JsonProperty("role")]
    internal string Role { get; set; }
    
    [JsonProperty("content")]
    [JsonConverter(typeof(VendorAnthropicChatRequestMessageContent.VendorAnthropicChatRequestMessageContentJsonConverter))]
    public VendorAnthropicChatRequestMessageContent Content { get; set; }

    [JsonProperty("output_config")]
    public VendorAnthropicChatRequest.VendorAnthropicChatRequestOutputConfig? OutputConfig { get; set; }

    [JsonProperty("clear_at")]
    [JsonConverter(typeof(StringEnumConverter))]
    public AnthropicSystemClearAt? ClearAt { get; set; }
        
    public VendorAnthropicChatRequestMessage(ChatMessageRoles role, ChatMessage msg)
    {
        Role = ChatMessageRolesCls.MemberToString(role) ?? "user";
        Content = new VendorAnthropicChatRequestMessageContent(msg);

        if (msg.VendorExtensions is ChatMessageVendorExtensionsAnthropic anthropic)
        {
            if (anthropic.Effort is AnthropicEffortLevels effort)
            {
                OutputConfig = new VendorAnthropicChatRequest.VendorAnthropicChatRequestOutputConfig
                {
                    Effort = AnthropicEffortHelper.ToApiValue(effort)
                };
            }

            ClearAt = anthropic.ClearAt;
        }
    }
}

internal class VendorAnthropicChatRequest
{
    internal static readonly Dictionary<OutboundToolChoiceModes, string> ToolChoiceMap = new Dictionary<OutboundToolChoiceModes, string>(5)
    {
        { OutboundToolChoiceModes.Auto, "auto" },
        { OutboundToolChoiceModes.Legacy, "auto" },
        { OutboundToolChoiceModes.None, "none" },
        { OutboundToolChoiceModes.Required, "any" },
        { OutboundToolChoiceModes.ToolFunction, "tool" }
    };

    internal class VendorAnthropicChatRequestMetadata
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; }
    }

    internal class VendorAnthropicChatRequestToolChoice
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("name")]
        public string? Name { get; set; }
    }

    internal class VendorAnthropicThinkingSettings
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("budget_tokens")]
        public int? BudgetTokens { get; set; }
        [JsonProperty("display")]
        [JsonConverter(typeof(StringEnumConverter))]
        public AnthropicThinkingDisplay? Display { get; set; }

        [JsonProperty("block_binding")]
        public AnthropicThinkingBlockBinding? BlockBinding { get; set; }
    }
    
    internal class VendorAnthropicChatRequestOutputConfigFormat
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("schema")]
        public object? Schema { get; set; }
    }
    
    internal class VendorAnthropicChatRequestOutputConfig
    {
        [JsonProperty("effort")]
        public string? Effort { get; set; }
        
        [JsonProperty("format")]
        public VendorAnthropicChatRequestOutputConfigFormat? Format { get; set; }
        
        [JsonProperty("task_budget")]
        public AnthropicTaskBudget? TaskBudget { get; set; }
    }
    
    [JsonProperty("messages")]
    public List<VendorAnthropicChatRequestMessage> Messages { get; set; }
    [JsonProperty("model")]
    public string Model { get; set; }
    [JsonProperty("system", Order = -2)]
    [JsonConverter(typeof(VendorAnthropicChatRequestMessageContent.VendorAnthropicChatRequestMessageContentJsonConverter))]
    public VendorAnthropicChatRequestMessageContent? System { get; set; }
    [JsonProperty("max_tokens")]
    public int MaxTokens { get; set; }
    [JsonProperty("metadata")]
    public VendorAnthropicChatRequestMetadata? Metadata { get; set; }
    [JsonProperty("stop_seqences")]
    public List<string>? StopSequences { get; set; }
    [JsonProperty("stream")]
    public bool? Stream { get; set; }
    [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
    public double? Temperature { get; set; }
    [JsonProperty("top_p", NullValueHandling = NullValueHandling.Ignore)]
    public double? TopP { get; set; }
    [JsonProperty("top_k", NullValueHandling = NullValueHandling.Ignore)]
    public int? TopK { get; set; }
    [JsonProperty("thinking")]
    public VendorAnthropicThinkingSettings? Thinking { get; set; }
    [JsonProperty("tool_choice")]
    public VendorAnthropicChatRequestToolChoice? ToolChoice { get; set; }
    [JsonProperty("tools")]
    [JsonConverter(typeof(VendorAnthropicToolsJsonConverter))]
    public List<IVendorAnthropicChatRequestTool>? Tools { get; set; }

    [JsonProperty("container")]
    public AnthropicContainer? Container { get; set; }

    [JsonProperty("mcp_servers")]
    public AnthropicMcpServer[]? McpServers { get; set; }
    
    [JsonProperty("output_config")]
    public VendorAnthropicChatRequestOutputConfig? OutputConfig { get; set; }
    
    [JsonProperty("context_management")]
    public AnthropicContextManagement? ContextManagement { get; set; }
    
    [JsonProperty("inference_geo")]
    [JsonConverter(typeof(StringEnumConverter))]
    public AnthropicInferenceGeoOptions? InferenceGeo { get; set; }
    
    [JsonProperty("service_tier")]
    [JsonConverter(typeof(StringEnumConverter))]
    public ChatRequestServiceTiers? ServiceTier { get; set; }
    
    [JsonProperty("speed")]
    [JsonConverter(typeof(StringEnumConverter))]
    public ChatRequestSpeeds? Speed { get; set; }

    [JsonProperty("cache_control")]
    public AnthropicCacheSettings? CacheControl { get; set; }
    
    [JsonProperty("diagnostics")]
    public AnthropicCacheDiagnosticsRequest? Diagnostics { get; set; }

    [JsonProperty("compaction", NullValueHandling = NullValueHandling.Ignore)]
    public AnthropicOnDemandCompaction? Compaction { get; set; }


    [JsonProperty("fallbacks")]
    public AnthropicFallbacks? Fallbacks { get; set; }
    
    public VendorAnthropicChatRequest(ChatRequest request, IEndpointProvider provider)
    {
        Model = request.Model?.Name ?? ChatModel.Anthropic.Claude5.Sonnet.Name;
        MaxTokens = request.MaxTokens ?? 1024;
        StopSequences = request.MultipleStopSequences?.ToList();
        Stream = request.Stream;
        Temperature = ChatModelAnthropicHelper.RejectsNonDefaultSamplingParams(Model) ? null : request.Temperature;
        TopP = ChatModelAnthropicHelper.RejectsNonDefaultSamplingParams(Model) ? null : request.TopP;
        TopK = ChatModelAnthropicHelper.RejectsNonDefaultSamplingParams(Model) ? null : request.TopK;
        Messages = [];
        System = null;
        
        if (request.Messages is not null)
        {
            ChatMessage? toolsMessage = null;
            
            foreach (ChatMessage msg in request.Messages)
            {
                switch (msg.Role)
                {
                    case ChatMessageRoles.Assistant or ChatMessageRoles.User:
                    {
                        if (toolsMessage is not null)
                        {
                            Messages.Add(new VendorAnthropicChatRequestMessage(ChatMessageRoles.User, toolsMessage));
                        }
                        
                        Messages.Add(new VendorAnthropicChatRequestMessage(msg.Role.Value, msg));
                        toolsMessage = null;
                        break;
                    }
                    case ChatMessageRoles.Tool: // multiple tool messages must be compressed into one
                    {
                        if (toolsMessage is null)
                        {
                            toolsMessage = msg;
                            toolsMessage.ChatMessageSerializeData = new VendorAnthropicChatMessageToolResults
                            {
                                ToolResults =
                                [
                                    new VendorAnthropicChatMessageToolResult(msg)
                                ]
                            };
                            
                            continue;
                        }

                        if (toolsMessage.ChatMessageSerializeData is VendorAnthropicChatMessageToolResults vd)
                        {
                            vd.ToolResults.Add(new VendorAnthropicChatMessageToolResult(msg));
                        }
                        
                        break;
                    }
                    case ChatMessageRoles.System:
                    {
                        if (Messages.Count == 0 && msg.Parts?.Any(p => p.AnthropicToolChange is not null) == true)
                            throw new ArgumentException("Tool changes must follow an existing conversation turn.");
                        if (System is null && Messages.Count == 0)
                        {
                            System = new VendorAnthropicChatRequestMessageContent(msg);
                        }
                        else
                        {
                            Messages.Add(new VendorAnthropicChatRequestMessage(ChatMessageRoles.System, msg));
                        }
                        break;
                    }
                }
            } 
            
            if (toolsMessage is not null)
            {
                Messages.Add(new VendorAnthropicChatRequestMessage(ChatMessageRoles.User, toolsMessage));
                toolsMessage = null;
            }
        }

        if (request.ToolChoice is not null)
        {
            string toolChoiceType = ToolChoiceMap.GetValueOrDefault(request.ToolChoice.Mode) ?? "auto";
            if (ChatModelAnthropicHelper.RejectsForcedToolChoice(Model)
                && toolChoiceType is "any" or "tool")
            {
                toolChoiceType = "auto";
            }

            ToolChoice = new VendorAnthropicChatRequestToolChoice
            {
                Type = toolChoiceType,
                Name = toolChoiceType is "tool" ? request.ToolChoice.Function?.Name : null
            };
        }

        // Set ReasoningBudget to 0 for Extended thinking models when ToolChoice is ToolFunction, otherwise the error is thrown.
        // https://platform.claude.com/docs/en/build-with-claude/extended-thinking#extended-thinking-with-tool-use
        // For correct reasoning with tools: 1) Tool choice has to be Auto & 2) Thinking blocks has to be sent back to the API (unmodified).
        if (IsExtendedThinkingModel(Model) && request.ToolChoice?.Mode == OutboundToolChoiceModes.ToolFunction)
        {
            request.ReasoningBudget = 0;
        }

        if (request.Tools is not null)
        {
            Tools = request.Tools
                .Where(x => x.Function is not null)
                .Select(t => (IVendorAnthropicChatRequestTool)new VendorAnthropicToolFunction(t))
                .ToList();
        }
        
        if (request.ResponseFormat?.Type is ChatRequestResponseFormatTypes.StructuredJson && request.ResponseFormat.Schema?.Schema is not null)
        {
            OutputConfig ??= new VendorAnthropicChatRequestOutputConfig();
            OutputConfig.Format = new VendorAnthropicChatRequestOutputConfigFormat
            {
                Type = "json_schema",
                Schema = request.ResponseFormat.Schema.Schema
            };
        }

        if (request.VendorExtensions?.Anthropic is not null)
        {
            if (request.VendorExtensions.Anthropic.BuiltInTools is not null)
            {
                Tools ??= [];
                Tools.AddRange(request.VendorExtensions.Anthropic.BuiltInTools
                    .Select(x => (IVendorAnthropicChatRequestTool)new VendorAnthropicToolFunction(x)));
            }

            if (request.VendorExtensions.Anthropic.AdvisorTool is not null)
            {
                Tools ??= [];
                Tools.Add((IVendorAnthropicChatRequestTool)new VendorAnthropicToolFunction(
                    request.VendorExtensions.Anthropic.AdvisorTool.ToBuiltInTool()));
            }
            
            if (request.VendorExtensions.Anthropic.Container is not null)
            {
                Container = request.VendorExtensions.Anthropic.Container;
            }

            if (request.VendorExtensions.Anthropic.McpServers is not null)
            {
                List<AnthropicMcpServer> resolvedServers = request.VendorExtensions.Anthropic.McpServers.Select(server =>
                {
                    if (string.IsNullOrWhiteSpace(server.Url))
                    {
                        server.Url = server.ResolveUrl();
                    }

                    return server;
                }).ToList();

                McpServers = resolvedServers.ToArray();

                List<AnthropicMcpToolset> toolsets = request.VendorExtensions.Anthropic.McpToolsets?.ToList() ?? [];

                foreach (AnthropicMcpServer server in resolvedServers)
                {
                    if (toolsets.Any(t => t.McpServerName == server.Name) || AnthropicConversationTools.DefinesMcpServer(request, server.Name))
                    {
                        continue;
                    }

                    toolsets.Add(AnthropicMcpToolset.FromLegacyConfiguration(server.Name, server.Configuration));
                }

                if (toolsets.Count > 0)
                {
                    Tools ??= [];
                    Tools.AddRange(toolsets);
                }
            }
            
            if (request.VendorExtensions.Anthropic.ContextManagement is not null)
            {
                ContextManagement = request.VendorExtensions.Anthropic.ContextManagement;
            }

            Compaction = request.VendorExtensions.Anthropic.Compaction;
            if (Compaction is not null && ContextManagement is not null)
            {
                throw new ArgumentException("On-demand compaction cannot be combined with context_management.");
            }

            
            if (request.VendorExtensions.Anthropic.InferenceGeo is not null)
            {
                InferenceGeo = request.VendorExtensions.Anthropic.InferenceGeo;
            }
            
            if (request.VendorExtensions.Anthropic.TaskBudget is not null)
            {
                OutputConfig ??= new VendorAnthropicChatRequestOutputConfig();
                OutputConfig.TaskBudget = request.VendorExtensions.Anthropic.TaskBudget;
            }

            if (request.VendorExtensions.Anthropic.CacheDiagnostics is not null)
            {
                Diagnostics = request.VendorExtensions.Anthropic.CacheDiagnostics;
            }

            if (request.VendorExtensions.Anthropic.Fallbacks is not null)
            {
                Fallbacks = request.VendorExtensions.Anthropic.Fallbacks;
            }

            request.VendorExtensions.Anthropic.OutboundRequest?.Invoke(
                System,
                Messages.Select(x => x.Content).ToList(),
                Tools?.OfType<VendorAnthropicToolFunction>().ToList());
        }
        
        if (request.ServiceTier is ChatRequestServiceTiers.Auto or ChatRequestServiceTiers.StandardOnly)
        {
            ServiceTier = request.ServiceTier;
        }
        
        if (request.Speed is ChatRequestSpeeds.Fast)
        {
            Speed = ChatRequestSpeeds.Fast;
        }

        if (request.AutoCache is not null)
        {
            CacheControl = request.AutoCache.Ttl.HasValue
                ? AnthropicCacheSettings.EphemeralWithTtl(request.AutoCache.Ttl.Value)
                : AnthropicCacheSettings.Ephemeral;
        }
        
        if (ChatModelAnthropicHelper.IsEffortCompatibleModel(Model))
        {
            string? effortValue = request.VendorExtensions?.Anthropic?.Effort is AnthropicEffortLevels vendorEffort
                ? AnthropicEffortHelper.ToApiValue(vendorEffort)
                : request.ReasoningEffort is ChatReasoningEfforts reasoningEffort
                    ? AnthropicEffortHelper.ToApiValue(reasoningEffort)
                    : null;

            if (effortValue is not null)
            {
                OutputConfig ??= new VendorAnthropicChatRequestOutputConfig();
                OutputConfig.Effort = effortValue;
            }
        }

        Thinking = BuildThinkingSettings(request);
        ClampDisabledThinkingEffort();
    }

    private VendorAnthropicThinkingSettings? BuildThinkingSettings(ChatRequest request)
    {
        AnthropicThinkingSettings? vendorThinking = request.VendorExtensions?.Anthropic?.Thinking;
        AnthropicThinkingTypes? mode = null;
        int? budgetTokens = null;

        if (vendorThinking is not null)
        {
            mode = vendorThinking.ResolvedType;
            budgetTokens = vendorThinking.BudgetTokens;
        }
        else if (request.ReasoningBudget is -1)
        {
            mode = AnthropicThinkingTypes.Adaptive;
        }
        else if (request.ReasoningBudget > 0)
        {
            mode = AnthropicThinkingTypes.Enabled;
            budgetTokens = request.ReasoningBudget;
        }

        if (mode is AnthropicThinkingTypes.Disabled && ChatModelAnthropicHelper.SupportsBetweenToolsThinking(Model))
        {
            mode = AnthropicThinkingTypes.BetweenTools;
        }

        if (vendorThinking?.BlockBinding is not null)
        {
            if (mode is AnthropicThinkingTypes.Disabled && ChatModelAnthropicHelper.IsAlwaysOnAdaptiveThinkingModel(Model))
                mode = AnthropicThinkingTypes.Adaptive;
            if (mode is not (AnthropicThinkingTypes.Adaptive or AnthropicThinkingTypes.Enabled))
                throw new ArgumentException("Thinking block binding requires adaptive or enabled thinking; it cannot be used with disabled or between-tools thinking.");
        }

        if (mode is AnthropicThinkingTypes.BetweenTools)
        {
            if (!ChatModelAnthropicHelper.SupportsBetweenToolsThinking(Model))
            {
                throw new ArgumentException("Between-tools thinking is supported only by Claude Sonnet 5.5.");
            }

            return new VendorAnthropicThinkingSettings { Type = "between_tools" };
        }

        if (mode is AnthropicThinkingTypes.Disabled)
        {
            if (ChatModelAnthropicHelper.IsAlwaysOnAdaptiveThinkingModel(Model)
                || !ChatModelAnthropicHelper.IsThinkingOnByDefault(Model))
            {
                return null;
            }

            return new VendorAnthropicThinkingSettings
            {
                Type = "disabled",
                Display = vendorThinking?.Display,
                BlockBinding = vendorThinking?.BlockBinding
            };
        }

        if (mode is null)
        {
            return null;
        }

        if (mode == AnthropicThinkingTypes.Enabled && RequiresAdaptiveThinkingOnly(Model))
        {
            mode = AnthropicThinkingTypes.Adaptive;
            budgetTokens = null;
        }

        VendorAnthropicThinkingSettings settings = new VendorAnthropicThinkingSettings
        {
            Type = ToApiThinkingType(mode.Value),
            BudgetTokens = mode == AnthropicThinkingTypes.Enabled ? budgetTokens : null,
            Display = vendorThinking?.Display,
            BlockBinding = vendorThinking?.BlockBinding
        };

        if (mode == AnthropicThinkingTypes.Enabled && settings.BudgetTokens is int budget && MaxTokens < budget)
        {
            MaxTokens = budget + 4_096;
        }

        return settings;
    }

    private void ClampDisabledThinkingEffort()
    {
        if (Thinking?.Type is not ("disabled" or "between_tools")
            || !ChatModelAnthropicHelper.RestrictsDisabledThinkingToHighEffortOrBelow(Model)
            || OutputConfig?.Effort is not ("xhigh" or "max"))
        {
            return;
        }

        OutputConfig.Effort = "high";
    }

    private static string ToApiThinkingType(AnthropicThinkingTypes type) => type switch
    {
        AnthropicThinkingTypes.Adaptive => "adaptive",
        AnthropicThinkingTypes.Enabled => "enabled",
        AnthropicThinkingTypes.Disabled => "disabled",
        AnthropicThinkingTypes.BetweenTools => "between_tools",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported Anthropic thinking type.")
    };

    private static bool RequiresAdaptiveThinkingOnly(string? modelName)
        => ChatModelAnthropicHelper.RequiresAdaptiveThinkingWhenEnabled(modelName);
    
    
    private static bool IsExtendedThinkingModel(string? modelName)
    {
        if (modelName is null)
        {
            return false;
        }

        return
            // Claude Sonnet 4 or Claude Sonnet 4.5
            modelName.StartsWith("claude-sonnet-4", StringComparison.OrdinalIgnoreCase)
            // Claude Haiku 4.5 
            || modelName.StartsWith("claude-haiku-4-5", StringComparison.OrdinalIgnoreCase)
            // Claude Opus 4 or Claude Opus 4.1 or Claude Opus 4.5
            || modelName.StartsWith("claude-opus-4", StringComparison.OrdinalIgnoreCase);
    }
}
