using System.Net;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;

namespace LlmTornado.Tests;

/// <summary>
/// Regression test for the streaming bug where the assistant text produced before
/// tool calls was dropped, so <see cref="Conversation.StreamResponseRich"/> appended
/// the tool-call message without it (and then overwrote Content with
/// <see cref="ApiResultBase.Object"/>, the API "object" type). Continuations re-sent
/// only [system, user, assistant(tool_call, no text), tool result] and the model
/// re-answered the original prompt on every tool round.
/// </summary>
[TestFixture]
public class OpenAiStreamToolCallContentTests
{
    private const string AssistantText = "Let me check the weather for you.";

    [Test]
    public async Task InboundStream_WhenToolCallsFollowText_KeepsTheTextOnTheToolCallMessage()
    {
        OpenAiEndpointProvider provider = new OpenAiEndpointProvider();

        ChatRequest request = new ChatRequest
        {
            Tools = BuildTools()
        };

        using StreamReader reader = new StreamReader(
            new MemoryStream(Encoding.UTF8.GetBytes(SseWithTextThenToolCall())));

        ChatResult? toolCallResult = null;

        await foreach (ChatResult? res in provider.InboundStream(reader, request, null))
        {
            if (res?.Choices is { Count: > 0 } && res.Choices[0].Delta?.ToolCalls?.Count > 0)
            {
                toolCallResult = res;
            }
        }

        Assert.That(toolCallResult, Is.Not.Null, "the streaming tool-call result should be yielded");
        Assert.That(
            toolCallResult!.Choices![0].Delta!.Content,
            Is.EqualTo(AssistantText),
            "assistant text streamed before the tool call must survive on the tool-call message");
    }

    /// <summary>
    /// End-to-end counterpart: the message appended to the conversation history must
    /// carry the assistant text (not ApiResultBase.Object). Uses Blablador because no
    /// other test creates an HTTP client for it, so the static client cache stays clean.
    /// </summary>
    [Test]
    public async Task StreamResponseRich_WhenToolCallsFollowText_HistoryAssistantMessageKeepsTheText()
    {
        TornadoConfig.CreateClientAsync = _ => Task.FromResult<HttpClient?>(new HttpClient(new SseHttpHandler(SseWithTextThenToolCall())));
        try
        {
            TornadoApi api = new TornadoApi(LLmProviders.Blablador, "test-key");
            Conversation conversation = api.Chat.CreateConversation(new ChatRequest
            {
                Model = new ChatModel("test-model", LLmProviders.Blablador),
                Tools = BuildTools()
            });
            conversation.AppendUserInput("What's the weather in Paris?");

            await conversation.StreamResponseRich(new ChatStreamEventHandler
            {
                FunctionCallHandler = _ => ValueTask.CompletedTask
            });

            ChatMessage? toolCallMessage = conversation.Messages
                .LastOrDefault(m => m.Role is ChatMessageRoles.Assistant && m.ToolCalls?.Count > 0);

            Assert.That(toolCallMessage, Is.Not.Null, "the assistant tool-call message should be appended");
            Assert.That(
                toolCallMessage!.Content,
                Is.EqualTo(AssistantText),
                "the appended assistant message must keep the streamed text, not ApiResultBase.Object");
        }
        finally
        {
            TornadoConfig.CreateClientAsync = null;
        }
    }

    private static List<Tool> BuildTools() =>
    [
        new Tool(new ToolFunction("get_weather", "Gets the current weather", new
        {
            type = "object",
            properties = new { location = new { type = "string" } },
            required = new[] { "location" }
        }))
    ];

    private static string SseWithTextThenToolCall()
    {
        return
            "data: {\"id\":\"1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Let me check\"},\"finish_reason\":null}]}\n\n" +
            "data: {\"id\":\"1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\" the weather for you.\"},\"finish_reason\":null}]}\n\n" +
            "data: {\"id\":\"1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"get_weather\",\"arguments\":\"{\\\"location\\\":\\\"Paris\\\"}\"}}]},\"finish_reason\":null}]}\n\n" +
            "data: {\"id\":\"1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}\n\n" +
            "data: [DONE]\n\n";
    }

    private sealed class SseHttpHandler : HttpMessageHandler
    {
        private readonly string _sse;

        public SseHttpHandler(string sse)
        {
            _sse = sse;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(_sse, Encoding.UTF8, "text/event-stream")
            });
        }
    }
}

