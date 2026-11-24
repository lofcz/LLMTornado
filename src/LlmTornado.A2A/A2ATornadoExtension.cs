using A2A;
using LlmTornado.Chat;
using LlmTornado.Code;
using System.Text.Json;

namespace LlmTornado.A2A;
/// <summary>
/// ToDo:
/// - Handle more file types (e.g. video, other documents)
/// </summary>
public static partial class A2ATornadoExtension
{
    public static ChatMessage ToTornadoMessage(this AgentMessage agentMessage)
    {
        List<ChatMessagePart> parts = [];

        if (agentMessage.Parts != null)
        {
            foreach (Part part in agentMessage.Parts)
            {
                if(part is TextPart text)
                {
                    parts.Add(text.ToTornadoMessagePart());
                }
                else if(part is FilePart file)
                {
                    ChatMessagePart? tornadoPart = file.ToTornadoMessagePart();
                    if(tornadoPart != null)
                    {
                        parts.Add(tornadoPart);
                    }
                }
            }
        }

        return new ChatMessage
        {
            Role = ToTornadoMessageRole(agentMessage.Role),
            Parts = parts
        };
    }

    public static AgentMessage ToA2AAgentMessage(this ChatMessage chatMessage,
        Dictionary<string, JsonElement>? metadata = null,
        string? contextId = null,
        string? taskId = null,
        string[]? referenceTaskIds = null,
        string[]? extensions = null
        )
    {
        List<Part> parts = [];
        if (chatMessage.Content != null)
        {
            parts.Add(chatMessage.Content.ToA2ATextPart());
        }
        else if(chatMessage.Reasoning != null)
        {
            parts.Add(chatMessage.Reasoning.ToA2ATextPart());
        }
        else if (chatMessage.Audio != null)
        {
            parts.Add(chatMessage.Audio.ToA2AFilePart());
        }
        else if(chatMessage.Reasoning != null)
        {
            parts.Add(chatMessage.Reasoning.ToA2ATextPart());
        }
        else if (chatMessage.Parts != null)
        {
            foreach (ChatMessagePart part in chatMessage.Parts)
            {
                Part? a2aPart = part.ToA2APart();
                if (a2aPart is not null) parts.Add(a2aPart);
            }
        }

        metadata ??= new Dictionary<string, JsonElement>();
        metadata["tokens"] = JsonDocument.Parse($"{chatMessage.Tokens ?? 0}").RootElement;
        metadata["refusal"] = JsonSerializer.SerializeToElement(chatMessage.Refusal ?? "");
        metadata["userName"] = JsonSerializer.SerializeToElement(chatMessage.Name ?? "");

        return new AgentMessage
        {
            Role = chatMessage.Role.ToA2AMessageRole(),
            MessageId = chatMessage.Id.ToString(),
            Parts = parts,
            Metadata = metadata,
            ContextId = contextId,
            TaskId = taskId,
            ReferenceTaskIds = referenceTaskIds?.ToList(),
            Extensions = extensions?.ToList()
        };
    }

    /// <summary>
    /// Work in progress
    /// </summary>
    /// <param name="chatMessage"></param>
    /// <param name="metadata"></param>
    /// <param name="description"></param>
    /// <param name="name"></param>
    /// <param name="extensions"></param>
    /// <returns></returns>
    public static Artifact ToA2AArtifact(this ChatMessage chatMessage,
       Dictionary<string, JsonElement>? metadata = null,
       string? description = null,
       string? name = null,
       string[]? extensions = null
       )
    {
        List<Part> parts = [];
        if (chatMessage.Content != null)
        {
            parts.Add(chatMessage.Content.ToA2ATextPart());
        }
        else if (chatMessage.Reasoning != null)
        {
            parts.Add(chatMessage.Reasoning.ToA2ATextPart());
        }
        else if (chatMessage.Audio != null)
        {
            parts.Add(chatMessage.Audio.ToA2AFilePart());
        }
        else if (chatMessage.Reasoning != null)
        {
            parts.Add(chatMessage.Reasoning.ToA2ATextPart());
        }
        else if (chatMessage.Parts != null)
        {
            foreach (ChatMessagePart part in chatMessage.Parts)
            {
                Part? a2aPart = part.ToA2APart();
                if (a2aPart is not null) parts.Add(a2aPart);
            }
        }

        metadata ??= new Dictionary<string, JsonElement>();
        metadata["tokens"] = JsonDocument.Parse($"{chatMessage.Tokens ?? 0}").RootElement;
        metadata["refusal"] = JsonSerializer.SerializeToElement(chatMessage.Refusal ?? "");
        metadata["userName"] = JsonSerializer.SerializeToElement(chatMessage.Name ?? "");

        return new Artifact
        {
            Metadata = metadata,
            Parts = [],
            ArtifactId = chatMessage.Id.ToString(),
            Description = description,
            Name = name,
            Extensions = extensions?.ToList()
        };
    }

    public static Part? ToA2APart(this ChatMessagePart part)
    {
        return part.Type switch
        {
            ChatMessageTypes.Text => part.Text?.ToA2ATextPart(),
            ChatMessageTypes.Image => part.Image?.ToA2AFilePart(),
            ChatMessageTypes.Document => part.Document?.ToA2AFilePart(),
            ChatMessageTypes.Audio => part.Audio?.ToA2AFilePart(),
            ChatMessageTypes.Reasoning => part.Reasoning?.ToA2ATextPart(),
            ChatMessageTypes.Video => part.Video?.ToA2AFilePart(),
            ChatMessageTypes.FileLink => part.FileLinkData?.ToA2AFilePart(),
            ChatMessageTypes.ExecutableCode => part.ExecutableCode?.ToA2ATextPart(),
            _ => null,
        };
    }

    public static MessageRole ToA2AMessageRole(this ChatMessageRoles? role)
    {
        return role switch
        {
            ChatMessageRoles.User => MessageRole.User,
            ChatMessageRoles.System => MessageRole.User,
            ChatMessageRoles.Assistant => MessageRole.Agent,
            _ => MessageRole.Agent,
        };
    }

    public static ChatMessageRoles ToTornadoMessageRole(this MessageRole? role)
    {
        return role switch
        {
            MessageRole.User => ChatMessageRoles.User,
            MessageRole.Agent => ChatMessageRoles.Assistant,
            _ => ChatMessageRoles.User,
        };
    }
}
