using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicMessageThreadSessionTests
{
    private static ChatMessage User(string text) => new(ChatMessageRoles.User, text);
    private static ChatRequest Prototype() => new()
    {
        Model = "claude-sonnet-4-6", MaxTokens = 1024,
        Messages = [new(ChatMessageRoles.System, "Be useful."), User("Initial input")],
        VendorExtensions = new(new ChatRequestVendorAnthropicExtensions { UserProfileId = "uprof_customer" })
    };

    private static Reply Message(string id, string stop = "end_turn", string? content = null, bool fallback = false)
        => new(new JObject
        {
            ["id"] = id, ["type"] = "message", ["role"] = "assistant", ["model"] = "claude-sonnet-4-6",
            ["content"] = JArray.Parse(content ?? """[{"type":"text","text":"Answer"}]"""), ["stop_reason"] = stop,
            ["usage"] = new JObject { ["input_tokens"] = 4, ["output_tokens"] = 2,
                ["iterations"] = fallback ? new JArray(new JObject { ["type"] = "fallback_message", ["model"] = "claude-sonnet-4-6" }) : new JArray() }
        }.ToString());

    private static Reply Error(string code, int status = 400) => new(new JObject
    {
        ["type"] = "error", ["error"] = new JObject { ["type"] = status == 404 ? "not_found_error" : "invalid_request_error",
            ["message"] = "Rejected", ["details"] = new JObject { ["error_code"] = code } }
    }.ToString(), status);

    [TestCase("thread_not_found", 404, false)]
    [TestCase("thread_already_continued", 400, false)]
    [TestCase("thread_continue_in_flight", 400, true)]
    public async Task Recovery_ReplaysNativeHistoryOnce_AndContinuesFromTheReplay(string code, int status, bool strict)
    {
        const string blocks = """[{"type":"thinking","thinking":"thought","signature":"signed"},{"type":"server_tool_use","id":"srvtoolu_1","name":"web_search","input":{"query":"news"}},{"type":"web_search_tool_result","tool_use_id":"srvtoolu_1","content":[{"type":"web_search_result","url":"https://example.com","title":"source","encrypted_content":"opaque"}]},{"type":"text","text":"Answer","citations":[{"type":"web_search_result_location","url":"https://example.com","title":"source","cited_text":"quoted","encrypted_index":"index"}]}]""";
        await using FakeServer server = new(Message("msg_first", content: blocks), Error(code, status), Message("msg_replay"), Message("msg_next"));
        server.Api.HttpStrict = strict;
        ChatRequest prototype = Prototype();
        var session = server.Api.Chat.CreateAnthropicThread(prototype);
        prototype.Messages![0].Content = "Mutated system";
        prototype.VendorExtensions!.Anthropic!.UserProfileId = "uprof_different";
        await session.Send(cancellationToken: server.Token);
        await session.Send([User("Second turn")], server.Token);
        Assert.That(session.PreviousMessageId, Is.EqualTo("msg_replay"));
        await session.Send([User("Third turn")], server.Token);

        Assert.That(server.Requests, Has.Count.EqualTo(4));
        var first = server.Requests[0]; var continuation = server.Requests[1]; var replay = server.Requests[2];
        Assert.That((string?)first.Body["system"]?[0]?["text"], Is.EqualTo("Be useful."));
        Assert.That(continuation.Body["system"], Is.Null);
        Assert.That(continuation.Body["tools"], Is.Null);
        Assert.That((JArray)continuation.Body["messages"]!, Has.Count.EqualTo(1));
        Assert.That((string?)continuation.Body["thread"]?["previous_message_id"], Is.EqualTo("msg_first"));
        Assert.That((string?)replay.Body["thread"]?["type"], Is.EqualTo("create"));
        Assert.That((JArray)replay.Body["messages"]!, Has.Count.EqualTo(3));
        Assert.That(JToken.DeepEquals(replay.Body["messages"]![1]!["content"], JArray.Parse(blocks)), Is.True);
        Assert.That((string?)server.Requests[3].Body["thread"]?["previous_message_id"], Is.EqualTo("msg_replay"));
        Assert.That(server.Requests.Select(x => x.Betas).Distinct().ToArray(), Has.Length.EqualTo(1));
        Assert.That(server.Requests.All(x => x.Profile == "uprof_customer"), Is.True);
    }

    [TestCase("model_not_found", 404)]
    [TestCase("thread_fingerprint_mismatch", 400)]
    [TestCase("thread_unsupported_request", 400)]
    [TestCase("thread_not_found", 500)]
    public async Task OtherErrors_AreSurfacedWithoutReplay_AndInputRemainsStaged(string code, int status)
    {
        await using FakeServer server = new(Message("msg_first"), Error(code, status));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        await session.Send(cancellationToken: server.Token);
        Assert.ThrowsAsync<HttpRequestException>(async () => await session.Send([User("New input")], server.Token));
        Assert.That(server.Requests, Has.Count.EqualTo(2));
        Assert.That(session.Messages, Has.Count.EqualTo(3));
        Assert.That((string?)session.Messages[2]["content"]?[0]?["text"], Is.EqualTo("New input"));
    }

    [Test]
    public async Task FailedReplay_IsNotRetriedIndefinitely()
    {
        await using FakeServer server = new(Message("msg_first"), Error("thread_not_found", 404), Error("thread_not_found", 404));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        await session.Send(cancellationToken: server.Token);
        Assert.ThrowsAsync<HttpRequestException>(async () => await session.Send([User("Next")], server.Token));
        Assert.That(server.Requests, Has.Count.EqualTo(3));
        Assert.That(session.PreviousMessageId, Is.Null);
    }

    [Test]
    public async Task Refusal_RollsBackOnlySendInput_AndPreservesStagedInput()
    {
        await using FakeServer server = new(Message("msg_first"), Message("msg_refused", "refusal"), Message("msg_new"));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        await session.Send(cancellationToken: server.Token);
        session.Append([User("Staged")]);
        ChatResult refusal = await session.Send([User("Refused input")], server.Token);
        Assert.That(refusal.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.Refusal));
        Assert.That(session.LastMessage!.Id, Is.EqualTo("msg_first"));
        Assert.That(session.PreviousMessageId, Is.Null);
        Assert.That(session.Messages.ToString(), Does.Not.Contain("Refused input"));
        await session.Send([User("Rephrased")], server.Token);
        Assert.That((string?)server.Requests[2].Body["thread"]?["type"], Is.EqualTo("create"));
        Assert.That((JArray)server.Requests[2].Body["messages"]!, Has.Count.EqualTo(4));
        Assert.That(server.Requests[2].Body["messages"]!.ToString(), Does.Contain("Staged"));
    }

    [Test]
    public async Task PauseAndFallback_ChooseEmptyContinuationThenFullCreate()
    {
        await using FakeServer server = new(Message("msg_paused", "pause_turn"), Message("msg_fallback", fallback: true), Message("msg_new"));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        await session.Send(cancellationToken: server.Token);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send([User("Too early")], server.Token));
        await session.Send(cancellationToken: server.Token);
        Assert.That(server.Requests[1].Body["messages"], Is.Empty);
        Assert.That(session.PreviousMessageId, Is.Null);
        Assert.That(session.LastMessage!.Id, Is.EqualTo("msg_fallback"));
        await session.Send([User("Next")], server.Token);
        Assert.That((string?)server.Requests[2].Body["thread"]?["type"], Is.EqualTo("create"));
        Assert.That((JArray)server.Requests[2].Body["messages"]!, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task UnsupportedPolicy_UsesFullHistoryAndRemovesOnlyTheThreadBeta()
    {
        await using FakeServer server = new(Message("msg_first"), Error("thread_unsupported_request"), Message("msg_plain"), Message("msg_plain2"));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype(), continueUnthreaded: true);
        await session.Send(cancellationToken: server.Token);
        await session.Send([User("Next")], server.Token);
        await session.Send([User("More")], server.Token);
        Assert.That(session.IsUnthreaded, Is.True);
        Assert.That(session.PreviousMessageId, Is.Null);
        Assert.That(server.Requests[2].Body["thread"], Is.Null);
        Assert.That(server.Requests[2].Body["system"], Is.Not.Null);
        Assert.That((JArray)server.Requests[3].Body["messages"]!, Has.Count.EqualTo(5));
        Assert.That(server.Requests[2].Betas, Does.Not.Contain(AnthropicMessageThread.BetaHeader));
        Assert.That(server.Requests[2].Betas, Does.Contain("files-api-2025-04-14"));
    }

    [Test]
    public async Task RepeatedExpiry_AfterReplayFallsBackOnlyWhenOptedIn()
    {
        await using FakeServer server = new(Message("msg_first"), Error("thread_not_found", 404), Message("msg_replayed"), Error("thread_not_found", 404), Message("msg_plain"));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype(), continueUnthreaded: true);
        await session.Send(cancellationToken: server.Token);
        await session.Send([User("Next")], server.Token);
        await session.Send([User("More")], server.Token);
        Assert.That((string?)server.Requests[2].Body["thread"]?["type"], Is.EqualTo("create"));
        Assert.That(server.Requests[4].Body["thread"], Is.Null);
        Assert.That(session.IsUnthreaded, Is.True);
    }

    [Test]
    public async Task ForkAndExport_AreIndependent_AndReplayDropsOnlyOldMessageCacheBreakpoints()
    {
        await using FakeServer server = new(Message("msg_first"), Message("msg_fork"), Message("msg_parent"), Message("msg_restored"));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        session.Update(JObject.Parse("""{"cache_control":{"type":"ephemeral"},"tools":[{"name":"check","description":"Check","input_schema":{"type":"object"},"cache_control":{"type":"ephemeral"}}]}"""));
        session.Append(JArray.Parse("""[{"role":"user","content":[{"type":"text","text":"Cached input","cache_control":{"type":"ephemeral"}}]}]"""));
        await session.Send(cancellationToken: server.Token);
        var fork = session.Fork();
        fork.Update(JObject.Parse("""{"system":"Fork system","max_tokens":2048}"""));
        fork.Append(JArray.Parse("""[{"role":"user","content":[{"type":"text","text":"New breakpoint","cache_control":{"type":"ephemeral"}}]}]"""));
        await fork.Send(cancellationToken: server.Token);
        await session.Send([User("Parent only")], server.Token);
        Assert.That(server.Requests[1].Body["messages"]![1]!["content"]![0]!["cache_control"], Is.Null);
        Assert.That(server.Requests[1].Body["messages"]![3]!["content"]![0]!["cache_control"], Is.Not.Null);
        Assert.That(server.Requests[1].Body["tools"]![0]!["cache_control"], Is.Not.Null);
        Assert.That(server.Requests[1].Body["cache_control"], Is.Not.Null);
        Assert.That(server.Requests[2].Body["tools"], Is.Null);
        Assert.That((string?)server.Requests[2].Body["thread"]?["previous_message_id"], Is.EqualTo("msg_first"));
        var exported = session.Export();
        var restored = server.Api.Chat.CreateAnthropicThread(exported);
        exported.Parameters["messages"] = new JArray();
        Assert.That(restored.Messages, Has.Count.EqualTo(5));
        await restored.Send([User("Restored")], server.Token);
        Assert.That((string?)server.Requests[3].Body["thread"]?["type"], Is.EqualTo("create"));
        Assert.That((JArray)server.Requests[3].Body["messages"]!, Has.Count.EqualTo(6));
        Assert.That(session.Messages.ToString(), Does.Not.Contain("New breakpoint"));
    }

    [Test]
    public async Task FixedFieldsCannotChangeAfterResponse_ButTokensAndRequestLimitsCan()
    {
        await using FakeServer server = new(Message("msg_first"), Message("msg_next"));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        session.Update(JObject.Parse("""{"mcp_servers":[{"type":"url","name":"service","url":"https://example.com/mcp","authorization_token":"old"}]}"""));
        await session.Send(cancellationToken: server.Token);
        Assert.Throws<InvalidOperationException>(() => session.Update(new JObject { ["system"] = "changed" }));
        Assert.Throws<InvalidOperationException>(() => session.Update(new JObject { ["model"] = "claude-opus-5-5" }));
        Assert.Throws<InvalidOperationException>(() => session.Update(new JObject { ["fallbacks"] = new JArray() }));
        session.Update(JObject.Parse("""{"max_tokens":2048,"mcp_servers":[{"type":"url","name":"service","url":"https://example.com/mcp","authorization_token":"new"}]}"""));
        await session.Send([User("Next")], server.Token);
        Assert.That((int?)server.Requests[1].Body["max_tokens"], Is.EqualTo(2048));
        Assert.That((string?)server.Requests[1].Body["mcp_servers"]?[0]?["authorization_token"], Is.EqualTo("new"));
    }

    private static string Event(string type, string data) => $"event: {type}\ndata: {data}\n\n";
    private static string Start(string id) => Event("message_start", new JObject
    {
        ["type"] = "message_start", ["message"] = new JObject
        {
            ["id"] = id, ["type"] = "message", ["role"] = "assistant", ["model"] = "claude-sonnet-4-6",
            ["content"] = new JArray(), ["usage"] = new JObject { ["input_tokens"] = 4, ["output_tokens"] = 0 }
        }
    }.ToString(Newtonsoft.Json.Formatting.None));
    private static string TextBlock(string text, bool stop = true) => Event("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""")
        + Event("content_block_delta", new JObject { ["type"] = "content_block_delta", ["index"] = 0, ["delta"] = new JObject { ["type"] = "text_delta", ["text"] = text } }.ToString(Newtonsoft.Json.Formatting.None))
        + (stop ? Event("content_block_stop", """{"type":"content_block_stop","index":0}""") : "");
    private static string Finish(string reason = "end_turn", bool stop = true, bool fallback = false) => Event("message_delta", new JObject
    {
        ["type"] = "message_delta", ["delta"] = new JObject { ["stop_reason"] = reason },
        ["usage"] = new JObject { ["output_tokens"] = 3, ["iterations"] = fallback ? new JArray(new JObject { ["type"] = "fallback_message", ["model"] = "claude-sonnet-4-6" }) : new JArray() }
    }.ToString(Newtonsoft.Json.Formatting.None)) + (stop ? Event("message_stop", """{"type":"message_stop"}""") : "");

    [Test]
    public async Task Streaming_RecordsOnlyCompletedResponses_AndRecoversBeforeReadingData()
    {
        await using FakeServer server = new(Message("msg_first"), Error("thread_not_found", 404),
            new Reply(Start("msg_stream") + TextBlock("Streamed answer") + Finish(), Stream: true), Message("msg_next"));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        await session.Send(cancellationToken: server.Token);
        List<ChatResult> chunks = [];
        await foreach (var chunk in session.Stream([User("Stream now")], server.Token)) chunks.Add(chunk);
        Assert.That(session.PreviousMessageId, Is.EqualTo("msg_stream"));
        Assert.That(session.LastMessage!.Choices![0].Message!.Content, Is.EqualTo("Streamed answer"));
        Assert.That(chunks.SelectMany(x => x.Choices ?? []).Any(x => x.Delta?.Content == "Streamed answer"), Is.True);
        await session.Send([User("After stream")], server.Token);
        Assert.That((string?)server.Requests[3].Body["thread"]?["previous_message_id"], Is.EqualTo("msg_stream"));
        Assert.That((JArray)server.Requests[2].Body["messages"]!, Has.Count.EqualTo(3));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StreamDisposedEarly_RetainsPartialText_AndAllowsAFullReplay(bool retryInterrupted)
    {
        await using FakeServer server = new(new Reply(Start("msg_partial") + TextBlock("Partial answer", false), Stream: true), Message("msg_retry"));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        await foreach (var chunk in session.Stream(cancellationToken: server.Token))
        {
            if (chunk.Choices?.Any(x => x.Delta?.Content == "Partial answer") == true)
            {
                Assert.That(session.PreviousMessageId, Is.Null);
                Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send([User("Concurrent")], server.Token));
                break;
            }
        }
        Assert.That(session.LastMessage, Is.Null);
        Assert.That(session.Messages.ToString(), Does.Contain("Partial answer"));
        await session.Send(retryInterrupted ? null : [User("Continue that answer")], server.Token);
        Assert.That((string?)server.Requests[1].Body["thread"]?["type"], Is.EqualTo("create"));
        Assert.That(server.Requests[1].Body["messages"]!.ToString().Contains("Partial answer"), Is.EqualTo(!retryInterrupted));
        Assert.That((JArray)server.Requests[1].Body["messages"]!, Has.Count.EqualTo(retryInterrupted ? 1 : 3));
    }

    [Test]
    public async Task StreamEndedWithoutMessageStop_ThrowsAndDoesNotAdvance()
    {
        await using FakeServer server = new(new Reply(Start("msg_partial") + TextBlock("Partial") + Finish(stop: false), Stream: true));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        Assert.ThrowsAsync<IOException>(async () => { await foreach (var _ in session.Stream(cancellationToken: server.Token)) { } });
        Assert.That(session.PreviousMessageId, Is.Null);
        Assert.That(session.LastMessage, Is.Null);
        Assert.That(session.Messages.ToString(), Does.Contain("Partial"));
    }

    [Test]
    public async Task StreamingRefusalBeforeMessageStop_RollsBackTheRefusedInput()
    {
        await using FakeServer server = new(Message("msg_first"), new Reply(Start("msg_refused") + TextBlock("Refusal") + Finish("refusal", false), Stream: true));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        await session.Send(cancellationToken: server.Token);
        Assert.ThrowsAsync<IOException>(async () => { await foreach (var _ in session.Stream([User("Refused input")], server.Token)) { } });
        Assert.That(session.Messages, Has.Count.EqualTo(2));
        Assert.That(session.LastMessage!.Id, Is.EqualTo("msg_first"));
        Assert.That(session.PreviousMessageId, Is.Null);
    }

    [Test]
    public async Task StreamingFallback_RetainsResponseAndCreatesNextThread()
    {
        await using FakeServer server = new(new Reply(Start("msg_fallback") + TextBlock("Fallback") + Finish(fallback: true), Stream: true), Message("msg_next"));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        await foreach (var _ in session.Stream(cancellationToken: server.Token)) { }
        Assert.That(session.LastMessage!.Id, Is.EqualTo("msg_fallback"));
        Assert.That(session.PreviousMessageId, Is.Null);
        await session.Send([User("Next")], server.Token);
        Assert.That((string?)server.Requests[1].Body["thread"]?["type"], Is.EqualTo("create"));
        Assert.That(server.Requests[1].Body["messages"]!.ToString(), Does.Contain("Fallback"));
    }

    [Test]
    public async Task StreamCancellation_ReleasesTheSessionAndRetainsPartialHistory()
    {
        await using FakeServer server = new(new Reply(Start("msg_partial") + TextBlock("Partial", false), Stream: true), Message("msg_next"));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        using CancellationTokenSource cancelled = CancellationTokenSource.CreateLinkedTokenSource(server.Token);
        Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () =>
        {
            await foreach (var chunk in session.Stream(cancellationToken: cancelled.Token))
                if (chunk.Choices?.Any(x => x.Delta?.Content == "Partial") == true) cancelled.Cancel();
        });
        Assert.That(session.PreviousMessageId, Is.Null);
        Assert.That(server.Requests, Has.Count.EqualTo(1));
        await session.Send([User("Continue")], server.Token);
        Assert.That(server.Requests[1].Body["messages"]!.ToString(), Does.Contain("Partial"));
        Assert.That((string?)server.Requests[1].Body["thread"]?["type"], Is.EqualTo("create"));
    }

    [Test]
    public async Task StreamedThinkingAndToolInputs_AreReassembledForLosslessReplay()
    {
        string sse = Start("msg_tools")
            + Event("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}}""")
            + Event("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"Reasoning"}}""")
            + Event("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"signature_delta","signature":"opaque-signature"}}""")
            + Event("content_block_stop", """{"type":"content_block_stop","index":0}""")
            + Event("content_block_start", """{"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"toolu_one","name":"check","input":{}}}""")
            + Event("content_block_delta", """{"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"value\":"}}""")
            + Event("content_block_delta", """{"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"42}"}}""")
            + Event("content_block_stop", """{"type":"content_block_stop","index":1}""")
            + Finish("tool_use");
        await using FakeServer server = new(new Reply(sse, Stream: true), Error("thread_not_found", 404), Message("msg_new"));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        await foreach (var _ in session.Stream(cancellationToken: server.Token)) { }
        session.Append(JArray.Parse("""[{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_one","content":"Done"}]}]"""));
        await session.Send(cancellationToken: server.Token);
        var content = (JArray)server.Requests[2].Body["messages"]![1]!["content"]!;
        Assert.That((string?)content[0]["thinking"], Is.EqualTo("Reasoning"));
        Assert.That((string?)content[0]["signature"], Is.EqualTo("opaque-signature"));
        Assert.That((int?)content[1]["input"]?["value"], Is.EqualTo(42));
        Assert.That((string?)server.Requests[2].Body["messages"]![2]!["content"]![0]!["tool_use_id"], Is.EqualTo("toolu_one"));
    }

    [Test]
    public async Task IncompleteToolInput_IsNotAddedToReplayHistory()
    {
        string sse = Start("msg_partial") + TextBlock("Keep text")
            + Event("content_block_start", """{"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"toolu_partial","name":"check","input":{}}}""")
            + Event("content_block_delta", """{"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"value\":"}}""");
        await using FakeServer server = new(new Reply(sse, Stream: true));
        var session = server.Api.Chat.CreateAnthropicThread(Prototype());
        Assert.ThrowsAsync<IOException>(async () => { await foreach (var _ in session.Stream(cancellationToken: server.Token)) { } });
        Assert.That(session.Messages.ToString(), Does.Contain("Keep text").And.Not.Contain("toolu_partial"));
    }

    private record Reply(string Body, int Status = 200, bool Stream = false);
    private record Received(JObject Body, string? Betas, string? Profile);

    private sealed class FakeServer : IAsyncDisposable
    {
        private readonly HttpListener listener = new();
        private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        private readonly Task serving;
        internal TornadoApi Api { get; }
        internal CancellationToken Token => timeout.Token;
        internal List<Received> Requests { get; } = [];

        internal FakeServer(params Reply[] replies)
        {
            using var port = new TcpListener(IPAddress.Loopback, 0);
            port.Start();
            int number = ((IPEndPoint)port.LocalEndpoint).Port;
            port.Stop();
            string prefix = $"http://127.0.0.1:{number}/";
            listener.Prefixes.Add(prefix);
            listener.Start();
            Api = new TornadoApi(new AnthropicEndpointProvider
            {
                Auth = new(LLmProviders.Anthropic, "local-test-key"), ApiRevision = AnthropicApiVersions.October2026,
                UrlResolver = (_, _, _) => prefix + "v1/messages"
            });
            serving = Serve(replies);
        }

        private async Task Serve(Reply[] replies)
        {
            try
            {
                foreach (Reply reply in replies)
                {
                    HttpListenerContext context = await listener.GetContextAsync().WaitAsync(Token);
                    string body = await new StreamReader(context.Request.InputStream).ReadToEndAsync(Token);
                    Requests.Add(new Received(JObject.Parse(body), context.Request.Headers["anthropic-beta"], context.Request.Headers["anthropic-user-profile-id"]));
                    context.Response.StatusCode = reply.Status;
                    context.Response.ContentType = reply.Stream ? "text/event-stream" : "application/json";
                    await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(reply.Body), Token);
                    context.Response.Close();
                }
            }
            catch (Exception) when (timeout.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync()
        {
            timeout.Cancel();
            listener.Close();
            await serving;
            timeout.Dispose();
        }
    }
}
