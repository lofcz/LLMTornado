#if MODERN
using System;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Realtime.Vendors.OpenAi;

namespace LlmTornado.Realtime.Translation;

/// <summary>
/// Active WebSocket session to the OpenAI Realtime translation endpoint (<c>/v1/realtime/translations</c>).
/// </summary>
public sealed class RealtimeTranslationSession : IAsyncDisposable
{
    private readonly RealtimeSession inner;
    private readonly RealtimeTranslationEventHandler? eventHandler;
    private readonly CancellationTokenSource connectionCancellation;
    private readonly TaskCompletionSource sessionClosedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool sessionClosedReceived;

    internal RealtimeTranslationSession(RealtimeSession inner, RealtimeTranslationEventHandler? eventHandler, CancellationTokenSource connectionCancellation)
    {
        this.inner = inner;
        this.eventHandler = eventHandler;
        this.connectionCancellation = connectionCancellation;
    }

    /// <summary>
    /// Opens a WebSocket session to <c>wss://api.openai.com/v1/realtime/translations</c>.
    /// </summary>
    internal static async Task<RealtimeTranslationSession> ConnectAsync(
        TornadoApi api,
        RealtimeTranslationConnectOptions options,
        CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<RealtimeTranslationSession> ready = new TaskCompletionSource<RealtimeTranslationSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken, cancellationToken);

        RealtimeConnectOptions connectOptions = new RealtimeConnectOptions
        {
            Kind = RealtimeSessionKind.Translation,
            Model = options.Model,
            ApiKey = options.ApiKey,
            SafetyIdentifier = options.SafetyIdentifier,
            CancellationToken = linked.Token,
            OnEventAsync = async evt =>
            {
                if (string.IsNullOrWhiteSpace(evt.RawJson))
                {
                    return;
                }

                RealtimeTranslationEvent parsed = VendorOpenAiRealtimeTranslation.ParseEvent(evt.RawJson);
                RealtimeTranslationSession session = await ready.Task.ConfigureAwait(false);
                await session.DispatchEventAsync(parsed).ConfigureAwait(false);
            }
        };

        RealtimeSession inner;
        try
        {
            inner = await RealtimeSession.ConnectAsync(api, connectOptions).ConfigureAwait(false);
        }
        catch
        {
            linked.Dispose();
            throw;
        }

        RealtimeTranslationSession session = new RealtimeTranslationSession(inner, options.EventHandler, linked);
        ready.SetResult(session);

        try
        {
            await session.UpdateSessionAsync(options.Config, linked.Token).ConfigureAwait(false);
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Whether the underlying WebSocket is open.
    /// </summary>
    public bool IsOpen => inner.IsOpen;

    /// <summary>
    /// Whether a <c>session.closed</c> event has been received.
    /// </summary>
    public bool IsSessionClosed => sessionClosedReceived;

    /// <summary>
    /// Sends a <c>session.update</c> event.
    /// </summary>
    public Task UpdateSessionAsync(RealtimeTranslationSessionConfig config, CancellationToken cancellationToken = default)
    {
        return inner.SendRawAsync(VendorOpenAiRealtimeTranslation.SerializeSessionUpdate(config), cancellationToken);
    }

    /// <summary>
    /// Appends 24 kHz PCM16 mono audio to the translation input buffer.
    /// </summary>
    public Task AppendAudioAsync(ReadOnlyMemory<byte> pcm16, CancellationToken cancellationToken = default)
    {
        if (pcm16.IsEmpty)
        {
            return Task.CompletedTask;
        }

        return inner.SendRawAsync(
            VendorOpenAiRealtimeTranslation.SerializeAppendAudio(Convert.ToBase64String(pcm16.Span)),
            cancellationToken);
    }

    /// <summary>
    /// Appends base64-encoded 24 kHz PCM16 mono audio to the translation input buffer.
    /// </summary>
    public Task AppendAudioBase64Async(string base64Pcm16, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(base64Pcm16))
        {
            return Task.CompletedTask;
        }

        return inner.SendRawAsync(VendorOpenAiRealtimeTranslation.SerializeAppendAudio(base64Pcm16), cancellationToken);
    }

    /// <summary>
    /// Streams PCM16 audio in recommended 200 ms frames.
    /// </summary>
    public async Task AppendAudioStreamAsync(ReadOnlyMemory<byte> pcm16, CancellationToken cancellationToken = default)
    {
        int offset = 0;
        int frameSize = VendorOpenAiRealtimeTranslation.FrameByteSize;

        while (offset < pcm16.Length)
        {
            int chunkSize = Math.Min(frameSize, pcm16.Length - offset);
            await AppendAudioAsync(pcm16.Slice(offset, chunkSize), cancellationToken).ConfigureAwait(false);
            offset += chunkSize;
        }
    }

    /// <summary>
    /// Sends <c>session.close</c> and waits for <c>session.closed</c>.
    /// </summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (sessionClosedReceived || !inner.IsOpen)
        {
            return;
        }

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        await inner.SendRawAsync(VendorOpenAiRealtimeTranslation.SerializeClose(), timeoutCts.Token).ConfigureAwait(false);

        await sessionClosedTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
    }

    internal async Task DispatchEventAsync(RealtimeTranslationEvent evt)
    {
        if (evt.EventType is RealtimeTranslationEventTypes.SessionClosed)
        {
            sessionClosedReceived = true;
        }

        if (eventHandler?.EventHandler is not null)
        {
            await eventHandler.EventHandler(evt).ConfigureAwait(false);
        }

        switch (evt.EventType)
        {
            case RealtimeTranslationEventTypes.OutputAudioDelta when eventHandler?.OutputAudioHandler is not null:
                await eventHandler.OutputAudioHandler(evt).ConfigureAwait(false);
                break;
            case RealtimeTranslationEventTypes.OutputTranscriptDelta when eventHandler?.OutputTranscriptHandler is not null:
                await eventHandler.OutputTranscriptHandler(evt).ConfigureAwait(false);
                break;
            case RealtimeTranslationEventTypes.InputTranscriptDelta when eventHandler?.InputTranscriptHandler is not null:
                await eventHandler.InputTranscriptHandler(evt).ConfigureAwait(false);
                break;
            case RealtimeTranslationEventTypes.SessionCreated or RealtimeTranslationEventTypes.SessionUpdated when eventHandler?.SessionHandler is not null:
                await eventHandler.SessionHandler(evt).ConfigureAwait(false);
                break;
            case RealtimeTranslationEventTypes.SessionClosed when eventHandler?.SessionClosedHandler is not null:
                await eventHandler.SessionClosedHandler(evt).ConfigureAwait(false);
                break;
            case RealtimeTranslationEventTypes.Error when eventHandler?.ErrorHandler is not null:
                await eventHandler.ErrorHandler(evt).ConfigureAwait(false);
                break;
        }

        if (evt.EventType is RealtimeTranslationEventTypes.SessionClosed)
        {
            sessionClosedTcs.TrySetResult();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await inner.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            connectionCancellation.Dispose();
        }
    }
}
#else
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LlmTornado.Realtime.Translation;

/// <summary>
/// WebSocket Realtime translation requires net8.0 or later.
/// </summary>
public sealed class RealtimeTranslationSession : IAsyncDisposable
{
    public bool IsOpen => false;
    public bool IsSessionClosed => false;

    public Task UpdateSessionAsync(RealtimeTranslationSessionConfig config, CancellationToken cancellationToken = default) =>
        throw Create();

    public Task AppendAudioAsync(ReadOnlyMemory<byte> pcm16, CancellationToken cancellationToken = default) =>
        throw Create();

    public Task AppendAudioBase64Async(string base64Pcm16, CancellationToken cancellationToken = default) =>
        throw Create();

    public Task AppendAudioStreamAsync(ReadOnlyMemory<byte> pcm16, CancellationToken cancellationToken = default) =>
        throw Create();

    public Task CloseAsync(CancellationToken cancellationToken = default) =>
        throw Create();

    internal Task DispatchEventAsync(RealtimeTranslationEvent evt) =>
        throw Create();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static NotSupportedException Create() =>
        new NotSupportedException("Realtime translation WebSocket sessions require net8.0 or later.");
}
#endif
