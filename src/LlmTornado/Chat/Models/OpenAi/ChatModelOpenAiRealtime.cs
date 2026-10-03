using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// OpenAI Realtime and Live API models (GA May 2026; Realtime 2.1 July 2026; GPT-Live 1 September 2026).
/// </summary>
public class ChatModelOpenAiRealtime : IVendorModelClassProvider
{
    /// <summary>
    /// GPT Realtime 2.1 — updated realtime reasoning model with improved alphanumeric
    /// recognition, silence and noise handling, and interruption behavior.
    /// Released July 6, 2026. 128k context, 32k max output.
    /// </summary>
    public static readonly ChatModel ModelRealtime21 = new ChatModel("gpt-realtime-2.1", LLmProviders.OpenAi, 128_000)
    {
        EndpointCapabilities = [ChatModelEndpointCapabilities.Realtime]
    };

    /// <inheritdoc cref="ModelRealtime21"/>
    public readonly ChatModel Realtime21 = ModelRealtime21;

    /// <summary>
    /// GPT Realtime 2.1 Mini — faster, lower-cost distilled reasoning model for realtime voice.
    /// Released July 6, 2026. 128k context, 32k max output.
    /// </summary>
    public static readonly ChatModel ModelRealtime21Mini = new ChatModel("gpt-realtime-2.1-mini", LLmProviders.OpenAi, 128_000)
    {
        EndpointCapabilities = [ChatModelEndpointCapabilities.Realtime]
    };

    /// <inheritdoc cref="ModelRealtime21Mini"/>
    public readonly ChatModel Realtime21Mini = ModelRealtime21Mini;

    /// <summary>
    /// GPT-Live 1 — full-duplex voice conversations over <c>/v1/live/sessions</c>.
    /// Generally available September 10, 2026. Can delegate reasoning and tools to a backend model.
    /// </summary>
    public static readonly ChatModel ModelLive1 = new ChatModel("gpt-live-1", LLmProviders.OpenAi, 128_000)
    {
        EndpointCapabilities = [ChatModelEndpointCapabilities.Live]
    };

    /// <inheritdoc cref="ModelLive1"/>
    public readonly ChatModel Live1 = ModelLive1;

    /// <summary>
    /// GPT Live Transcribe — low-latency streaming speech-to-text for realtime transcription.
    /// Released July 30, 2026.
    /// </summary>
    public static readonly ChatModel ModelLiveTranscribe = new ChatModel("gpt-live-transcribe", LLmProviders.OpenAi, 128_000)
    {
        EndpointCapabilities = [ChatModelEndpointCapabilities.Realtime]
    };

    /// <inheritdoc cref="ModelLiveTranscribe"/>
    public readonly ChatModel LiveTranscribe = ModelLiveTranscribe;

    /// <summary>
    /// GPT Realtime 2 — speech-to-speech with configurable reasoning effort.
    /// 128k context, 32k max output. Use GPT Realtime 2.1 for new voice agents.
    /// </summary>
    public static readonly ChatModel ModelRealtime2 = new ChatModel("gpt-realtime-2", LLmProviders.OpenAi, 128_000)
    {
        EndpointCapabilities = [ChatModelEndpointCapabilities.Realtime]
    };

    /// <inheritdoc cref="ModelRealtime2"/>
    public readonly ChatModel Realtime2 = ModelRealtime2;

    /// <summary>
    /// GPT Realtime — general realtime voice model.
    /// </summary>
    public static readonly ChatModel ModelRealtime = new ChatModel("gpt-realtime", LLmProviders.OpenAi, 32_000, ["gpt-realtime-2025-08-28"])
    {
        EndpointCapabilities = [ChatModelEndpointCapabilities.Realtime]
    };

    /// <inheritdoc cref="ModelRealtime"/>
    public readonly ChatModel Realtime = ModelRealtime;

    /// <summary>
    /// GPT Realtime 1.5 — prior-generation realtime voice model.
    /// </summary>
    public static readonly ChatModel ModelRealtime15 = new ChatModel("gpt-realtime-1.5", LLmProviders.OpenAi, 32_000)
    {
        EndpointCapabilities = [ChatModelEndpointCapabilities.Realtime]
    };

    /// <inheritdoc cref="ModelRealtime15"/>
    public readonly ChatModel Realtime15 = ModelRealtime15;

    /// <summary>
    /// GPT Realtime Mini — low-latency realtime voice.
    /// </summary>
    public static readonly ChatModel ModelRealtimeMini = new ChatModel("gpt-realtime-mini", LLmProviders.OpenAi, 32_000, ["gpt-realtime-mini-2025-12-15", "gpt-realtime-mini-2025-10-06"])
    {
        EndpointCapabilities = [ChatModelEndpointCapabilities.Realtime]
    };

    /// <inheritdoc cref="ModelRealtimeMini"/>
    public readonly ChatModel RealtimeMini = ModelRealtimeMini;

    /// <summary>
    /// GPT Realtime Translate — continuous speech-to-speech translation (dedicated <c>/v1/realtime/translations</c> endpoint).
    /// </summary>
    public static readonly ChatModel ModelRealtimeTranslate = new ChatModel("gpt-realtime-translate", LLmProviders.OpenAi, 16_000)
    {
        EndpointCapabilities = [ChatModelEndpointCapabilities.Realtime]
    };

    /// <inheritdoc cref="ModelRealtimeTranslate"/>
    public readonly ChatModel RealtimeTranslate = ModelRealtimeTranslate;

    /// <summary>
    /// GPT Realtime Whisper — streaming speech-to-text for realtime transcription sessions.
    /// </summary>
    public static readonly ChatModel ModelRealtimeWhisper = new ChatModel("gpt-realtime-whisper", LLmProviders.OpenAi, 16_000)
    {
        EndpointCapabilities = [ChatModelEndpointCapabilities.Realtime]
    };

    /// <inheritdoc cref="ModelRealtimeWhisper"/>
    public readonly ChatModel RealtimeWhisper = ModelRealtimeWhisper;

    /// <summary>
    /// All known OpenAI Realtime API models.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() =>
    [
        ModelRealtime21, ModelRealtime21Mini, ModelLive1, ModelLiveTranscribe,
        ModelRealtime2, ModelRealtime, ModelRealtime15, ModelRealtimeMini,
        ModelRealtimeTranslate, ModelRealtimeWhisper
    ]);

    /// <inheritdoc cref="ModelsAll"/>
    public List<IModel> AllModels => ModelsAll;

    internal ChatModelOpenAiRealtime()
    {
    }
}
