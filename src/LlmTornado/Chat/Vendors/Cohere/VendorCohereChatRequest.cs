using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Images;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Chat.Vendors.Cohere;

#region Request Model

/// <summary>
/// Represents the request payload for the Cohere Chat API (v2).
/// </summary>
internal class VendorCohereChatRequest
{
    [JsonProperty("stream")]
    public bool Stream { get; set; }

    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("messages")]
    public List<VendorCohereChatMessage> Messages { get; set; }

    [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
    public List<VendorCohereTool>? Tools { get; set; }
    
    [JsonProperty("citation_options", NullValueHandling = NullValueHandling.Ignore)]
    public VendorCohereCitationOptions? CitationOptions { get; set; }

    [JsonProperty("response_format", NullValueHandling = NullValueHandling.Ignore)]
    public VendorCohereResponseFormat? ResponseFormat { get; set; }

    [JsonProperty("safety_mode", NullValueHandling = NullValueHandling.Ignore)]
    public VendorCohereSafetyMode? SafetyMode { get; set; }

    [JsonProperty("max_tokens", NullValueHandling = NullValueHandling.Ignore)]
    public int? MaxTokens { get; set; }

    [JsonProperty("stop_sequences", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? StopSequences { get; set; }

    [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
    public double? Temperature { get; set; }

    [JsonProperty("seed", NullValueHandling = NullValueHandling.Ignore)]
    public int? Seed { get; set; }

    [JsonProperty("frequency_penalty", NullValueHandling = NullValueHandling.Ignore)]
    public double? FrequencyPenalty { get; set; }

    [JsonProperty("presence_penalty", NullValueHandling = NullValueHandling.Ignore)]
    public double? PresencePenalty { get; set; }

    [JsonProperty("k", NullValueHandling = NullValueHandling.Ignore)]
    public int? K { get; set; }

    [JsonProperty("p", NullValueHandling = NullValueHandling.Ignore)]
    public double? P { get; set; }

    [JsonProperty("logprobs", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Logprobs { get; set; }

    [JsonProperty("tool_choice", NullValueHandling = NullValueHandling.Ignore)]
    public VendorCohereToolChoice? ToolChoice { get; set; }
    
    [JsonProperty("thinking", NullValueHandling = NullValueHandling.Ignore)]
    public VendorCohereThinking? Thinking { get; set; }
    
    public JObject Serialize(JsonSerializerSettings settings)
    {
        JsonSerializer serializer = JsonSerializer.CreateDefault(settings);
        JObject jsonPayload = JObject.FromObject(this, serializer);
        return jsonPayload;
    }
    
    public VendorCohereChatRequest(ChatRequest request, IEndpointProvider provider)
    {
        Stream = request.Stream ?? false;
        Model = request.Model?.Name ?? ChatModel.Cohere.Command.A0325;
        MaxTokens = request.MaxTokens;
        Temperature = request.Temperature;
        P = request.TopP;
        FrequencyPenalty = request.FrequencyPenalty;
        PresencePenalty = request.PresencePenalty;
        StopSequences = request.MultipleStopSequences?.ToList();
        Seed = request.Seed;
        Logprobs = request.Logprobs;
        
        if (request.Tools is { Count: > 0 })
        {
            // Optimize: single loop instead of Where().Select().ToList() to avoid intermediate enumerable allocations
            List<VendorCohereTool> tools = [];
            foreach (var t in request.Tools)
            {
                if (t.Function is not null)
                {
                    tools.Add(new VendorCohereTool
                    {
                        Function = new VendorCohereFunctionDefinition
                        {
                            Name = t.Function.Name,
                            Description = t.Function.Description,
                            Parameters = JObject.FromObject(t.Function.Parameters ?? new object())
                        }
                    });
                }
            }
            Tools = tools;
        }

        if (request.ToolChoice?.Mode is not null)
        {
            ToolChoice = request.ToolChoice.Mode switch
            {
                OutboundToolChoiceModes.None => VendorCohereToolChoice.None,
                OutboundToolChoiceModes.Required => VendorCohereToolChoice.Required,
                OutboundToolChoiceModes.ToolFunction => VendorCohereToolChoice.Required,
                _ => null
            };
        }
        
        Messages = request.Messages?.Select<ChatMessage, VendorCohereChatMessage>(m =>
        {
            switch (m.Role)
            {
                case ChatMessageRoles.User:
                {
                    VendorCohereUserMessageContent content;
                    
                    if (m.Parts is { Count: > 0 })
                    {
                        List<VendorCohereContentBlock?> contentBlocks = m.Parts.Select<ChatMessagePart, VendorCohereContentBlock?>(p =>
                        {
                            return p.Type switch
                            {
                                ChatMessageTypes.Text => new VendorCohereTextContentBlock
                                {
                                    Text = p.Text ?? string.Empty
                                },
                                ChatMessageTypes.Image => new VendorCohereImageContentBlock
                                {
                                    ImageUrl = new VendorCohereImageUrl
                                    {
                                        Url = p.Image!.Url,
                                        Detail = p.Image!.Detail switch
                                        {
                                            ImageDetail.Auto => "auto",
                                            ImageDetail.High => "high",
                                            ImageDetail.Low => "low",
                                            _ => null
                                        }
                                    }
                                },
                                _ => null
                            };
                        }).Where(x => x is not null).ToList();

                        if (contentBlocks.Count == 1 && contentBlocks[0] is VendorCohereTextContentBlock textBlock)
                        {
                            content = textBlock.Text;
                        }
                        else
                        {
                            content = new VendorCohereUserMessageContent(contentBlocks!);
                        }
                    }
                    else if (m.Content is not null)
                    {
                        content = new VendorCohereUserMessageContent(m.Content);
                    }
                    else
                    {
                        content = new VendorCohereUserMessageContent(string.Empty);
                    }

                    return new VendorCohereUserChatMessage
                    {
                        Content = content
                    };
                }
                case ChatMessageRoles.Assistant:
                {
                    VendorCohereAssistantChatMessage assistantMessage = new VendorCohereAssistantChatMessage();

                    List<VendorCohereContentBlock> reasoningBlocks = [];

                    if (m.Parts is { Count: > 0 })
                    {
                        foreach (ChatMessagePart part in m.Parts)
                        {
                            if (part.Type is ChatMessageTypes.Reasoning && !string.IsNullOrWhiteSpace(part.Reasoning?.Content))
                            {
                                reasoningBlocks.Add(new VendorCohereThinkingContentBlock
                                {
                                    Thinking = part.Reasoning!.Content
                                });
                            }
                            else if (part.Type is ChatMessageTypes.Text && !part.Text.IsNullOrWhiteSpace())
                            {
                                reasoningBlocks.Add(new VendorCohereTextContentBlock
                                {
                                    Text = part.Text ?? string.Empty
                                });
                            }
                        }
                    }
                    else
                    {
                        if (!m.ReasoningContent.IsNullOrWhiteSpace())
                        {
                            reasoningBlocks.Add(new VendorCohereThinkingContentBlock
                            {
                                Thinking = m.ReasoningContent
                            });
                        }

                        if (!m.Content.IsNullOrWhiteSpace())
                        {
                            reasoningBlocks.Add(new VendorCohereTextContentBlock
                            {
                                Text = m.Content
                            });
                        }
                    }

                    if (reasoningBlocks.Count > 1 || reasoningBlocks.Exists(x => x is VendorCohereThinkingContentBlock))
                    {
                        assistantMessage.Content = new VendorCohereAssistantMessageContent(reasoningBlocks);
                    }
                    else
                    {
                        assistantMessage.Content = m.Content;
                    }

                    if (m.ToolCalls is { Count: > 0 })
                    {
                        assistantMessage.ToolCalls = m.ToolCalls.Where(x => x.FunctionCall is not null).Select(tc => new VendorCohereToolCall
                        {
                            Id = tc.Id,
                            Type = VendorCohereToolCallType.Function,
                            Function = new VendorCohereToolCallFunction
                            {
                                Name = tc.FunctionCall!.Name,
                                Arguments = tc.FunctionCall.Arguments
                            }
                        }).ToList();
                    }
                    
                    return assistantMessage;
                }
                case ChatMessageRoles.System:
                {
                    VendorCohereSystemMessageContent content;

                    if (m.Parts is { Count: > 0 })
                    {
                        content = new VendorCohereSystemMessageContent(m.Parts
                            .Where(p => p.Type == ChatMessageTypes.Text)
                            .Select<ChatMessagePart, VendorCohereTextContentBlock>(p => new VendorCohereTextContentBlock
                            {
                                Text = p.Text ?? string.Empty
                            }).ToList());
                    }
                    else
                    {
                        content = new VendorCohereSystemMessageContent(m.Content ?? string.Empty);
                    }

                    return new VendorCohereSystemChatMessage
                    {
                        Content = content
                    };
                }
                case ChatMessageRoles.Tool:
                {
                    return new VendorCohereToolChatMessage
                    {
                        ToolCallId = m.ToolCallId,
                        Content = new VendorCohereToolMessageContent(m.Content ?? string.Empty)
                    };
                }
                default:
                {
                    return null;
                }
            }
        }).Where(x => x is not null).ToList()!;
        
        ChatRequestVendorCohereExtensions? extensions = request.VendorExtensions?.Cohere;

        if (extensions is not null)
        {
            if (extensions.CitationQuality.HasValue)
            {
                CitationOptions = new VendorCohereCitationOptions
                {
                    Mode = extensions.CitationQuality.Value switch
                    {
                        ChatVendorCohereExtensionCitationQuality.Fast => VendorCohereCitationMode.Fast,
                        ChatVendorCohereExtensionCitationQuality.Accurate => VendorCohereCitationMode.Accurate,
                        ChatVendorCohereExtensionCitationQuality.Off => VendorCohereCitationMode.Off,
                        _ => throw new InvalidEnumArgumentException()
                    }
                };
            }

            if (extensions.SafetyMode.HasValue)
            {
                SafetyMode = extensions.SafetyMode.Value switch
                {
                    ChatVendorCohereExtensionSafetyMode.Contextual => VendorCohereSafetyMode.Contextual,
                    ChatVendorCohereExtensionSafetyMode.Strict => VendorCohereSafetyMode.Strict,
                    ChatVendorCohereExtensionSafetyMode.None => VendorCohereSafetyMode.Off,
                    _ => throw new InvalidEnumArgumentException()
                };
            }
        }

        Thinking = ResolveThinking(request, extensions);
    }

    private static VendorCohereThinking? ResolveThinking(ChatRequest request, ChatRequestVendorCohereExtensions? extensions)
    {
        if (extensions?.Thinking is not null)
        {
            return new VendorCohereThinking
            {
                Type = extensions.Thinking.Type switch
                {
                    ChatVendorCohereThinkingType.Enabled => "enabled",
                    ChatVendorCohereThinkingType.Disabled => "disabled",
                    _ => null
                },
                TokenBudget = extensions.Thinking.TokenBudget
            };
        }

        bool isReasoningModel = request.Model is not null && ChatModelCohere.ReasoningModels.Contains(request.Model);

        if (!isReasoningModel)
        {
            return null;
        }

        if (request.ReasoningBudget is 0 || request.ReasoningEffort is ChatReasoningEfforts.None)
        {
            return new VendorCohereThinking
            {
                Type = "disabled"
            };
        }

        if (request.ReasoningBudget is > 0)
        {
            return new VendorCohereThinking
            {
                TokenBudget = request.ReasoningBudget
            };
        }

        if (request.ReasoningEffort is not null)
        {
            return new VendorCohereThinking
            {
                Type = "enabled"
            };
        }

        return null;
    }
}

internal class VendorCohereThinking
{
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string? Type { get; set; }

    [JsonProperty("token_budget", NullValueHandling = NullValueHandling.Ignore)]
    public int? TokenBudget { get; set; }
}

#endregion

#region Messages and Content Blocks

internal abstract class VendorCohereChatMessage
{
    [JsonProperty("role")]
    public string Role { get; protected set; }
}

internal class VendorCohereUserChatMessage : VendorCohereChatMessage
{
    public VendorCohereUserChatMessage()
    {
        Role = "user";
    }
    
    [JsonProperty("content")]
    public VendorCohereUserMessageContent Content { get; set; }
}

internal class VendorCohereAssistantChatMessage : VendorCohereChatMessage
{
    public VendorCohereAssistantChatMessage() { Role = "assistant"; }
    [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
    public List<VendorCohereToolCall>? ToolCalls { get; set; }
    [JsonProperty("tool_plan", NullValueHandling = NullValueHandling.Ignore)]
    public string? ToolPlan { get; set; }
    [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
    public VendorCohereAssistantMessageContent? Content { get; set; }
}

internal class VendorCohereSystemChatMessage : VendorCohereChatMessage
{
    public VendorCohereSystemChatMessage() { Role = "system"; }
    [JsonProperty("content")]
    public VendorCohereSystemMessageContent Content { get; set; }
}

internal class VendorCohereToolChatMessage : VendorCohereChatMessage
{
    public VendorCohereToolChatMessage() { Role = "tool"; }
    [JsonProperty("tool_call_id")]
    public string ToolCallId { get; set; }
    [JsonProperty("content")]
    public VendorCohereToolMessageContent Content { get; set; }
}

[JsonConverter(typeof(VendorCohereContentBlockConverter))]
internal abstract class VendorCohereContentBlock
{
    [JsonProperty("type")]
    public string Type { get; protected set; }
}

internal class VendorCohereTextContentBlock : VendorCohereContentBlock
{
    public VendorCohereTextContentBlock()
    {
        Type = "text";
    }
    
    [JsonProperty("text")]
    public string Text { get; set; }
}

internal class VendorCohereImageContentBlock : VendorCohereContentBlock
{
    public VendorCohereImageContentBlock()
    {
        Type = "image_url";
    }
    
    [JsonProperty("image_url")]
    public VendorCohereImageUrl ImageUrl { get; set; }
}

internal class VendorCohereThinkingContentBlock : VendorCohereContentBlock
{
    public VendorCohereThinkingContentBlock()
    {
        Type = "thinking";
    }
    
    [JsonProperty("thinking")]
    public string Thinking { get; set; }
}

internal class VendorCohereDocumentContentBlock : VendorCohereContentBlock
{
    public VendorCohereDocumentContentBlock()
    {
        Type = "document";
    }
    
    [JsonProperty("document")]
    public VendorCohereDocument Document { get; set; }
}

internal class VendorCohereDocument
{
    [JsonProperty("data")]
    public Dictionary<string, object> Data { get; set; }
    [JsonProperty("id")]
    public string Id { get; set; }
}

internal class VendorCohereImageUrl
{
    [JsonProperty("url")]
    public string Url { get; set; }
    [JsonProperty("detail", NullValueHandling = NullValueHandling.Ignore)]
    public string? Detail { get; set; }
}

#endregion

#region Tools and Functions

[JsonConverter(typeof(StringEnumConverter))]
internal enum VendorCohereToolCallType
{
    [EnumMember(Value = "function")] Function
}

internal class VendorCohereToolCall
{
    [JsonProperty("id")]
    public string Id { get; set; }
    [JsonProperty("type")]
    public VendorCohereToolCallType Type { get; set; }
    [JsonProperty("function")]
    public VendorCohereToolCallFunction Function { get; set; }
}

internal class VendorCohereToolCallFunction
{
    [JsonProperty("name")]
    public string Name { get; set; }
    [JsonProperty("arguments")]
    public string Arguments { get; set; }
}

internal class VendorCohereTool
{
    [JsonProperty("type")]
    public string Type { get; } = "function";
    [JsonProperty("function")]
    public VendorCohereFunctionDefinition Function { get; set; }
}

internal class VendorCohereFunctionDefinition
{
    [JsonProperty("name")]
    public string Name { get; set; }
    [JsonProperty("parameters")]
    public JObject Parameters { get; set; }
    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }
}

#endregion

#region Other Options

[JsonConverter(typeof(StringEnumConverter))]
internal enum VendorCohereSafetyMode
{
    [EnumMember(Value = "CONTEXTUAL")] Contextual,
    [EnumMember(Value = "STRICT")] Strict,
    [EnumMember(Value = "OFF")] Off
}

[JsonConverter(typeof(StringEnumConverter))]
internal enum VendorCohereToolChoice
{
    [EnumMember(Value = "REQUIRED")] Required,
    [EnumMember(Value = "NONE")] None
}

internal class VendorCohereCitationOptions
{
    [JsonProperty("mode")]
    public VendorCohereCitationMode Mode { get; set; }
}

[JsonConverter(typeof(StringEnumConverter))]
internal enum VendorCohereCitationMode
{
    [EnumMember(Value = "FAST")] Fast,
    [EnumMember(Value = "ACCURATE")] Accurate,
    [EnumMember(Value = "OFF")] Off
}

[JsonConverter(typeof(VendorCohereResponseFormatConverter))]
internal abstract class VendorCohereResponseFormat
{
    [JsonProperty("type")]
    public string Type { get; protected set; }
}

internal class VendorCohereTextResponseFormat : VendorCohereResponseFormat
{
    public VendorCohereTextResponseFormat() { Type = "text"; }
}

internal class VendorCohereJsonObjectResponseFormat : VendorCohereResponseFormat
{
    public VendorCohereJsonObjectResponseFormat() { Type = "json_object"; }
    [JsonProperty("json_schema", NullValueHandling = NullValueHandling.Ignore)]
    public JObject? JsonSchema { get; set; }
}

#endregion

#region Converters

internal class VendorCohereChatMessageConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(VendorCohereChatMessage).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JObject item = JObject.Load(reader);
        string role = item["role"]?.Value<string>();

        VendorCohereChatMessage message = role switch
        {
            "user" => new VendorCohereUserChatMessage(),
            "assistant" => new VendorCohereAssistantChatMessage(),
            "system" => new VendorCohereSystemChatMessage(),
            "tool" => new VendorCohereToolChatMessage(),
            _ => throw new NotSupportedException($"Unsupported message role: {role}")
        };

        serializer.Populate(item.CreateReader(), message);
        return message;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value, value.GetType());
    }
}

internal class VendorCohereResponseFormatConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(VendorCohereResponseFormat).IsAssignableFrom(objectType);
    }
    
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JObject item = JObject.Load(reader);
        string type = item["type"]?.Value<string>();

        VendorCohereResponseFormat format = type switch
        {
            "text" => new VendorCohereTextResponseFormat(),
            "json_object" => new VendorCohereJsonObjectResponseFormat(),
            _ => throw new NotSupportedException($"Unsupported response format type: {type}")
        };
        
        serializer.Populate(item.CreateReader(), format);
        return format;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value, value.GetType());
    }
}

internal class VendorCohereContentBlockConverter : JsonConverter<VendorCohereContentBlock>
{
    public override VendorCohereContentBlock ReadJson(JsonReader reader, Type objectType, VendorCohereContentBlock? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject item = JObject.Load(reader);
        string type = item["type"]?.Value<string>();

        VendorCohereContentBlock block = type switch
        {
            "text" => new VendorCohereTextContentBlock(),
            "image_url" => new VendorCohereImageContentBlock(),
            "thinking" => new VendorCohereThinkingContentBlock(),
            "document" => new VendorCohereDocumentContentBlock(),
            _ => throw new NotSupportedException($"Unsupported content block type: {type}")
        };
        
        serializer.Populate(item.CreateReader(), block);
        return block;
    }

    public override void WriteJson(JsonWriter writer, VendorCohereContentBlock? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        JObject jo = new JObject
        {
            { "type", value.Type }
        };

        switch (value)
        {
            case VendorCohereTextContentBlock textBlock:
                jo.Add("text", textBlock.Text);
                break;
            case VendorCohereImageContentBlock imageBlock:
                jo.Add("image_url", JObject.FromObject(imageBlock.ImageUrl, serializer));
                break;
            case VendorCohereDocumentContentBlock docBlock:
                jo.Add("document", JObject.FromObject(docBlock.Document, serializer));
                break;
        }

        jo.WriteTo(writer);
    }
}

[JsonConverter(typeof(VendorCohereUserMessageContentConverter))]
internal class VendorCohereUserMessageContent
{
    public string? StringContent { get; }
    public List<VendorCohereContentBlock>? BlocksContent { get; }

    public VendorCohereUserMessageContent(string content)
    {
        StringContent = content;
    }

    public VendorCohereUserMessageContent(List<VendorCohereContentBlock> blocks)
    {
        BlocksContent = blocks;
    }

    public static implicit operator VendorCohereUserMessageContent(string content) => new(content);
    public static implicit operator VendorCohereUserMessageContent(List<VendorCohereContentBlock> blocks) => new(blocks);
}

[JsonConverter(typeof(VendorCohereAssistantMessageContentConverter))]
internal class VendorCohereAssistantMessageContent
{
    public string? StringContent { get; }
    public List<VendorCohereContentBlock>? BlocksContent { get; }

    public VendorCohereAssistantMessageContent(string content)
    {
        StringContent = content;
    }

    public VendorCohereAssistantMessageContent(List<VendorCohereContentBlock> blocks)
    {
        BlocksContent = blocks;
    }

    public static implicit operator VendorCohereAssistantMessageContent(string content) => new(content);
    public static implicit operator VendorCohereAssistantMessageContent(List<VendorCohereContentBlock> blocks) => new(blocks);
}

[JsonConverter(typeof(VendorCohereSystemMessageContentConverter))]
internal class VendorCohereSystemMessageContent
{
    public string? StringContent { get; }
    public List<VendorCohereTextContentBlock>? BlocksContent { get; }

    public VendorCohereSystemMessageContent(string content)
    {
        StringContent = content;
    }

    public VendorCohereSystemMessageContent(List<VendorCohereTextContentBlock> blocks)
    {
        BlocksContent = blocks;
    }

    public static implicit operator VendorCohereSystemMessageContent(string content) => new(content);
    public static implicit operator VendorCohereSystemMessageContent(List<VendorCohereTextContentBlock> blocks) => new(blocks);
}

[JsonConverter(typeof(VendorCohereToolMessageContentConverter))]
internal class VendorCohereToolMessageContent
{
    public string? StringContent { get; }
    public List<VendorCohereContentBlock>? BlocksContent { get; }

    public VendorCohereToolMessageContent(string content)
    {
        StringContent = content;
    }

    public VendorCohereToolMessageContent(List<VendorCohereContentBlock> blocks)
    {
        BlocksContent = blocks;
    }

    public static implicit operator VendorCohereToolMessageContent(string content) => new(content);
    public static implicit operator VendorCohereToolMessageContent(List<VendorCohereContentBlock> blocks) => new(blocks);
}

internal class VendorCohereSystemMessageContentConverter : JsonConverter<VendorCohereSystemMessageContent>
{
    public override VendorCohereSystemMessageContent ReadJson(JsonReader reader, Type objectType, VendorCohereSystemMessageContent? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            return new VendorCohereSystemMessageContent(reader.Value!.ToString()!);
        }
        
        if (reader.TokenType == JsonToken.StartArray)
        {
            List<VendorCohereTextContentBlock>? blocks = serializer.Deserialize<List<VendorCohereTextContentBlock>>(reader);
            return new VendorCohereSystemMessageContent(blocks!);
        }
        
        throw new JsonSerializationException("Unexpected token type for VendorCohereSystemMessageContent");
    }

    public override void WriteJson(JsonWriter writer, VendorCohereSystemMessageContent? value, JsonSerializer serializer)
    {
        if (value?.StringContent is not null)
        {
            writer.WriteValue(value.StringContent);
        }
        else if (value?.BlocksContent is not null)
        {
            serializer.Serialize(writer, value.BlocksContent);
        }
        else
        {
            writer.WriteNull();
        }
    }
}

internal class VendorCohereAssistantMessageContentConverter : JsonConverter<VendorCohereAssistantMessageContent>
{
    public override VendorCohereAssistantMessageContent ReadJson(JsonReader reader, Type objectType, VendorCohereAssistantMessageContent? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            return new VendorCohereAssistantMessageContent(reader.Value!.ToString()!);
        }
        
        if (reader.TokenType == JsonToken.StartArray)
        {
            List<VendorCohereContentBlock>? blocks = serializer.Deserialize<List<VendorCohereContentBlock>>(reader);
            return new VendorCohereAssistantMessageContent(blocks!);
        }
        
        throw new JsonSerializationException("Unexpected token type for VendorCohereAssistantMessageContent");
    }

    public override void WriteJson(JsonWriter writer, VendorCohereAssistantMessageContent? value, JsonSerializer serializer)
    {
        if (value?.StringContent is not null)
        {
            writer.WriteValue(value.StringContent);
        }
        else if (value?.BlocksContent is not null)
        {
            serializer.Serialize(writer, value.BlocksContent);
        }
        else
        {
            writer.WriteNull();
        }
    }
}

internal class VendorCohereToolMessageContentConverter : JsonConverter<VendorCohereToolMessageContent>
{
    public override VendorCohereToolMessageContent ReadJson(JsonReader reader, Type objectType, VendorCohereToolMessageContent? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            return new VendorCohereToolMessageContent(reader.Value!.ToString()!);
        }
        
        if (reader.TokenType == JsonToken.StartArray)
        {
            List<VendorCohereContentBlock>? blocks = serializer.Deserialize<List<VendorCohereContentBlock>>(reader);
            return new VendorCohereToolMessageContent(blocks!);
        }
        
        throw new JsonSerializationException("Unexpected token type for VendorCohereToolMessageContent");
    }

    public override void WriteJson(JsonWriter writer, VendorCohereToolMessageContent? value, JsonSerializer serializer)
    {
        if (value?.StringContent is not null)
        {
            writer.WriteValue(value.StringContent);
        }
        else if (value?.BlocksContent is not null)
        {
            serializer.Serialize(writer, value.BlocksContent);
        }
        else
        {
            writer.WriteNull();
        }
    }
}

internal class VendorCohereUserMessageContentConverter : JsonConverter<VendorCohereUserMessageContent>
{
    public override VendorCohereUserMessageContent ReadJson(JsonReader reader, Type objectType, VendorCohereUserMessageContent? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            return new VendorCohereUserMessageContent(reader.Value!.ToString()!);
        }
        
        if (reader.TokenType == JsonToken.StartArray)
        {
            List<VendorCohereContentBlock>? blocks = serializer.Deserialize<List<VendorCohereContentBlock>>(reader);
            return new VendorCohereUserMessageContent(blocks!);
        }
        
        throw new JsonSerializationException("Unexpected token type for VendorCohereUserMessageContent");
    }

    public override void WriteJson(JsonWriter writer, VendorCohereUserMessageContent? value, JsonSerializer serializer)
    {
        if (value?.StringContent is not null)
        {
            writer.WriteValue(value.StringContent);
        }
        else if (value?.BlocksContent is not null)
        {
            serializer.Serialize(writer, value.BlocksContent);
        }
        else
        {
            writer.WriteNull();
        }
    }
 }

#endregion