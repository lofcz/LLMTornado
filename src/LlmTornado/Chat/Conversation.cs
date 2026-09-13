using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Code.Vendor;
using LlmTornado.Files;
using LlmTornado.Infra;
using LlmTornado.Responses;
using LlmTornado.Responses.Events;

namespace LlmTornado.Chat;

/// <summary>
///     Represents on ongoing chat with back-and-forth interactions between the user and the chatbot.
///     This is the simplest way to interact with the Chat APIs, rather than manually using the ChatEndpoint methods.
/// </summary>
public class Conversation
{
    private readonly ChatEndpoint endpoint;
    private readonly List<ChatMessage> messages;
    private readonly ResponsesEndpoint responsesEndpoint;
    /// <summary>
    ///     Creates a new conversation.
    /// </summary>
    /// <param name="endpoint">
    ///     A reference to the API endpoint, needed for API requests. Generally should be
    ///     <see cref="TornadoApi.Chat" />.
    /// </param>
    /// <param name="model">
    ///     Optionally specify the model to use for Chat requests. If not specified, used
    ///     <paramref name="defaultChatRequestArgs" />.Model or falls back to <see cref="LlmTornado.Models.Model.GPT35_Turbo" />
    /// </param>
    /// <param name="defaultChatRequestArgs">
    ///     Allows setting the parameters to use when calling the Chat API. Can be useful for setting temperature,
    ///     presence_penalty, and more.  See
    ///     <see href="https://platform.openai.com/docs/api-reference/chat/create">
    ///         OpenAI documentation for a list of possible
    ///         parameters to tweak.
    ///     </see>
    /// </param>
    public Conversation(ChatEndpoint endpoint, ChatModel? model = null, ChatRequest? defaultChatRequestArgs = null)
    {
        RequestParameters = new ChatRequest(this, defaultChatRequestArgs);

        if (model is not null)
        {
            RequestParameters.Model = model;
        }

        RequestParameters.Model ??= ChatModel.OpenAi.Gpt35.Turbo;

        messages = [];
        this.endpoint = endpoint;
        responsesEndpoint = endpoint.Api.Responses;
    }

    /// <summary>
    ///     Allows setting the parameters to use when calling the Chat API.
    /// </summary>
    public ChatRequest RequestParameters { get; }

    /// <summary>
    ///     Specifies the model to use for Chat requests. This is just a shorthand to access
    ///     <see cref="RequestParameters" />.Model
    /// </summary>
    public ChatModel Model
    {
        get => RequestParameters.Model ?? ChatModel.OpenAi.Gpt35.Turbo;
        set => RequestParameters.Model = value;
    }

    /// <summary>
    ///     Called after one or more tools are requested by the model and the corresponding results are resolved.
    /// </summary>
    public Func<ResolvedToolsCall, Task>? OnAfterToolsCall { get; set; }

    /// <summary>
    ///     After calling <see cref="GetResponse" />, this contains the full response object which can contain
    ///     useful metadata like token usages, <see cref="ChatChoice.FinishReason" />, etc.  This is overwritten with every
    ///     call to <see cref="GetResponse" /> and only contains the most recent result.
    /// </summary>
    public ChatResult? MostRecentApiResult { get; private set; }

    /// <summary>
    /// If the conversation is currently in an error state, this property contains information about the error.
    /// </summary>
    public TornadoHttpException? Error { get; internal set; }
    
    /// <summary>
    ///     If not null, overrides the default OpenAI auth
    /// </summary>
    public ApiAuthentication? Auth { get; set; }

    /// <summary>
    ///     A list of messages exchanged so far.  Do not modify this list directly.  Instead, use
    ///     <see cref="AppendMessage(ChatMessage)" />, <see cref="AppendUserInput(string)" />,
    ///     <see cref="AppendSystemMessage(string)" />, or <see cref="AppendExampleChatbotOutput(string)" />.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages => messages.ToList();

    /// <summary>
    ///     Appends a <see cref="ChatMessage" /> to the chat history
    /// </summary>
    /// <param name="message">The <see cref="ChatMessage" /> to append to the chat history</param>
    public Conversation AppendMessage(ChatMessage message)
    {
        messages.Add(message);
        return this;
    }

    /// <summary>
    ///     Appends a <see cref="ChatMessage" /> to the chat hstory
    /// </summary>
    /// <param name="message">The <see cref="ChatMessage" /> to append to the chat history</param>
    /// <param name="position">Zero-based index at which to insert the message</param>
    public Conversation AppendMessage(ChatMessage message, int position)
    {
        messages.Insert(position, message);
        return this;
    }

    /// <summary>
    /// Updates <see cref="RequestParameters"/>.
    /// </summary>
    public Conversation Update(Action<ChatRequest> updateFn)
    {
        updateFn.Invoke(RequestParameters);
        return this;
    }

    /// <summary>
    ///     Removes given message from the conversation. If the message is not found, nothing happens
    /// </summary>
    /// <param name="message"></param>
    /// <returns>Whether message was removed</returns>
    public bool RemoveMessage(ChatMessage message)
    {
        ChatMessage? msg = messages.FirstOrDefault(x => x.Id == message.Id);

        if (msg is not null)
        {
            messages.Remove(msg);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Removes message with given id from the conversation. If the message is not found, nothing happens
    /// </summary>
    /// <param name="id"></param>
    /// <returns>Whether message was removed</returns>
    public bool RemoveMessage(Guid id)
    {
        ChatMessage? msg = messages.FirstOrDefault(x => x.Id == id);

        if (msg is not null)
        {
            messages.Remove(msg);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears the conversation, removing all messages.
    /// </summary>
    public void Clear()
    {
        ResetLastResultState();
        messages.Clear();
    }

    /// <summary>
    ///     Updates text of a given message
    /// </summary>
    /// <param name="message">Message to update</param>
    /// <param name="content">New text</param>
    public Conversation EditMessageContent(ChatMessage message, string content)
    {
        message.Content = content;
        message.Parts = null;
        return this;
    }

    /// <summary>
    ///     Updates parts of a given message
    /// </summary>
    /// <param name="message">Message to update</param>
    /// <param name="parts">New parts</param>
    public Conversation EditMessageContent(ChatMessage message, IEnumerable<ChatMessagePart> parts)
    {
        message.Content = null;
        message.Parts = parts.ToList();
        return this;
    }

    /// <summary>
    ///     Finds a message in the conversation by id. If found, updates text of this message
    /// </summary>
    /// <param name="id">Message to update</param>
    /// <param name="content">New text</param>
    /// <returns>Whether message was updated</returns>
    public bool EditMessageContent(Guid id, string content)
    {
        ChatMessage? msg = messages.FirstOrDefault(x => x.Id == id);

        if (msg is not null)
        {
            msg.Content = content;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Updates role of a given message
    /// </summary>
    /// <param name="message">Message to update</param>
    /// <param name="role">New role</param>
    public Conversation EditMessageRole(ChatMessage message, ChatMessageRoles role)
    {
        message.Role = role;
        return this;
    }

    /// <summary>
    ///     Finds a message in the conversation by id. If found, updates text of this message
    /// </summary>
    /// <param name="id">Message to update</param>
    /// <param name="role">New role</param>
    /// <returns>Whether message was updated</returns>
    public bool EditMessageRole(Guid id, ChatMessageRoles role)
    {
        ChatMessage? msg = messages.FirstOrDefault(x => x.Id == id);

        if (msg is not null)
        {
            msg.Role = role;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history
    /// </summary>
    /// <param name="role">
    ///     The <see cref="ChatMessageRoles" /> for the message.  Typically, a conversation is formatted with a
    ///     system message first, followed by alternating user and assistant messages.  See
    ///     <see href="https://platform.openai.com/docs/guides/chat/introduction">the OpenAI docs</see> for more details about
    ///     usage.
    /// </param>
    /// <param name="content">The content of the message)</param>
    public Conversation AppendMessage(ChatMessageRoles role, string content)
    {
        return AppendMessage(new ChatMessage(role, content));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat hstory
    /// </summary>
    /// <param name="role">
    ///     The <see cref="ChatMessageRoles" /> for the message.  Typically, a conversation is formatted with a
    ///     system message first, followed by alternating user and assistant messages.  See
    ///     <see href="https://platform.openai.com/docs/guides/chat/introduction">the OpenAI docs</see> for more details about
    ///     usage.
    /// </param>
    /// <param name="content">The content of the message</param>
    /// <param name="id">Id of the message</param>
    public Conversation AppendMessage(ChatMessageRoles role, string content, Guid? id)
    {
        return AppendMessage(new ChatMessage(role, content, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="content">
    ///     Text content generated by the end users of an application, or set by a developer as an
    ///     instruction
    /// </param>
    public Conversation AppendUserInput(string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, content));
    }

    /// <summary>
    /// Adds a <see cref="ChatMessage" /> to the chat history.
    /// </summary>
    public Conversation AddMessage(ChatMessage message)
    {
        return AppendMessage(message);
    }

    /// <summary>
    /// Adds messages to the chat history.
    /// </summary>
    // ReSharper disable once ParameterHidesMember
    public Conversation AddMessage(IEnumerable<ChatMessage> messages)
    {
        this.messages.AddRange(messages);
        return this;
    }

    /// <summary>
    /// Adds messages to the chat history.
    /// </summary>
    // ReSharper disable once ParameterHidesMember
    public Conversation AddMessage(List<ChatMessage> messages)
    {
        this.messages.AddRange(messages);
        return this;
    }

    /// <summary>
    /// <inheritdoc cref="AppendUserInput(string)"/>
    /// </summary>
    public Conversation AddUserMessage(string content)
    {
        return AppendUserInput(content);
    }

    /// <summary>
    /// Replaces the current system prompt in the conversation, or prepends system prompt as the first message.
    /// </summary>
    public Conversation SetSystemMessage(string prompt)
    {
        messages.RemoveAll(x => x.Role is ChatMessageRoles.System);
        PrependSystemMessage(prompt);
        return this;
    }

    /// <summary>
    /// Replaces the current system prompt in the conversation, or prepends system prompt as the first message.
    /// </summary>
    public Conversation SetSystemMessage(string prompt, Guid id)
    {
        messages.RemoveAll(x => x.Role is ChatMessageRoles.System);
        PrependSystemMessage(prompt, id);
        return this;
    }

    /// <summary>
    /// Replaces the current system prompt in the conversation, or prepends system prompt as the first message.
    /// </summary>
    public Conversation SetSystemMessage(IEnumerable<ChatMessagePart> parts)
    {
        messages.RemoveAll(x => x.Role is ChatMessageRoles.System);
        PrependSystemMessage(parts);
        return this;
    }

    /// <summary>
    /// Replaces the current system prompt in the conversation, or prepends system prompt as the first message.
    /// </summary>
    public Conversation SetSystemMessage(IEnumerable<ChatMessagePart> parts, Guid id)
    {
        messages.RemoveAll(x => x.Role is ChatMessageRoles.System);
        PrependSystemMessage(parts, id);
        return this;
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="content">
    ///     Text content generated by the end users of an application, or set by a developer as an
    ///     instruction
    /// </param>
    /// <param name="id">id of the message</param>
    public Conversation AppendUserInput(string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, content, id));
    }

    /// <summary>
    /// <inheritdoc cref="AppendUserInput(string, Guid)"/>
    /// </summary>
    public Conversation AddUserMessage(string content, Guid id)
    {
        return AppendUserInput(content, id);
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="parts">
    ///     Parts of the message
    /// </param>
    /// <param name="id">id of the message</param>
    public Conversation AppendUserInput(IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, parts, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="parts">
    ///     Parts of the message
    /// </param>
    /// <param name="id">id of the message</param>
    public Conversation AddUserMessage(IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendUserInput(parts, id);
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="parts">
    ///     Parts of the message
    /// </param>
    public Conversation AppendUserInput(IEnumerable<ChatMessagePart> parts)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, parts));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="parts">
    ///     Parts of the message
    /// </param>
    public Conversation AddUserMessage(IEnumerable<ChatMessagePart> parts)
    {
        return AppendUserInput(parts);
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="userName">The name of the user in a multi-user chat</param>
    /// <param name="content">
    ///     Text content generated by the end users of an application, or set by a developer as an
    ///     instruction
    /// </param>
    public Conversation AppendUserInputWithName(string userName, string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, content)
        {
            Name = userName
        });
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="userName">The name of the user in a multi-user chat</param>
    /// <param name="content">
    ///     Text content generated by the end users of an application, or set by a developer as an
    ///     instruction
    /// </param>
    /// <param name="id">id of the message</param>
    public Conversation AppendUserInputWithName(string userName, string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, content, id) { Name = userName });
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.User" />.  The user messages help instruct the assistant. They can be generated by the
    ///     end users of an application, or set by a developer as an instruction.
    /// </summary>
    /// <param name="userName">The name of the user in a multi-user chat</param>
    /// <param name="parts">
    ///     Parts of the message generated by the end users of an application, or set by a developer as an
    ///     instruction
    /// </param>
    /// <param name="id">id of the message</param>
    public Conversation AppendUserInputWithName(string userName, IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.User, parts, id) { Name = userName });
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />. The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="content">text content that helps set the behavior of the assistant</param>
    public Conversation AppendSystemMessage(string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, content));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />. The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="content">text content that helps set the behavior of the assistant</param>
    public Conversation AddSystemMessage(string content)
    {
        return AppendSystemMessage(content);
    }

    /// <summary>
    ///      Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///      <see cref="ChatMessageRoles.System" />. The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="parts"></param>
    /// <returns></returns>
    public Conversation AppendSystemMessage(IEnumerable<ChatMessagePart> parts)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, parts));
    }

    /// <summary>
    ///      Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///      <see cref="ChatMessageRoles.System" />. The system message helps set the behavior of the assistant.
    /// </summary>
    public Conversation AddSystemMessage(IEnumerable<ChatMessagePart> parts)
    {
        return AppendSystemMessage(parts);
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />.  The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="content">text content that helps set the behavior of the assistant</param>
    /// <param name="id">id of the message</param>
    public Conversation AppendSystemMessage(string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, content, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />.  The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="parts">Parts of the message which helps set the behavior of the assistant</param>
    /// <param name="id">id of the message</param>
    public Conversation AppendSystemMessage(IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, parts, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />.  The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="content">text content that helps set the behavior of the assistant</param>
    /// <param name="id">id of the message</param>
    public Conversation PrependSystemMessage(string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, content, id), 0);
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />.  The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="content">text content that helps set the behavior of the assistant</param>
    public Conversation PrependSystemMessage(string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, content), 0);
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />.  The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="parts">Parts of the message which helps set the behavior of the assistant</param>
    /// <param name="id">id of the message</param>
    public Conversation PrependSystemMessage(IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, parts, id), 0);
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.System" />.  The system message helps set the behavior of the assistant.
    /// </summary>
    /// <param name="parts">Parts of the message which helps set the behavior of the assistant</param>
    public Conversation PrependSystemMessage(IEnumerable<ChatMessagePart> parts)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.System, parts), 0);
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Assistant" />.  Assistant messages can be written by a developer to help give examples
    ///     of desired behavior.
    /// </summary>
    /// <param name="content">Text content written by a developer to help give examples of desired behavior</param>
    public Conversation AppendExampleChatbotOutput(string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, content));
    }

    /// <summary>
    /// <inheritdoc cref="AppendExampleChatbotOutput(string)"/>
    /// </summary>
    public Conversation AddAssistantMessage(string content)
    {
        return AppendExampleChatbotOutput(content);
    }

    /// <summary>
    /// Appends assistant message to the conversation.
    /// </summary>
    public Conversation AppendAssistantMessage(string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, content));
    }

    /// <summary>
    /// Appends assistant message to the conversation.
    /// </summary>
    public Conversation AppendAssistantMessage(string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, content, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Tool" />.  The function message is a response to a request from the system for
    ///     output from a predefined function.
    /// </summary>
    /// <param name="functionName">The name of the function for which the content has been generated as the result</param>
    /// <param name="content">The text content (usually JSON)</param>
    public Conversation AppendFunctionMessage(string functionName, string content)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Tool, content)
        {
            Name = functionName
        });
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Tool" />.  The function message is a response to a request from the system for
    ///     output from a predefined function.
    /// </summary>
    /// <param name="functionName">The name of the function for which the content has been generated as the result</param>
    /// <param name="content">The text content (usually JSON)</param>
    /// <param name="invocationSucceeded">Whether the invocation succeeded, can be null</param>
    public Conversation AddToolMessage(string functionName, string content, bool? invocationSucceeded = null)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Tool, content)
        {
            ToolCallId = functionName,
            ToolInvocationSucceeded = invocationSucceeded
        });
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Assistant" />.  Assistant messages can be written by a developer to help give examples
    ///     of desired behavior.
    /// </summary>
    /// <param name="content">Text content written by a developer to help give examples of desired behavior</param>
    /// <param name="id">id of the message</param>
    public Conversation AppendExampleChatbotOutput(string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, content, id));
    }

    /// <summary>
    /// <inheritdoc cref="AppendExampleChatbotOutput(string, Guid)"/>
    /// </summary>
    public Conversation AddAssistantMessage(string content, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, content, id));
    }

    /// <summary>
    /// <inheritdoc cref="AppendExampleChatbotOutput(string, Guid)"/>
    /// </summary>
    public Conversation AddAssistantMessage(string content, List<ToolCall> calls)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, content)
        {
            ToolCalls = calls
        });
    }

    /// <summary>
    /// <inheritdoc cref="AppendExampleChatbotOutput(string, Guid)"/>
    /// </summary>
    public Conversation AddAssistantMessage(string content, List<ToolCall> calls, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, content, id)
        {
            ToolCalls = calls
        });
    }

    /// <summary>
    /// <inheritdoc cref="AppendExampleChatbotOutput(string, Guid)"/>
    /// </summary>
    public Conversation AddAssistantMessage(List<ToolCall> calls)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant)
        {
            ToolCalls = calls
        });
    }

    /// <summary>
    /// <inheritdoc cref="AppendExampleChatbotOutput(string, Guid)"/>
    /// </summary>
    public Conversation AddAssistantMessage(List<ToolCall> calls, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant)
        {
            Id = id,
            ToolCalls = calls
        });
    }

    /// <summary>
    /// <inheritdoc cref="AppendExampleChatbotOutput(string, Guid)"/>
    /// </summary>
    public Conversation AddAssistantMessage(List<ChatMessagePart> parts)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant)
        {
            Parts = parts
        });
    }

    /// <summary>
    /// <inheritdoc cref="AppendExampleChatbotOutput(string, Guid)"/>
    /// </summary>
    public Conversation AddAssistantMessage(List<ChatMessagePart> parts, List<ToolCall> toolCalls, bool? invocationSucceeded = null)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant)
        {
            Parts = parts,
            ToolCalls = toolCalls,
            ToolInvocationSucceeded = invocationSucceeded
        });
    }

    /// <summary>
    /// <inheritdoc cref="AppendExampleChatbotOutput(string, Guid)"/>
    /// </summary>
    public Conversation AddAssistantMessage(List<ChatMessagePart> parts, List<ToolCall> toolCalls, Guid id, bool? invocationSucceeded = null)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant)
        {
            Id = id,
            Parts = parts,
            ToolCalls = toolCalls,
            ToolInvocationSucceeded = invocationSucceeded
        });
    }

    /// <summary>
    /// <inheritdoc cref="AppendExampleChatbotOutput(string, Guid)"/>
    /// </summary>
    public Conversation AddAssistantMessage(List<ChatMessagePart> parts, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant)
        {
            Id = id,
            Parts = parts
        });
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Assistant" />.  Assistant messages can be written by a developer to help give examples
    ///     of desired behavior.
    /// </summary>
    /// <param name="parts">Parts of the message written by a developer to help give examples of desired behavior</param>
    /// <param name="id">id of the message</param>
    public Conversation AppendExampleChatbotOutput(IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, parts, id));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Assistant" />.  Assistant messages can be written by a developer to help give examples
    ///     of desired behavior.
    /// </summary>
    /// <param name="parts">Parts of the message written by a developer to help give examples of desired behavior</param>
    public Conversation AppendExampleChatbotOutput(IEnumerable<ChatMessagePart> parts)
    {
        return AppendMessage(new ChatMessage(ChatMessageRoles.Assistant, parts));
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Assistant" />.  Assistant messages can be written by a developer to help give examples
    ///     of desired behavior.
    /// </summary>
    /// <param name="parts">Parts of the message written by a developer to help give examples of desired behavior</param>
    /// <param name="id">id of the message</param>
    public Conversation AddAssistantMessage(IEnumerable<ChatMessagePart> parts, Guid id)
    {
        return AppendExampleChatbotOutput(parts, id);
    }

    /// <summary>
    ///     Creates and appends a <see cref="ChatMessage" /> to the chat history with the Role of
    ///     <see cref="ChatMessageRoles.Assistant" />.  Assistant messages can be written by a developer to help give examples
    ///     of desired behavior.
    /// </summary>
    /// <param name="parts">Parts of the message written by a developer to help give examples of desired behavior</param>
    public Conversation AddAssistantMessage(IEnumerable<ChatMessagePart> parts)
    {
        return AppendExampleChatbotOutput(parts);
    }

    #region Non-streaming

    /// <summary>
    /// Sets <see cref="Error"/> and <see cref="MostRecentApiResult"/> to null, managed internally.
    /// </summary>
    public void ResetLastResultState()
    {
        Error = null;
        MostRecentApiResult = null;
    }
    
    /// <summary>
    ///     Calls the API to get a response, which is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />.
    /// </summary>
    /// <returns>The string of the response from the chatbot API</returns>
    public async Task<string?> GetResponse(CancellationToken token = default)
    {
        RestDataOrException<ChatRichResponse> result = await GetResponseRichSafe(token).ConfigureAwait(false);
        
        if (result.Exception is not null)
        {
            throw result.Exception;
        }
        
        string text = result.Data!.Text;
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// Serializes the conversation and returns the request that would be sent outbound
    /// </summary>
    public TornadoRequestContent Serialize(ChatRequestSerializeOptions? options = null)
    {
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages = messages,
            Stream = options?.Stream,
        };

        IEndpointProvider provider = endpoint.Api.GetProvider(Model);
        return req.Serialize(provider, options);
    }

    /// <summary>
    /// Serializes the conversation and returns the request that would be sent outbound
    /// </summary>
    public TornadoRequestContent Serialize(bool pretty)
    {
        return Serialize(pretty ? ChatRequestSerializeOptions.PresetPretty : null);
    }

    /// <summary>
    /// Calls the API to get a response. Safe on a network level.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The response with rich content blocks.</returns>
    public async Task<RestDataOrException<ChatRichResponse>> GetResponseRichSafe(CancellationToken token = default)
    {
        return await GetResponseRichSafe(null, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Calls the API to get a response. Safe on a network level.
    /// </summary>
    /// <param name="functionCallHandler">If provided, the tool calls are resolved immediately and a message is appended to the conversation with the result.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The response with rich content blocks.</returns>
    public async Task<RestDataOrException<ChatRichResponse>> GetResponseRichSafe(Func<List<FunctionCall>, ValueTask>? functionCallHandler, CancellationToken token = default)
    {
        ResetLastResultState();
        
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages = messages,
            CancellationToken = token
        };

        ChatResult chatResult;
        IHttpCallResult httpResult;
        CapabilityEndpoints capabilityEndpoint = req.GetCapabilityEndpoint();

        if (capabilityEndpoint is CapabilityEndpoints.Responses)
        {
            IEndpointProvider provider = responsesEndpoint.Api.GetProvider(req.Model ?? ChatModel.OpenAi.Gpt35.Turbo);
            ResponseRequest responsesReq = ResponseHelpers.ToResponseRequest(provider, req.ResponseRequestParameters, req);
            HttpCallResult<ResponseResult> result = await responsesEndpoint.CreateResponseSafe(responsesReq).ConfigureAwait(false);

            if (!result.Ok)
            {
                Error = new TornadoHttpException(result);
                return new RestDataOrException<ChatRichResponse>(result);
            }

            httpResult = result;
            chatResult = ResponseHelpers.ToChatResult(result.Data, responsesReq, provider);
        }
        else
        {
            HttpCallResult<ChatResult> res = await endpoint.CreateChatCompletionSafe(req).ConfigureAwait(false);

            if (!res.Ok)
            {
                Error = new TornadoHttpException(res);
                return new RestDataOrException<ChatRichResponse>(res);
            }

            httpResult = res;
            chatResult = res.Data;
        }

        MostRecentApiResult = chatResult;

        ChatRichResponse response = await HandleResponseRich(req, chatResult, functionCallHandler, null).ConfigureAwait(false);
        return new RestDataOrException<ChatRichResponse>(response, httpResult);
    }

    ParsedToolCalls ParseCalls(ChatMessage message)
    {
        List<FunctionCall> calls = [];
        List<CustomToolCall> customCalls = [];

        if (message.ToolCalls is null || message.ToolCalls.Count is 0)
        {
            return new ParsedToolCalls
            {
                CustomToolCalls = customCalls,
                FunctionCalls = calls
            };
        }

        foreach (ToolCall call in message.ToolCalls)
        {
            if (call.FunctionCall is not null)
            {
                calls.Add(new FunctionCall
                {
                    Name = call.FunctionCall!.Name,
                    Arguments = call.FunctionCall.Arguments,
                    ToolCall = call,
                    Tool = RequestParameters.Tools?.FirstOrDefault(y => string.Equals(y.Function?.Name, call.FunctionCall.Name)),
                    Result = call.FunctionCall.Result,
                    LastInvocationResult = call.FunctionCall.LastInvocationResult
                });
            }
            else if (call.CustomCall is not null)
            {
                customCalls.Add(new CustomToolCall
                {
                    Name = call.CustomCall.Name,
                    Input = call.CustomCall.Input,
                    ToolCall = call,
                    Result = call.CustomCall.Result
                });
            }
        }

        return new ParsedToolCalls
        {
            CustomToolCalls = customCalls,
            FunctionCalls = calls
        };
    }

    private async Task<ChatRichResponse> HandleResponseRich(ChatRequest request, ChatResult? res, Func<List<FunctionCall>, ValueTask>? functionCallHandler, ToolCallsHandler? toolCallsHandler)
    {
        List<ChatRichResponseBlock> blocks = [];
        ChatRichResponse response = new ChatRichResponse(res, blocks);

        if (res is not null)
        {
            response.Request = res.Request;
        }

        if (res is null || !(res.Choices?.Count > 0))
        {
            return response;
        }

        foreach (ChatChoice choice in res.Choices)
        {
            ChatMessage? newMsg = choice.Message;

            if (newMsg is null)
            {
                continue;
            }

            AppendMessage(newMsg);

            if (newMsg.ToolCalls is { Count: > 0 } && !OutboundToolChoice.OutboundToolChoiceConverter.KnownFunctionNames.Contains(newMsg.ToolCalls[0].FunctionCall?.Name ?? string.Empty))
            {
                ParsedToolCalls parsedCalls = ParseCalls(newMsg);
                ResolvedToolsCall result = new ResolvedToolsCall();

                if (parsedCalls.CustomToolCalls.Count > 0)
                {
                    blocks.AddRange(parsedCalls.CustomToolCalls.Select(x => new ChatRichResponseBlock
                    {
                        Type = ChatRichResponseBlockTypes.CustomTool,
                        CustomToolCall = x
                    }));
                }

                if (parsedCalls.FunctionCalls.Count > 0)
                {
                    blocks.AddRange(parsedCalls.FunctionCalls.Select(x => new ChatRichResponseBlock
                    {
                        Type = ChatRichResponseBlockTypes.Function,
                        FunctionCall = x
                    }));

                    if (functionCallHandler is not null || toolCallsHandler is not null)
                    {
                        Guid currentMsgId = Guid.NewGuid();

                        if (functionCallHandler is not null)
                        {
                            await functionCallHandler.Invoke(parsedCalls.FunctionCalls);
                        }

                        foreach (FunctionCall call in parsedCalls.FunctionCalls)
                        {
                            if (call.Result is null && call.Tool?.Delegate is not null)
                            {
                                await call.Invoke(call.Arguments ?? "{}");
                            }
                            
                            ChatMessage fnResultMsg = new ChatMessage(ChatMessageRoles.Tool, call.Result?.Content ?? "The service returned no data.".ToJson(), Guid.NewGuid())
                            {
                                Id = currentMsgId,
                                ToolCallId = call.ToolCall?.Id ?? call.Name,
                                ToolInvocationSucceeded = call.Result?.InvocationSucceeded ?? false,
                                ContentJsonType = call.Result?.ContentJsonType ?? typeof(string),
                                FunctionCall = call
                            };

                            currentMsgId = Guid.NewGuid();
                            AppendMessage(fnResultMsg);

                            result.ToolResults.Add(new ResolvedToolCall
                            {
                                Call = call,
                                Result = call.Result ?? new FunctionResult(call, null, null, false),
                                ToolMessage = fnResultMsg
                            });
                        }

                        if (OnAfterToolsCall is not null)
                        {
                            await OnAfterToolsCall(result).ConfigureAwait(false);
                        }
                    }
                }
            }

            if (newMsg.Parts?.Count > 0)
            {
                blocks.AddRange(newMsg.Parts.Select(x => new ChatRichResponseBlock(x, newMsg)));
            }
            else if (newMsg.Content?.Length > 0)
            {
                blocks.Add(new ChatRichResponseBlock
                {
                    Type = ChatRichResponseBlockTypes.Message,
                    Message = newMsg.Content,
                    Reasoning = newMsg.ReasoningTokens is null ? null : new ChatMessageReasoningData
                    {
                        Content  = newMsg.ReasoningTokens,
                        Provider = res.Provider?.Provider ?? LLmProviders.OpenAi
                    }
                });
            }
            else if (newMsg.Audio is not null)
            {
                blocks.Add(new ChatRichResponseBlock
                {
                    Type = ChatRichResponseBlockTypes.Audio,
                    ChatAudio = newMsg.Audio
                });
            }

            if (!newMsg.ReasoningTokens.IsNullOrWhiteSpace() && !blocks.Any(x => x.Type is ChatRichResponseBlockTypes.Reasoning))
            {
                blocks.Add(new ChatRichResponseBlock
                {
                    Type = ChatRichResponseBlockTypes.Reasoning,
                    Reasoning = new ChatMessageReasoningData
                    {
                        Content = newMsg.ReasoningTokens
                    }
                });
            }
        }

        return response;
    }

    /// <summary>
    /// Calls the API to get a response.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The response with rich content blocks.</returns>
    public async Task<ChatRichResponse> GetResponseRich(CancellationToken token = default)
    {
        return await GetResponseRichInternal(null, null, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Calls the API to get a response.
    /// </summary>
    /// <param name="toolCallsHandler">Results from tools with attached delegates will be added to the conversation automatically.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The response with rich content blocks.</returns>
    public async Task<ChatRichResponse> GetResponseRich(ToolCallsHandler toolCallsHandler, CancellationToken token = default)
    {
        return await GetResponseRichInternal(null, toolCallsHandler, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Calls the API to get a response.
    /// </summary>
    /// <param name="fnHandler">If provided, the tool calls are resolved immediately and a message is appended to the conversation with the result.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The response with rich content blocks.</returns>
    public async Task<ChatRichResponse> GetResponseRich(Func<List<FunctionCall>, ValueTask> fnHandler, CancellationToken token = default)
    {
        return await GetResponseRichInternal(fnHandler, null, token);
    }

    private async Task<ChatRichResponse> GetResponseRichInternal(Func<List<FunctionCall>, ValueTask>? fnHandler, ToolCallsHandler? toolCallsHandler, CancellationToken token = default)
    {
        ResetLastResultState();
        
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages = messages,
            CancellationToken = token
        };

        ChatResult? res;
        CapabilityEndpoints capabilityEndpoint = req.GetCapabilityEndpoint();

        if (capabilityEndpoint is CapabilityEndpoints.Responses && req.ResponseRequestParameters is not null)
        {
            // avoid double-serializing, use provider resolved without regards to available API keys
            IEndpointProvider provider = endpoint.Api.GetProvider(req.Model ?? ChatModel.OpenAi.Gpt35.Turbo);
            ResponseRequest responsesReq = ResponseHelpers.ToResponseRequest(provider, req.ResponseRequestParameters, req);
            HttpCallResult<ResponseResult> resultSafe = await responsesEndpoint.CreateResponseSafe(responsesReq).ConfigureAwait(false);

            if (!resultSafe.Ok)
            {
                Error = new TornadoHttpException(resultSafe);
                throw resultSafe.Exception;
            }
            
            res = ResponseHelpers.ToChatResult(resultSafe.Data, responsesReq, provider);
        }
        else
        {
            HttpCallResult<ChatResult> resSafe = await endpoint.CreateChatCompletionSafe(req).ConfigureAwait(false);

            if (!resSafe.Ok)
            {
                Error = new TornadoHttpException(resSafe);
                throw resSafe.Exception;
            }

            res = resSafe.Data;
        }
        
        MostRecentApiResult = res;

        if (res is null)
        {
            return new ChatRichResponse(null, null);
        }

        if (res.Choices is null)
        {
            return new ChatRichResponse(res, null);
        }

        return await HandleResponseRich(req, res, fnHandler, toolCallsHandler).ConfigureAwait(false);
    }

    /// <summary>
    ///     Calls the API to get a response, which is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />.
    /// </summary>
    /// <returns>The string of the response from the chatbot API</returns>
    public async Task<RestDataOrException<ChatChoice>> GetResponseSafe(CancellationToken token = default)
    {
        ResetLastResultState();
        
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages = messages,
            CancellationToken = token
        };

        HttpCallResult<ChatResult> res = await endpoint.CreateChatCompletionSafe(req).ConfigureAwait(false);

        if (!res.Ok)
        {
            Error = new TornadoHttpException(res);
            return new RestDataOrException<ChatChoice>(res);
        }

        MostRecentApiResult = res.Data;

        if (res.Data.Choices is null)
        {
            return new RestDataOrException<ChatChoice>(new Exception("No choices returned by the service."), res);
        }

        if (res.Data.Choices.Count > 0)
        {
            ChatMessage? newMsg = res.Data.Choices[0].Message;

            if (newMsg is not null)
            {
                AppendMessage(newMsg);
            }

            return new RestDataOrException<ChatChoice>(res.Data.Choices[0], res);
        }

        return new RestDataOrException<ChatChoice>(new Exception("No choices returned by the service."), res);
    }
    #endregion

    #region Streaming

    /// <summary>
    ///     Calls the API to get a response, which is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />, and streams the results to the
    ///     <paramref name="resultHandler" /> as they come in. <br />
    ///     If you are on the latest C# supporting async enumerables, you may prefer the cleaner syntax of
    ///     <see cref="StreamResponseEnumerable" /> instead.
    /// </summary>
    /// <param name="resultHandler">An action to be called as each new result arrives.</param>
    /// <param name="token"></param>
    public async Task StreamResponse(Action<string> resultHandler, CancellationToken token = default)
    {
        await foreach (string res in StreamResponseEnumerable(token: token))
        {
            resultHandler(res);
        }
    }

    /// <summary>
    ///     Calls the API to get a response, which is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />, and streams the results to the
    ///     <paramref name="resultHandler" /> as they come in. <br />
    ///     If you are on the latest C# supporting async enumerables, you may prefer the cleaner syntax of
    ///     <see cref="StreamResponseEnumerable" /> instead.
    /// </summary>
    /// <param name="resultHandler">
    ///     An action to be called as each new result arrives, which includes the index of the result
    ///     in the overall result set.
    /// </param>
    /// <param name="token"></param>
    public async Task StreamResponse(Action<int, string> resultHandler, CancellationToken token = default)
    {
        int index = 0;

        await foreach (string res in StreamResponseEnumerable(token: token))
        {
            resultHandler(index++, res);
        }
    }

    /// <summary>
    ///     Calls the API to get a response, which is appended to the current chat's <see cref="Messages" /> as an
    ///     <see cref="ChatMessageRoles.Assistant" /> <see cref="ChatMessage" />, and streams the results as they come in.
    ///     <br />
    ///     If you are not using C# 8 supporting async enumerables or if you are using the .NET Framework, you may need to use
    ///     <see cref="Code.StreamResponse" /> instead.
    /// </summary>
    /// <returns>
    ///     An async enumerable with each of the results as they come in.  See
    ///     <see href="https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#asynchronous-streams" /> for more
    ///     details on how to consume an async enumerable.
    /// </returns>
    public async IAsyncEnumerable<string> StreamResponseEnumerable(Guid? messageId = null, CancellationToken token = default)
    {
        ResetLastResultState();
        
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages = messages,
            CancellationToken = token
        };

        StringBuilder responseStringBuilder = new StringBuilder();
        ChatMessageRoles? responseRole = null;

        await foreach (ChatResult res in endpoint.StreamChatEnumerable(req).WithCancellation(token))
        {
            if (res.Choices is null)
            {
                yield break;
            }

            if (res.Choices.Count <= 0)
            {
                yield break;
            }

            bool solved = false;

            foreach (ChatChoice choice in res.Choices)
            {
                ChatMessage? internalDelta = choice.Delta;

                if (res.StreamInternalKind is ChatResultStreamInternalKinds.AppendAssistantMessage)
                {
                    if (internalDelta is not null)
                    {
                        internalDelta.Role = ChatMessageRoles.Assistant;
                        internalDelta.Tokens = res.Usage?.CompletionTokens;
                        AppendMessage(internalDelta);
                    }

                    solved = true;
                    break;
                }
            }

            if (solved)
            {
                continue;
            }

            foreach (ChatChoice choice in res.Choices)
            {
                if (choice.Delta is not null)
                {
                    ChatMessage delta = choice.Delta;

                    if (responseRole is null && delta.Role is not null)
                    {
                        responseRole = delta.Role;
                    }

                    if (delta.Parts?.Count > 0)
                    {
                        foreach (ChatMessagePart part in delta.Parts)
                        {
                            if (part.Type is ChatMessageTypes.Text && part.Text?.Length > 0)
                            {
                                responseStringBuilder.Append(part.Text);
                                yield return part.Text;
                            }
                        }
                    }
                    else
                    {
                        string? deltaContent = delta.Content;

                        if (deltaContent is not null && deltaContent.Length > 0)
                        {
                            responseStringBuilder.Append(deltaContent);
                            yield return deltaContent;
                        }
                    }
                }
            }

            MostRecentApiResult = res;
        }

        if (responseRole is not null)
        {
            AppendMessage((ChatMessageRoles)responseRole, responseStringBuilder.ToString(), messageId);
        }
    }

    /// <summary>
    ///     Stream LLM response as a series of events. The raw events from Provider are abstracted away and only high-level events are reported such as inbound plaintext tokens, complete tool requests, etc.
    /// </summary>
    public async Task StreamResponseRich(Guid msgId, Func<string?, ValueTask>? messageTokenHandler, Func<List<FunctionCall>, ValueTask>? functionCallHandler, Func<ChatMessageRoles, ValueTask>? messageTypeResolvedHandler, Ref<string>? outboundRequest = null, Func<ChatResponseVendorExtensions, ValueTask>? vendorFeaturesHandler = null, CancellationToken token = default)
    {
        await StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler = messageTokenHandler,
            FunctionCallHandler = functionCallHandler,
            MessageTypeResolvedHandler = messageTypeResolvedHandler,
            VendorFeaturesHandler = vendorFeaturesHandler,
            MessageId = msgId
        }, token);
    }

    /// <summary>
    ///     Stream LLM response as a series of events. The raw events from Provider are abstracted away and only high-level events are reported such as inbound plaintext tokens, complete tool requests, etc.
    /// </summary>
    public async Task StreamResponseRich(Func<string?, ValueTask>? messageTokenHandler, Func<List<FunctionCall>, ValueTask>? functionCallHandler, Func<ChatMessageRoles, ValueTask>? messageTypeResolvedHandler, Ref<string>? outboundRequest = null, Func<ChatResponseVendorExtensions, ValueTask>? vendorFeaturesHandler = null, CancellationToken token = default)
    {
        await StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenHandler = messageTokenHandler,
            FunctionCallHandler = functionCallHandler,
            MessageTypeResolvedHandler = messageTypeResolvedHandler,
            VendorFeaturesHandler = vendorFeaturesHandler
        }, token);
    }

    /// <summary>
    /// Certain providers (Google) might take some time to process uploaded files. This affects mostly videos. If the conversations contain any messages with File parts, this call verifies they are ready and if not, waits for the ready state.
    /// </summary>
    /// <param name="checkFrequencyMs">The frequency of polling, in ms</param>
    /// <param name="token">Cancellation token</param>
    public async Task<RestDataOrException<bool>> WaitForContentReady(int checkFrequencyMs = 1000, CancellationToken token = default)
    {
        IEndpointProvider provider = endpoint.Api.GetProvider(Model);

        if (provider.Provider is not LLmProviders.Google)
        {
            return new RestDataOrException<bool>(true, (HttpCallRequest?)null);
        }

        List<ChatMessage> toCheck = Messages.Where(x => x.Parts?.Any(y => y.FileLinkData?.State is not FileLinkStates.Active) ?? false).ToList();

        if (toCheck.Count is 0)
        {
            return new RestDataOrException<bool>(true, (HttpCallRequest?)null);
        }

        List<ChatMessagePart> parts = toCheck.SelectMany(x => x.Parts?.Where(y =>
        {
            if (y.FileLinkData is null)
            {
                return false;
            }

            // not files or active files
            if (y.Type is not ChatMessageTypes.FileLink || y.FileLinkData.State is FileLinkStates.Active)
            {
                return false;
            }

            // YouTube videos or other absolute sources
            if (!y.FileLinkData.FileUri.StartsWith(GoogleEndpointProvider.BaseUrl) && Uri.TryCreate(y.FileLinkData.FileUri, UriKind.Absolute, out _))
            {
                return false;
            }

            return true;
        }) ?? []).ToList();

        if (parts.Count is 0)
        {
            return new RestDataOrException<bool>(true, (HttpCallRequest?)null);
        }

        HashSet<ChatMessagePart> pendingParts = new HashSet<ChatMessagePart>(parts);
        int maxIters = checkFrequencyMs < 100 ? 100 : 20;

        try
        {
            while (pendingParts.Count > 0 && maxIters > 0)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                maxIters--;

                // select all pending parts
                Task<(ChatMessagePart part, bool isProcessing)>[] tasks = pendingParts.Select(async part =>
                {
                    if (part.FileLinkData is null)
                    {
                        return (part, isProcessing: false);
                    }

                    TornadoFile? file = await endpoint.Api.Files.Get(part.FileLinkData.FileUri, provider.Provider);

                    bool isProcessing = file?.State is FileLinkStates.Processing;

                    if (!isProcessing)
                    {
                        part.FileLinkData.State = file?.State ?? FileLinkStates.Unknown;

                        if (part.FileLinkData.File is not null)
                        {
                            part.FileLinkData.File.State = file?.State ?? FileLinkStates.Unknown;
                        }
                    }

                    return (part, isProcessing);
                }).ToArray();

                (ChatMessagePart part, bool isProcessing)[] results = await Task.WhenAll(tasks);

                // remove done parts
                foreach ((ChatMessagePart part, bool isProcessing) in results)
                {
                    if (!isProcessing)
                    {
                        pendingParts.Remove(part);
                    }
                }

                if (pendingParts.Count > 0)
                {
                    await Task.Delay(checkFrequencyMs, token);
                }
            }
        }
        catch (Exception e)
        {
            return new RestDataOrException<bool>(e);
        }

        return new RestDataOrException<bool>(true, (HttpCallRequest?)null);
    }

    /// <summary>
    ///     Stream LLM response as a series of events. The raw events from Provider are abstracted away and only high-level events are reported such as inbound plaintext tokens, complete tool requests, etc.
    /// </summary>
    /// <param name="eventsHandler"></param>
    /// <param name="token"></param>
    public async Task StreamResponseRich(ChatStreamEventHandler? eventsHandler, CancellationToken token = default)
    {
        ResetLastResultState();
        
        ChatRequest req = new ChatRequest(this, RequestParameters)
        {
            Messages = messages,
            CancellationToken = token,
            Stream = true
        };

        req.StreamOptions ??= ChatStreamOptions.KnownOptionsIncludeUsage;

        if (!req.StreamOptions.IncludeUsage)
        {
            req.StreamOptions = null;
        }

        TornadoRequestContentWithProvider serialized = ChatRequest.Serialize(endpoint.Api, req);
        IEndpointProvider provider = serialized.Provider;

        req = eventsHandler?.MutateChatRequestHandler is not null
            ? await eventsHandler.MutateChatRequestHandler.Invoke(req)
            : req;
        bool isFirst = true;
        Guid currentMsgId = eventsHandler?.MessageId ?? Guid.NewGuid();
        ChatMessage? lastUserMessage = messages.LastOrDefault(x => x.Role is ChatMessageRoles.User);
        bool isFirstMessageToken = true;
        int tokenIndex = 0;

        CapabilityEndpoints capabilityEndpoint = req.GetCapabilityEndpoint();

        if (capabilityEndpoint is CapabilityEndpoints.Responses)
        {
            ResponseRequest responsesRequest = ResponseHelpers.ToResponseRequest(provider, req.ResponseRequestParameters, req);

            await responsesEndpoint.StreamResponseRich(responsesRequest, new ResponseStreamEventHandler
            {
                OnEvent = async (evt) =>
                {
                    if (eventsHandler?.OnResponseEvent is not null)
                    {
                        await eventsHandler.OnResponseEvent.Invoke(evt);
                    }

                    switch (evt.EventType)
                    {
                        case ResponseEventTypes.ResponseOutputTextDelta when
                            evt is ResponseEventOutputTextDelta deltaEvt:
                            {
                                if (eventsHandler?.MessageTokenHandler is not null)
                                {
                                    await eventsHandler.MessageTokenHandler.Invoke(deltaEvt.Delta);
                                }

                                break;
                            }
                        case ResponseEventTypes.ResponseReasoningSummaryPartDone when
                            evt is ResponseEventReasoningSummaryPartDone reasoningPartDoneEvt:
                            {
                                if (eventsHandler?.ReasoningTokenHandler is not null)
                                {
                                    await eventsHandler.ReasoningTokenHandler.Invoke(new ChatMessageReasoningData
                                    {
                                        Content = reasoningPartDoneEvt.Part.Text
                                    });
                                }

                                break;
                            }
                        case ResponseEventTypes.ResponseCompleted when
                            evt is ResponseEventCompleted completedEvt:
                            {
                                ChatChoice chatChoice = ResponseHelpers.ToChatChoice(completedEvt.Response, responsesRequest, provider);

                                if (chatChoice.Message is not null)
                                {
                                    AppendMessage(chatChoice.Message);
                                }

                                if (completedEvt.Response.Tools?.Count > 0)
                                {
                                    object? structuredResult = null;

                                    if (chatChoice.Message is not null && (eventsHandler?.FunctionCallHandler is not null || eventsHandler?.ToolCallsHandler is not null))
                                    {
                                        structuredResult = await ChatEndpoint.HandleChatResult(req, chatChoice.Message, null).ConfigureAwait(false);
                                        ParsedToolCalls parsedCalls = ParseCalls(chatChoice.Message);
                                        ResolvedToolsCall result = new ResolvedToolsCall();

                                        if (parsedCalls.FunctionCalls.Count > 0)
                                        {
                                            if (eventsHandler.FunctionCallHandler is not null)
                                            {
                                                await eventsHandler.FunctionCallHandler.Invoke(parsedCalls.FunctionCalls);
                                            }

                                            foreach (FunctionCall call in parsedCalls.FunctionCalls)
                                            {
                                                ChatMessage fnResultMsg = new ChatMessage(ChatMessageRoles.Tool,
                                                    call.Result?.Content ?? "The service returned no data.".ToJson(),
                                                    Guid.NewGuid())
                                                {
                                                    ToolCallId = call.ToolCall?.Id ?? call.Name,
                                                    ToolInvocationSucceeded = call.Result?.InvocationSucceeded ?? false,
                                                    ContentJsonType = call.Result?.ContentJsonType ?? typeof(string),
                                                    FunctionCall = call
                                                };

                                                AppendMessage(fnResultMsg);

                                                result.ToolResults.Add(new ResolvedToolCall
                                                {
                                                    Call = call,
                                                    Result = call.Result ?? new FunctionResult(call, null, null, false),
                                                    ToolMessage = fnResultMsg
                                                });
                                            }

                                            if (eventsHandler.AfterFunctionCallsResolvedHandler is not null)
                                            {
                                                await eventsHandler.AfterFunctionCallsResolvedHandler.Invoke(result, eventsHandler);
                                            }

                                            if (OnAfterToolsCall is not null)
                                            {
                                                await OnAfterToolsCall(result);
                                            }
                                        }

                                        return;
                                    }
                                }


                                if (eventsHandler?.OnFinished is not null)
                                {
                                    ChatUsage usage = new ChatUsage(LLmProviders.OpenAi);

                                    if (completedEvt.Response.Usage != null)
                                    {
                                        usage = new ChatUsage(completedEvt.Response.Usage);
                                    }

                                    await eventsHandler.OnFinished.Invoke(new ChatStreamFinishedData
                                    (
                                        usage,
                                        completedEvt.Response.Error is not null
                                            ? ChatMessageFinishReasons.Error
                                            : ChatMessageFinishReasons.EndTurn
                                    ));
                                }

                                break;
                            }
                        case ResponseEventTypes.ResponseOutputTextDone when evt is ResponseEventOutputTextDone doneEvt:
                            {
                                if (eventsHandler?.BlockFinishedHandler is not null)
                                {
                                    //TODO:
                                    // await eventsHandler.BlockFinishedHandler.Invoke(doneEvt.);
                                }

                                break;
                            }
                    }
                }
            }, token);
        }
        else
        {
            await foreach (ChatResult res in endpoint.StreamChatReal(serialized, req, eventsHandler).WithCancellation(token))
            {
                bool solved = false;

                // internal events are resolved immediately, we never return control to the user.
                if (res.StreamInternalKind is not null)
                {
                    if (res.Choices is not null)
                    {
                        if (res.StreamInternalKind is ChatResultStreamInternalKinds.FinishData)
                        {
                            if (eventsHandler?.OnFinished is not null)
                            {
                                ChatChoice? finishChoice = res.Choices.FirstOrDefault();
                                await eventsHandler.OnFinished.Invoke(new ChatStreamFinishedData(
                                    res.Usage ?? new ChatUsage(provider.Provider),
                                    finishChoice?.FinishReason ?? ChatMessageFinishReasons.Unknown,
                                    finishChoice?.StopReason,
                                    finishChoice?.StopDetails));
                            }

                            break;
                        }

                        foreach (ChatChoice choice in res.Choices)
                        {
                            ChatMessage? internalDelta = choice.Delta;

                            if (res.StreamInternalKind is ChatResultStreamInternalKinds.AssistantMessageTransientBlock)
                            {
                                if (eventsHandler?.BlockFinishedHandler is not null)
                                {
                                    await eventsHandler.BlockFinishedHandler.Invoke(internalDelta);
                                }

                                solved = true;
                                break;
                            }

                            if (res.StreamInternalKind is ChatResultStreamInternalKinds.AppendAssistantMessage)
                            {
                                if (internalDelta is not null)
                                {
                                    internalDelta.Role = ChatMessageRoles.Assistant;
                                    internalDelta.Id = currentMsgId;
                                    internalDelta.Tokens = res.Usage?.CompletionTokens;

                                    if (lastUserMessage is not null)
                                    {
                                        lastUserMessage.Tokens = res.Usage?.PromptTokens;
                                    }

                                    currentMsgId = Guid.NewGuid();
                                    AppendMessage(internalDelta);
                                }

                                if (eventsHandler?.BlockFinishedHandler is not null)
                                {
                                    await eventsHandler.BlockFinishedHandler.Invoke(internalDelta);
                                }

                                if (res.Usage is not null && eventsHandler?.OnUsageReceived is not null)
                                {
                                    await eventsHandler.OnUsageReceived.Invoke(res.Usage);
                                }

                                solved = true;
                                break;
                            }
                        }
                    }
                }

                if (solved)
                {
                    continue;
                }

                if (res.Choices is null || res.Choices.Count is 0)
                {
                    if (res.VendorExtensions is not null && eventsHandler?.VendorFeaturesHandler is not null)
                    {
                        await eventsHandler.VendorFeaturesHandler.Invoke(res.VendorExtensions);
                    }

                    MostRecentApiResult = res;
                    continue;
                }

                foreach (ChatChoice choice in res.Choices)
                {
                    ChatMessage? delta = choice.Delta;
                    ChatMessage? message = choice.Message;

                    if (isFirst && delta?.Role is not null)
                    {
                        if (eventsHandler?.MessageTypeResolvedHandler is not null)
                        {
                            await eventsHandler.MessageTypeResolvedHandler(delta.Role ?? ChatMessageRoles.Unknown);
                        }

                        isFirst = false;
                    }

                    if (delta?.ToolCalls?.Count > 0)
                    {
                        await ChatEndpoint.HandleChatResult(req, delta, res).ConfigureAwait(false);
                    }

                    if (delta is not null && eventsHandler is not null)
                    {
                        // role can be either Tool or Assistant, we need to handle both cases
                        if (delta.Role is ChatMessageRoles.Tool || delta.ToolCalls?.Count > 0)
                        {
                            delta.Role = ChatMessageRoles.Assistant;
                            ParsedToolCalls parsedCalls = ParseCalls(delta);
                            ValueTask? fnTask = null, customTask = null;

                            if (eventsHandler.FunctionCallHandler is not null && parsedCalls.FunctionCalls.Count > 0)
                            {
                                fnTask = eventsHandler.FunctionCallHandler.Invoke(parsedCalls.FunctionCalls);
                            }

                            if (eventsHandler.CustomToolCallHandler is not null && parsedCalls.CustomToolCalls.Count > 0)
                            {
                                customTask = eventsHandler.CustomToolCallHandler.Invoke(parsedCalls.CustomToolCalls);
                            }

                            await Threading.WhenAll(fnTask, customTask);

                            // NOTE: this used to overwrite delta.Content with
                            // MostRecentApiResult.Object when the finish reason was ToolCalls.
                            // ApiResultBase.Object is the API "object" field (e.g.
                            // "chat.completion.chunk"), never the message text, so it clobbered
                            // the assistant content the provider had already attached (and, when
                            // the provider attached nothing, injected the bogus object string into
                            // the conversation history). Providers are responsible for populating
                            // the tool-call message content; see OpenAiEndpointProvider which now
                            // preserves the streamed plaintext.

                            if (lastUserMessage is not null)
                            {
                                lastUserMessage.Tokens = res.Usage?.PromptTokens;
                            }

                            if (res.Usage is not null && eventsHandler.OnUsageReceived is not null)
                            {
                                await eventsHandler.OnUsageReceived.Invoke(res.Usage);
                            }

                            delta.Tokens = res.Usage?.CompletionTokens;
                            AppendMessage(delta);

                            if (eventsHandler.FunctionCallHandler is not null || eventsHandler.ToolCallsHandler is not null || eventsHandler.CustomToolCallHandler is not null)
                            {
                                ResolvedToolsCall result = new ResolvedToolsCall();

                                if (parsedCalls.FunctionCalls.Count > 0 || parsedCalls.CustomToolCalls.Count > 0)
                                {
                                    result.AssistantMessage = delta;

                                    foreach (FunctionCall call in parsedCalls.FunctionCalls)
                                    {
                                        ChatMessage fnResultMsg = new ChatMessage(ChatMessageRoles.Tool,
                                            call.Result?.Content ?? "The service returned no data.".ToJson(),
                                            Guid.NewGuid())
                                        {
                                            Id = currentMsgId,
                                            ToolCallId = call.ToolCall?.Id ?? call.Name,
                                            ToolInvocationSucceeded = call.Result?.InvocationSucceeded ?? false,
                                            ContentJsonType = call.Result?.ContentJsonType ?? typeof(string),
                                            FunctionCall = call
                                        };

                                        currentMsgId = Guid.NewGuid();
                                        AppendMessage(fnResultMsg);

                                        result.ToolResults.Add(new ResolvedToolCall
                                        {
                                            Call = call,
                                            Result = call.Result ?? new FunctionResult(call, null, null, false),
                                            ToolMessage = fnResultMsg
                                        });
                                    }

                                    foreach (CustomToolCall call in parsedCalls.CustomToolCalls)
                                    {
                                        ChatMessage fnResultMsg = new ChatMessage(ChatMessageRoles.Tool,
                                            call.Result?.Content ?? "The service returned no data.",
                                            Guid.NewGuid())
                                        {
                                            Id = currentMsgId,
                                            ToolCallId = call.ToolCall?.Id ?? call.Name,
                                            ToolInvocationSucceeded = call.Result?.InvocationSucceeded ?? false,
                                            ContentJsonType = call.Result?.ContentJsonType ?? typeof(string),
                                            CustomToolCall = call
                                        };

                                        currentMsgId = Guid.NewGuid();
                                        AppendMessage(fnResultMsg);

                                        result.ToolResults.Add(new ResolvedToolCall
                                        {
                                            CustomCall = call,
                                            CustomResult = call.Result ?? new CustomToolCallResult(call, null),
                                            ToolMessage = fnResultMsg
                                        });
                                    }

                                    if (eventsHandler.AfterFunctionCallsResolvedHandler is not null)
                                    {
                                        await eventsHandler.AfterFunctionCallsResolvedHandler.Invoke(result,
                                            eventsHandler);
                                    }

                                    if (OnAfterToolsCall is not null)
                                    {
                                        await OnAfterToolsCall(result);
                                    }
                                }
                            }

                            if (eventsHandler.OnFinished is not null)
                            {
                                ChatChoice? finishChoice = MostRecentApiResult?.Choices?.FirstOrDefault();
                                await eventsHandler.OnFinished.Invoke(new ChatStreamFinishedData(
                                    res.Usage ?? new ChatUsage(provider.Provider), 
                                    finishChoice?.FinishReason ?? ChatMessageFinishReasons.ToolCalls,
                                    finishChoice?.StopReason));
                            }

                            return;
                        }
                        else if (delta.Role is ChatMessageRoles.Assistant)
                        {
                            if (delta.Parts?.Count > 0)
                            {
                                foreach (ChatMessagePart part in delta.Parts)
                                {
                                    if (eventsHandler.MessagePartHandler is not null)
                                    {
                                        await InvokeMessagePartHandler(part);
                                    }

                                    switch (part.Type)
                                    {
                                        case ChatMessageTypes.Text:
                                            {
                                                await InvokeMessageHandler(part.Text ?? message?.Content);
                                                break;
                                            }
                                        case ChatMessageTypes.Reasoning
                                            when eventsHandler.ReasoningTokenHandler is not null:
                                            {
                                                ChatMessageReasoningData? msg = part.Reasoning;

                                                if (RequestParameters.TrimResponseStart && isFirstMessageToken &&
                                                    msg is not null)
                                                {
                                                    msg.Content = msg.Content?.TrimStart();
                                                    isFirstMessageToken = false;
                                                }

                                                if (msg is not null)
                                                {
                                                    await eventsHandler.ReasoningTokenHandler.Invoke(msg);
                                                }

                                                break;
                                            }
                                        case ChatMessageTypes.Image when eventsHandler.ImageTokenHandler is not null:
                                            {
                                                if (part.Image is not null)
                                                {
                                                    await eventsHandler.ImageTokenHandler.Invoke(part.Image);
                                                }

                                                break;
                                            }
                                    }
                                }
                            }
                            else
                            {
                                if (delta.ReasoningTokens is not null)
                                {
                                    if (eventsHandler.ReasoningTokenHandler is not null)
                                    {
                                        await eventsHandler.ReasoningTokenHandler.Invoke(new ChatMessageReasoningData
                                        {
                                            Content  = delta.ReasoningTokens,
                                            Provider = provider.Provider
                                        });
                                    }
                                }

                                if (eventsHandler.MessagePartHandler is not null && !string.IsNullOrEmpty(delta.Content ?? message?.Content))
                                {
                                    await InvokeMessagePartHandler(new ChatMessagePart
                                    {
                                        Type = ChatMessageTypes.Text,
                                        Text = delta.Content ?? message?.Content
                                    });
                                }

                                if (delta.Content is not null)
                                {
                                    await InvokeMessageHandler(delta.Content);
                                }
                            }

                            if (delta.Audio is not null)
                            {
                                if (eventsHandler.AudioTokenHandler is not null)
                                {
                                    await eventsHandler.AudioTokenHandler.Invoke(delta.Audio);
                                }
                            }
                        }
                    }
                }

                continue;

                async ValueTask InvokeMessagePartHandler(ChatMessagePart part)
                {
                    if (eventsHandler.MessagePartHandler is null)
                    {
                        return;
                    }

                    if (part.Text is not null)
                    {
                        if (RequestParameters.TrimResponseStart && isFirstMessageToken)
                        {
                            part.Text = part.Text.TrimStart();
                            isFirstMessageToken = false;
                        }
                    }

                    await eventsHandler.MessagePartHandler.Invoke(part);
                }

                async ValueTask InvokeMessageHandler(string? msg)
                {
                    if (eventsHandler.MessageTokenExHandler is not null)
                    {
                        if (msg is not null)
                        {
                            if (RequestParameters.TrimResponseStart && isFirstMessageToken)
                            {
                                msg = msg.TrimStart();
                                isFirstMessageToken = false;
                            }

                            await eventsHandler.MessageTokenExHandler.Invoke(new StreamedMessageToken
                            {
                                Content = msg,
                                Index = tokenIndex
                            });

                            tokenIndex++;
                        }
                    }

                    if (eventsHandler.MessageTokenHandler is null)
                    {
                        return;
                    }

                    if (msg is not null)
                    {
                        if (RequestParameters.TrimResponseStart && isFirstMessageToken)
                        {
                            msg = msg.TrimStart();
                            isFirstMessageToken = false;
                        }

                        await eventsHandler.MessageTokenHandler.Invoke(msg);
                    }
                }
            }
        }
    }

    #endregion


}
