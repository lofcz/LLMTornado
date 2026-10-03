using System;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Realtime.Translation;
using Newtonsoft.Json;

namespace LlmTornado.Realtime;

/// <summary>
/// OpenAI Realtime API REST helpers (GA). Use with <see cref="RealtimeSession"/> for WebSocket sessions.
/// </summary>
public class RealtimeEndpoint : EndpointBase
{
    private readonly Lazy<RealtimeTranslationEndpoint> translation;

    internal RealtimeEndpoint(TornadoApi api) : base(api)
    {
        translation = new Lazy<RealtimeTranslationEndpoint>(() => new RealtimeTranslationEndpoint(api));
    }

    /// <summary>
    /// Streaming speech translation via <c>/v1/realtime/translations</c> and <c>gpt-realtime-translate</c>.
    /// </summary>
    public RealtimeTranslationEndpoint Translation => translation.Value;
    /// <inheritdoc />
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Realtime;

    /// <summary>
    /// Creates a client secret and optional embedded session config (GA — preferred over legacy <c>/realtime/sessions</c>).
    /// </summary>
    public Task<HttpCallResult<RealtimeClientSecretResponse>> CreateClientSecret(
        RealtimeClientSecretRequest request,
        CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return HttpPost<RealtimeClientSecretResponse>(provider, Endpoint, provider.ApiUrl(Endpoint, "/client_secrets"), postData: Serialize(request), ct: cancellationToken);
    }

    /// <summary>
    /// Creates a client secret for a <c>gpt-realtime-2</c> voice session with configurable reasoning.
    /// </summary>
    public Task<HttpCallResult<RealtimeClientSecretResponse>> CreateClientSecretForRealtime2(
        RealtimeVoiceSessionConfig session,
        int secretTtlSeconds = 600,
        CancellationToken cancellationToken = default)
    {
        return CreateClientSecret(new RealtimeClientSecretRequest
        {
            ExpiresAfter = new RealtimeClientSecretExpiresAfter { Seconds = secretTtlSeconds },
            Session = session
        }, cancellationToken);
    }

    /// <summary>
    /// Creates a client secret for a translation session (<c>gpt-realtime-translate</c>).
    /// Set <see cref="RealtimeAudioOutputConfig.Language"/> on the session audio output.
    /// </summary>
    public Task<HttpCallResult<RealtimeClientSecretResponse>> CreateClientSecretForTranslation(
        RealtimeVoiceSessionConfig session,
        string targetLanguage,
        int secretTtlSeconds = 600,
        CancellationToken cancellationToken = default)
    {
        session.Model ??= ChatModelOpenAiRealtime.ModelRealtimeTranslate;
        session.Audio ??= new RealtimeAudioConfig();
        session.Audio.Output ??= new RealtimeAudioOutputConfig();
        session.Audio.Output.Language = targetLanguage;

        IEndpointProvider provider = Api.ResolveProvider(LLmProviders.OpenAi);
        return HttpPost<RealtimeClientSecretResponse>(provider, Endpoint, provider.ApiUrl(Endpoint, "/translations/client_secrets"), postData: Serialize(new
        {
            expires_after = new RealtimeClientSecretExpiresAfter { Seconds = secretTtlSeconds },
            session = new { model = session.Model.ApiName ?? session.Model.Name, audio = session.Audio }
        }), ct: cancellationToken);
    }

    /// <summary>
    /// Creates a client secret for a transcription session (<c>gpt-live-transcribe</c> by default).
    /// </summary>
    public Task<HttpCallResult<RealtimeClientSecretResponse>> CreateClientSecretForTranscription(
        RealtimeTranscriptionSessionConfig? session = null,
        int secretTtlSeconds = 600,
        CancellationToken cancellationToken = default)
    {
        return CreateClientSecret(new RealtimeClientSecretRequest
        {
            ExpiresAfter = new RealtimeClientSecretExpiresAfter { Seconds = secretTtlSeconds },
            Session = session ?? RealtimeTranscriptionSessionConfig.ForLiveTranscribe()
        }, cancellationToken);
    }

    /// <summary>
    /// Opens a GA Realtime WebSocket session. Requires <c>MODERN</c> target framework (net8+).
    /// </summary>
    public Task<RealtimeSession> ConnectAsync(RealtimeConnectOptions options)
    {
        return RealtimeSession.ConnectAsync(Api, options);
    }

#if MODERN
    /// <summary>
    /// Opens a streaming transcription session, sends PCM audio, and collects transcript events.
    /// </summary>
    public async Task<RealtimeTranscriptionResult> TranscribeStreamingAsync(
        byte[] pcm16Audio,
        RealtimeTranscriptionSessionConfig? sessionConfig = null,
        RealtimeTranscriptionStreamEventHandler? handler = null,
        CancellationToken cancellationToken = default)
    {
        RealtimeTranscriptionResult result = new RealtimeTranscriptionResult();
        RealtimeTranscriptionStreamEventHandler effectiveHandler = handler ?? new RealtimeTranscriptionStreamEventHandler();

        RealtimeConnectOptions connectOptions = new RealtimeConnectOptions
        {
            Kind = RealtimeSessionKind.Transcription,
            CancellationToken = cancellationToken,
            OnEventAsync = evt => result.DispatchAsync(evt, effectiveHandler),
            OnError = error =>
            {
                result.Errors.Add(error.Message);
                result.Completed.TrySetResult(false);
            },
            OnClose = description =>
            {
                if (!result.Completed.Task.IsCompleted)
                {
                    result.Errors.Add(description ?? "Realtime connection closed before transcription completed.");
                    result.Completed.TrySetResult(false);
                }
            }
        };

        await using RealtimeSession session = await ConnectAsync(connectOptions).ConfigureAwait(false);
        await session.UpdateSessionAsync(sessionConfig ?? RealtimeTranscriptionSessionConfig.ForLiveTranscribe(), cancellationToken).ConfigureAwait(false);
        const int chunkSize = 24_000 * 2 / 5;
        for (int offset = 0; offset < pcm16Audio.Length; offset += chunkSize)
        {
            int length = Math.Min(chunkSize, pcm16Audio.Length - offset);
            await session.AppendInputAudioAsync(Convert.ToBase64String(pcm16Audio, offset, length), cancellationToken).ConfigureAwait(false);
        }
        await session.CommitInputAudioAsync(cancellationToken).ConfigureAwait(false);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));
        try
        {
            await result.Completed.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result.Errors.Add("Realtime transcription timed out before completion.");
        }

        result.PartialTranscript = string.Concat(result.Deltas);
        return result;
    }
#endif

    private static string Serialize(object? data)
    {
        return JsonConvert.SerializeObject(data, EndpointBase.NullSettings);
    }
}
