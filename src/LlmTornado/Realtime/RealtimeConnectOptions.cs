using System;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Chat.Models;

namespace LlmTornado.Realtime;

/// <summary>
/// Options for opening a GA OpenAI Realtime WebSocket session.
/// </summary>
public class RealtimeConnectOptions
{
    /// <summary>
    /// Session kind determines WebSocket path and behavior.
    /// </summary>
    public RealtimeSessionKind Kind { get; set; } = RealtimeSessionKind.Voice;

    /// <summary>
    /// Model for the connection query string. Defaults by <see cref="Kind"/>.
    /// </summary>
    public ChatModel? Model { get; set; }

    /// <summary>
    /// API key or ephemeral client secret (<c>ek_...</c>). When null, uses authenticated OpenAI provider from <see cref="TornadoApi"/>.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional safety identifier header (<c>OpenAI-Safety-Identifier</c>).
    /// </summary>
    public string? SafetyIdentifier { get; set; }

    /// <summary>
    /// API version segment (default <c>v1</c>).
    /// </summary>
    public string ApiVersion { get; set; } = "v1";

    /// <summary>
    /// Base host (default <c>api.openai.com</c>).
    /// </summary>
    public string Host { get; set; } = "api.openai.com";

    /// <summary>
    /// Invoked for each parsed server event.
    /// </summary>
    public Action<RealtimeServerEvent>? OnEvent { get; set; }

    /// <summary>
    /// Asynchronous event handler, awaited before the next server event is processed.
    /// </summary>
    public Func<RealtimeServerEvent, Task>? OnEventAsync { get; set; }

    /// <summary>
    /// Invoked when the WebSocket opens.
    /// </summary>
    public Action? OnOpen { get; set; }

    /// <summary>
    /// Invoked on WebSocket errors.
    /// </summary>
    public Action<Exception>? OnError { get; set; }

    /// <summary>
    /// Invoked when the WebSocket closes.
    /// </summary>
    public Action<string?>? OnClose { get; set; }

    /// <summary>
    /// Cancellation token for connect and receive loop.
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    /// <summary>
    /// Resolves the model name for the WebSocket URL.
    /// </summary>
    public string ResolveModelName()
    {
        if (Model is not null)
        {
            return Model.Name;
        }

        return Kind switch
        {
            RealtimeSessionKind.Translation => ChatModelOpenAiRealtime.ModelRealtimeTranslate.Name,
            RealtimeSessionKind.Transcription => ChatModelOpenAiRealtime.ModelLiveTranscribe.Name,
            _ => ChatModelOpenAiRealtime.ModelRealtime21.Name
        };
    }

    /// <summary>
    /// Builds the GA WebSocket URL (no <c>OpenAI-Beta: realtime=v1</c> header required).
    /// </summary>
    public Uri BuildWebSocketUri()
    {
        if (Kind is RealtimeSessionKind.Transcription)
        {
            return new Uri($"wss://{Host}/{ApiVersion}/realtime?intent=transcription");
        }

        string model = Uri.EscapeDataString(ResolveModelName());
        string path = Kind switch
        {
            RealtimeSessionKind.Translation => $"{ApiVersion}/realtime/translations",
            _ => $"{ApiVersion}/realtime"
        };

        return new Uri($"wss://{Host}/{path}?model={model}");
    }
}
