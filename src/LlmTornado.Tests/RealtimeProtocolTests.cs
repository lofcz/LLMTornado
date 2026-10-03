using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using LlmTornado.Code;
using LlmTornado.Realtime;
using LlmTornado.Realtime.Translation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
[NonParallelizable]
public class RealtimeProtocolTests
{
    [Test]
    public async Task ClientSecrets_UseSeparateVoiceTranslationAndTranscriptionSchemas()
    {
        List<(string Path, JObject Body)> requests = [];
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        TcpListener portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        int port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        string prefix = $"http://127.0.0.1:{port}/";
        using HttpListener listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        Task serving = ServeRequests();
        try
        {
            TornadoApi api = new TornadoApi(new Uri(prefix), "test-key", LLmProviders.OpenAi);
            var voice = await api.Realtime.CreateClientSecretForRealtime2(RealtimeVoiceSessionConfig.ForRealtime2());
            var translation = await api.Realtime.CreateClientSecretForTranslation(new RealtimeVoiceSessionConfig
            {
                Instructions = "This field is not part of a translation session.",
                Reasoning = new RealtimeReasoningConfig { Effort = RealtimeReasoningEffort.Low }
            }, "es");
            var transcription = await api.Realtime.CreateClientSecretForTranscription();
            await serving.WaitAsync(timeout.Token);

            Assert.That(new[] { voice.Ok, translation.Ok, transcription.Ok }, Is.All.True);
            Assert.That(requests.Select(x => x.Path), Is.EqualTo(new[]
            {
                "/v1/realtime/client_secrets", "/v1/realtime/translations/client_secrets", "/v1/realtime/client_secrets"
            }));
            Assert.That(requests[0].Body.SelectToken("session.type")?.ToString(), Is.EqualTo("realtime"));
            Assert.That(requests[0].Body.SelectToken("session.model")?.ToString(), Is.EqualTo("gpt-realtime-2"));
            Assert.That(requests[1].Body.SelectToken("session.model")?.ToString(), Is.EqualTo("gpt-realtime-translate"));
            Assert.That(requests[1].Body.SelectToken("session.audio.output.language")?.ToString(), Is.EqualTo("es"));
            Assert.That(requests[1].Body["session"]!.Children<JProperty>().Select(x => x.Name), Is.EquivalentTo(new[] { "model", "audio" }));
            Assert.That(requests[2].Body.SelectToken("session.type")?.ToString(), Is.EqualTo("transcription"));
            Assert.That(requests[2].Body.SelectToken("session.audio.input.transcription.model")?.ToString(), Is.EqualTo("gpt-live-transcribe"));
            Assert.That(requests[2].Body.SelectToken("session.audio.input.transcription.language"), Is.Null);
            Assert.That(requests[2].Body.SelectToken("session.audio.input.turn_detection")?.Type, Is.EqualTo(JTokenType.Null));
        }
        finally
        {
            listener.Close();
            try
            {
                await serving;
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
            {
                // The listener was closed before all requests arrived.
            }
        }

        async Task ServeRequests()
        {
            for (int i = 0; i < 3; i++)
            {
                HttpListenerContext context = await listener.GetContextAsync().WaitAsync(timeout.Token);
                using StreamReader reader = new StreamReader(context.Request.InputStream);
                requests.Add((context.Request.Url!.AbsolutePath, JObject.Parse(await reader.ReadToEndAsync(timeout.Token))));
                byte[] response = Encoding.UTF8.GetBytes("""{"value":"ek_test","expires_at":2000000000}""");
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = response.Length;
                await context.Response.OutputStream.WriteAsync(response, timeout.Token);
                context.Response.Close();
            }
        }
    }

    [Test]
    public async Task WebSocket_PreservesUtf8EventOrderAndDrainsTranslationBeforeClose()
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ScriptedWebSocket socket = new ScriptedWebSocket();
        socket.Enqueue("""{"type":"session.created"}""");
        string deltaJson = """{"type":"response.output_text.delta","delta":"Grüße"}""";
        byte[] deltaBytes = Encoding.UTF8.GetBytes(deltaJson);
        int split = Array.IndexOf(deltaBytes, (byte)0xC3) + 1;
        socket.Enqueue(deltaBytes[..split], false);
        socket.Enqueue(deltaBytes[split..], true);
        socket.Enqueue("""{"type":"response.done"}""");
        List<RealtimeServerEvent> received = [];
        TaskCompletionSource releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource receivedAll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int synchronousCalls = 0;
        await using (RealtimeSession session = new RealtimeSession(socket, new RealtimeConnectOptions
        {
            OnEvent = _ => synchronousCalls++,
            OnEventAsync = async evt =>
            {
                received.Add(evt);
                if (received.Count == 1)
                {
                    await releaseFirst.Task.WaitAsync(timeout.Token);
                }
                if (received.Count == 3)
                {
                    receivedAll.TrySetResult();
                }
            }
        }, CancellationTokenSource.CreateLinkedTokenSource(timeout.Token)))
        {
            Assert.That(received, Has.Count.EqualTo(1));
            releaseFirst.SetResult();
            await receivedAll.Task.WaitAsync(timeout.Token);
            Assert.That(received.Select(x => x.Type), Is.EqualTo(new[] { "session.created", "response.output_text.delta", "response.done" }));
            Assert.That(received[1].Delta, Is.EqualTo("Grüße"));
            Assert.That(synchronousCalls, Is.EqualTo(3));
            await session.CloseSessionAsync(timeout.Token);
            Assert.That(socket.CloseOutputCalls, Is.EqualTo(1));
            Assert.That(socket.Sent, Is.Empty);
        }

        ScriptedWebSocket translationSocket = new ScriptedWebSocket();
        translationSocket.Enqueue("""{"type":"session.created"}""");
        TaskCompletionSource<RealtimeTranslationSession> ready = new TaskCompletionSource<RealtimeTranslationSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource created = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource audioStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseAudio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource closeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseClose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RealtimeSession inner = new RealtimeSession(translationSocket, new RealtimeConnectOptions
        {
            Kind = RealtimeSessionKind.Translation,
            OnEventAsync = async evt =>
            {
                var translation = await ready.Task.WaitAsync(timeout.Token);
                await translation.DispatchEventAsync(LlmTornado.Realtime.Vendors.OpenAi.VendorOpenAiRealtimeTranslation.ParseEvent(evt.RawJson!));
            }
        }, CancellationTokenSource.CreateLinkedTokenSource(timeout.Token));
        await using RealtimeTranslationSession translated = new RealtimeTranslationSession(inner, new RealtimeTranslationEventHandler
        {
            SessionHandler = _ => { created.TrySetResult(); return ValueTask.CompletedTask; },
            OutputAudioHandler = async _ =>
            {
                audioStarted.SetResult();
                await releaseAudio.Task.WaitAsync(timeout.Token);
            },
            SessionClosedHandler = async _ =>
            {
                closeStarted.SetResult();
                await releaseClose.Task.WaitAsync(timeout.Token);
            }
        }, CancellationTokenSource.CreateLinkedTokenSource(timeout.Token));
        ready.SetResult(translated);
        await created.Task.WaitAsync(timeout.Token);
        Task closing = translated.CloseAsync(timeout.Token);
        Assert.That(translationSocket.Sent.Single()["type"]?.ToString(), Is.EqualTo("session.close"));
        translationSocket.Enqueue("""{"type":"session.output_audio.delta","delta":"AAA="}""");
        translationSocket.Enqueue("""{"type":"session.closed"}""");
        await audioStarted.Task.WaitAsync(timeout.Token);
        Assert.That(closing.IsCompleted, Is.False);
        releaseAudio.SetResult();
        await closeStarted.Task.WaitAsync(timeout.Token);
        Assert.That(closing.IsCompleted, Is.False);
        releaseClose.SetResult();
        await closing.WaitAsync(timeout.Token);
        Assert.That(translated.IsSessionClosed, Is.True);
    }

    [Test]
    public async Task Transcription_CollectsResultsWithCustomCallbacksAndCompletesOnErrors()
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        RealtimeTranscriptionResult result = new RealtimeTranscriptionResult();
        int deltas = 0;
        TaskCompletionSource completionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<RealtimeServerEvent, ValueTask> deltaHandler = _ => { deltas++; return ValueTask.CompletedTask; };
        RealtimeTranscriptionStreamEventHandler handler = new RealtimeTranscriptionStreamEventHandler
        {
            OnTranscriptionDelta = deltaHandler,
            OnTranscriptionCompleted = async _ =>
            {
                completionStarted.SetResult();
                await releaseCompletion.Task.WaitAsync(timeout.Token);
            }
        };
        await result.DispatchAsync(new RealtimeServerEvent { Type = RealtimeEventTypes.TranscriptionDelta, Delta = "Hello" }, handler);
        Task completing = result.DispatchAsync(new RealtimeServerEvent { Type = RealtimeEventTypes.TranscriptionCompleted, Transcript = "Hello." }, handler);
        await completionStarted.Task.WaitAsync(timeout.Token);
        Assert.That(result.Completed.Task.IsCompleted, Is.False);
        releaseCompletion.SetResult();
        await completing.WaitAsync(timeout.Token);
        Assert.That(await result.Completed.Task, Is.True);
        Assert.That(result.Deltas, Is.EqualTo(new[] { "Hello" }));
        Assert.That(result.FinalTranscript, Is.EqualTo("Hello."));
        Assert.That(deltas, Is.EqualTo(1));
        Assert.That(handler.OnTranscriptionDelta, Is.SameAs(deltaHandler));

        foreach (string type in new[] { RealtimeEventTypes.TranscriptionFailed, RealtimeEventTypes.Error })
        {
            RealtimeTranscriptionResult failed = new RealtimeTranscriptionResult();
            await failed.DispatchAsync(new RealtimeServerEvent
            {
                Type = type,
                Error = new RealtimeErrorPayload { Message = "Invalid audio" }
            }, new RealtimeTranscriptionStreamEventHandler());
            Assert.That(failed.Errors, Is.EqualTo(new[] { "Invalid audio" }));
            Assert.That(await failed.Completed.Task.WaitAsync(timeout.Token), Is.False);
        }
    }

    private sealed class ScriptedWebSocket : WebSocket
    {
        private readonly Channel<(byte[] Bytes, bool EndOfMessage)> frames = Channel.CreateUnbounded<(byte[], bool)>();
        private WebSocketState state = WebSocketState.Open;
        public ConcurrentQueue<JObject> Sent { get; } = new ConcurrentQueue<JObject>();
        public int CloseOutputCalls { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => state;
        public override string? SubProtocol => null;
        public void Enqueue(string json) => Enqueue(Encoding.UTF8.GetBytes(json), true);
        public void Enqueue(byte[] bytes, bool endOfMessage) => frames.Writer.TryWrite((bytes, endOfMessage));
        public override void Abort() => state = WebSocketState.Aborted;
        public override void Dispose() => state = WebSocketState.Closed;
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("CloseAsync would compete with the active receive loop.");
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            CloseOutputCalls++;
            state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }
        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            var frame = await frames.Reader.ReadAsync(cancellationToken);
            frame.Bytes.CopyTo(buffer.Array!, buffer.Offset);
            return new WebSocketReceiveResult(frame.Bytes.Length, WebSocketMessageType.Text, frame.EndOfMessage);
        }
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            Sent.Enqueue(JObject.Parse(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count)));
            return Task.CompletedTask;
        }
    }
}
