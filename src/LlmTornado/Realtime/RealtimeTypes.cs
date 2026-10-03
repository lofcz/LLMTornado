using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Realtime;

/// <summary>
/// Kind of Realtime WebSocket session.
/// </summary>
public enum RealtimeSessionKind
{
    /// <summary>Voice agent on <c>/v1/realtime</c> (e.g. <c>gpt-realtime-2</c>).</summary>
    Voice,

    /// <summary>Continuous translation on <c>/v1/realtime/translations</c> (<c>gpt-realtime-translate</c>).</summary>
    Translation,

    /// <summary>Streaming transcription (<c>gpt-realtime-whisper</c>, session type <c>transcription</c>).</summary>
    Transcription
}

/// <summary>
/// GA Realtime reasoning effort for <c>gpt-realtime-2</c>.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum RealtimeReasoningEffort
{
    [EnumMember(Value = "minimal")]
    Minimal,

    [EnumMember(Value = "low")]
    Low,

    [EnumMember(Value = "medium")]
    Medium,

    [EnumMember(Value = "high")]
    High,

    [EnumMember(Value = "xhigh")]
    XHigh
}

/// <summary>
/// Reasoning configuration for realtime voice sessions.
/// </summary>
public class RealtimeReasoningConfig
{
    [JsonProperty("effort")]
    public RealtimeReasoningEffort? Effort { get; set; }
}

/// <summary>
/// Client secret TTL configuration for <c>POST /v1/realtime/client_secrets</c>.
/// </summary>
public class RealtimeClientSecretExpiresAfter
{
    [JsonProperty("anchor")]
    public string? Anchor { get; set; } = "created_at";

    [JsonProperty("seconds")]
    public int? Seconds { get; set; }
}

/// <summary>
/// PCM / G.711 audio format for GA Realtime sessions.
/// </summary>
public class RealtimeAudioFormat
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("rate")]
    public int? Rate { get; set; }
}

/// <summary>
/// Input audio noise reduction.
/// </summary>
public class RealtimeNoiseReduction
{
    [JsonProperty("type")]
    public string? Type { get; set; }
}

/// <summary>
/// Server or semantic VAD turn detection.
/// </summary>
public class RealtimeTurnDetection
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("threshold")]
    public double? Threshold { get; set; }

    [JsonProperty("prefix_padding_ms")]
    public int? PrefixPaddingMs { get; set; }

    [JsonProperty("silence_duration_ms")]
    public int? SilenceDurationMs { get; set; }

    [JsonProperty("create_response")]
    public bool? CreateResponse { get; set; }

    [JsonProperty("interrupt_response")]
    public bool? InterruptResponse { get; set; }

    [JsonProperty("idle_timeout_ms")]
    public int? IdleTimeoutMs { get; set; }

    [JsonProperty("eagerness")]
    public string? Eagerness { get; set; }
}

/// <summary>
/// Input audio transcription settings.
/// </summary>
public class RealtimeInputTranscription
{
    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("language")]
    public string? Language { get; set; }

    /// <summary>Expected input languages for <c>gpt-live-transcribe</c>. Do not combine with <see cref="Language"/>.</summary>
    [JsonProperty("languages")]
    public List<string>? Languages { get; set; }

    /// <summary>Vocabulary hints for <c>gpt-live-transcribe</c>.</summary>
    [JsonProperty("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonProperty("prompt")]
    public string? Prompt { get; set; }

    /// <summary>Delay preset for <c>gpt-realtime-whisper</c>: minimal, low, medium, high, xhigh.</summary>
    [JsonProperty("delay")]
    public string? Delay { get; set; }
}

/// <summary>
/// Realtime session input audio configuration.
/// </summary>
public class RealtimeAudioInputConfig
{
    private RealtimeTurnDetection? turnDetection;
    private bool turnDetectionSet;

    [JsonProperty("format")]
    public RealtimeAudioFormat? Format { get; set; }

    [JsonProperty("noise_reduction")]
    public RealtimeNoiseReduction? NoiseReduction { get; set; }

    [JsonProperty("transcription")]
    public RealtimeInputTranscription? Transcription { get; set; }

    [JsonProperty("turn_detection", NullValueHandling = NullValueHandling.Include)]
    public RealtimeTurnDetection? TurnDetection
    {
        get => turnDetection;
        set
        {
            turnDetection = value;
            turnDetectionSet = true;
        }
    }

    /// <summary>Include explicit null values to disable turn detection.</summary>
    public bool ShouldSerializeTurnDetection() => turnDetectionSet;
}

/// <summary>
/// Realtime session output audio configuration.
/// </summary>
public class RealtimeAudioOutputConfig
{
    [JsonProperty("format")]
    public RealtimeAudioFormat? Format { get; set; }

    [JsonProperty("voice")]
    public object? Voice { get; set; }

    [JsonProperty("speed")]
    public double? Speed { get; set; }

    [JsonProperty("language")]
    public string? Language { get; set; }
}

/// <summary>
/// Combined input/output audio configuration (GA schema).
/// </summary>
public class RealtimeAudioConfig
{
    [JsonProperty("input")]
    public RealtimeAudioInputConfig? Input { get; set; }

    [JsonProperty("output")]
    public RealtimeAudioOutputConfig? Output { get; set; }
}

/// <summary>
/// Function tool for Realtime sessions.
/// </summary>
public class RealtimeFunctionTool
{
    [JsonProperty("type")]
    public string Type { get; set; } = "function";

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("parameters")]
    public object? Parameters { get; set; }
}

/// <summary>
/// GA voice-agent session configuration (<c>type: realtime</c>).
/// </summary>
public class RealtimeVoiceSessionConfig
{
    [JsonProperty("type")]
    public string Type { get; set; } = "realtime";

    [JsonProperty("model")]
    [JsonConverter(typeof(ChatModelJsonConverter))]
    public ChatModel? Model { get; set; }

    [JsonProperty("instructions")]
    public string? Instructions { get; set; }

    [JsonProperty("audio")]
    public RealtimeAudioConfig? Audio { get; set; }

    [JsonProperty("reasoning")]
    public RealtimeReasoningConfig? Reasoning { get; set; }

    [JsonProperty("output_modalities")]
    public List<string>? OutputModalities { get; set; }

    [JsonProperty("max_output_tokens")]
    public object? MaxOutputTokens { get; set; }

    [JsonProperty("tools")]
    public List<RealtimeFunctionTool>? Tools { get; set; }

    [JsonProperty("tool_choice")]
    public object? ToolChoice { get; set; }

    [JsonProperty("parallel_tool_calls")]
    public bool? ParallelToolCalls { get; set; }

    [JsonProperty("truncation")]
    public object? Truncation { get; set; }

    [JsonProperty("tracing")]
    public object? Tracing { get; set; }

    [JsonProperty("include")]
    public List<string>? Include { get; set; }

    public static RealtimeVoiceSessionConfig ForRealtime2(string? instructions = null, RealtimeReasoningEffort effort = RealtimeReasoningEffort.Low)
    {
        return new RealtimeVoiceSessionConfig
        {
            Model = ChatModelOpenAiRealtime.ModelRealtime2,
            Instructions = instructions,
            Reasoning = new RealtimeReasoningConfig { Effort = effort },
            Audio = new RealtimeAudioConfig
            {
                Input = new RealtimeAudioInputConfig
                {
                    Format = new RealtimeAudioFormat { Type = "audio/pcm", Rate = 24000 },
                    TurnDetection = new RealtimeTurnDetection { Type = "server_vad" }
                },
                Output = new RealtimeAudioOutputConfig
                {
                    Format = new RealtimeAudioFormat { Type = "audio/pcm", Rate = 24000 },
                    Voice = "marin"
                }
            }
        };
    }
}

/// <summary>
/// Transcription session configuration (<c>type: transcription</c>).
/// </summary>
public class RealtimeTranscriptionSessionConfig
{
    [JsonProperty("type")]
    public string Type { get; set; } = "transcription";

    [JsonProperty("audio")]
    public RealtimeAudioConfig? Audio { get; set; }

    [JsonProperty("include")]
    public List<string>? Include { get; set; }

    /// <summary>Streaming transcription with <c>gpt-live-transcribe</c> and manual audio commits.</summary>
    public static RealtimeTranscriptionSessionConfig ForLiveTranscribe(string language = "en", string delay = "low")
    {
        return new RealtimeTranscriptionSessionConfig
        {
            Audio = new RealtimeAudioConfig
            {
                Input = new RealtimeAudioInputConfig
                {
                    Format = new RealtimeAudioFormat { Type = "audio/pcm", Rate = 24000 },
                    Transcription = new RealtimeInputTranscription
                    {
                        Model = ChatModelOpenAiRealtime.ModelLiveTranscribe.Name,
                        Languages = [language],
                        Delay = delay
                    },
                    TurnDetection = null
                }
            }
        };
    }

    /// <summary>Default config for <c>gpt-realtime-whisper</c> with manual commit (turn_detection null).</summary>
    public static RealtimeTranscriptionSessionConfig ForRealtimeWhisper(string language = "en", string delay = "low")
    {
        return new RealtimeTranscriptionSessionConfig
        {
            Audio = new RealtimeAudioConfig
            {
                Input = new RealtimeAudioInputConfig
                {
                    Format = new RealtimeAudioFormat { Type = "audio/pcm", Rate = 24000 },
                    Transcription = new RealtimeInputTranscription
                    {
                        Model = ChatModelOpenAiRealtime.ModelRealtimeWhisper.Name,
                        Language = language,
                        Delay = delay
                    },
                    TurnDetection = null
                }
            }
        };
    }
}

/// <summary>
/// Request body for <c>POST /v1/realtime/client_secrets</c>.
/// </summary>
public class RealtimeClientSecretRequest
{
    [JsonProperty("expires_after")]
    public RealtimeClientSecretExpiresAfter? ExpiresAfter { get; set; }

    [JsonProperty("session")]
    public object? Session { get; set; }
}

/// <summary>
/// Response from <c>POST /v1/realtime/client_secrets</c>.
/// </summary>
public class RealtimeClientSecretResponse
{
    [JsonProperty("value")]
    public string? Value { get; set; }

    [JsonProperty("expires_at")]
    public long? ExpiresAt { get; set; }

    [JsonProperty("session")]
    public JObject? Session { get; set; }
}

/// <summary>
/// GA Realtime server event type names.
/// </summary>
public static class RealtimeEventTypes
{
    public const string SessionCreated = "session.created";
    public const string SessionUpdated = "session.updated";
    public const string ResponseOutputTextDelta = "response.output_text.delta";
    public const string ResponseOutputAudioDelta = "response.output_audio.delta";
    public const string ResponseOutputAudioTranscriptDelta = "response.output_audio_transcript.delta";
    public const string ConversationItemAdded = "conversation.item.added";
    public const string ConversationItemDone = "conversation.item.done";
    public const string InputAudioBufferSpeechStarted = "input_audio_buffer.speech_started";
    public const string InputAudioBufferSpeechStopped = "input_audio_buffer.speech_stopped";
    public const string InputAudioBufferCommitted = "input_audio_buffer.committed";
    public const string TranscriptionDelta = "conversation.item.input_audio_transcription.delta";
    public const string TranscriptionCompleted = "conversation.item.input_audio_transcription.completed";
    public const string TranscriptionFailed = "conversation.item.input_audio_transcription.failed";
    public const string Error = "error";

    /// <summary>Translation session: source transcript delta.</summary>
    public const string SessionInputTranscriptDelta = "session.input_transcript.delta";

    /// <summary>Translation session: translated transcript delta.</summary>
    public const string SessionOutputTranscriptDelta = "session.output_transcript.delta";

    /// <summary>Translation session: translated PCM16 audio delta.</summary>
    public const string SessionOutputAudioDelta = "session.output_audio.delta";

    /// <summary>Translation session closed.</summary>
    public const string SessionClosed = "session.closed";
}

/// <summary>
/// Parsed Realtime server event (WebSocket).
/// </summary>
public class RealtimeServerEvent
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("event_id")]
    public string? EventId { get; set; }

    [JsonProperty("item_id")]
    public string? ItemId { get; set; }

    [JsonProperty("content_index")]
    public int? ContentIndex { get; set; }

    [JsonProperty("delta")]
    public string? Delta { get; set; }

    [JsonProperty("transcript")]
    public string? Transcript { get; set; }

    [JsonProperty("error")]
    public RealtimeErrorPayload? Error { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? ExtensionData { get; set; }

    public string? RawJson { get; set; }
}

/// <summary>
/// Realtime API error payload on WebSocket <c>error</c> events.
/// </summary>
public class RealtimeErrorPayload
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("code")]
    public string? Code { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("param")]
    public string? Param { get; set; }
}

/// <summary>
/// Typed handlers for Realtime transcription WebSocket streaming.
/// </summary>
public class RealtimeTranscriptionStreamEventHandler
{
    public Func<RealtimeServerEvent, ValueTask>? OnEvent { get; set; }
    public Func<RealtimeServerEvent, ValueTask>? OnTranscriptionDelta { get; set; }
    public Func<RealtimeServerEvent, ValueTask>? OnTranscriptionCompleted { get; set; }
    public Func<RealtimeServerEvent, ValueTask>? OnTranscriptionFailed { get; set; }
    public Func<RealtimeErrorPayload, ValueTask>? OnError { get; set; }

    internal async ValueTask DispatchAsync(RealtimeServerEvent evt)
    {
        if (OnEvent is not null)
        {
            await OnEvent(evt).ConfigureAwait(false);
        }

        switch (evt.Type)
        {
            case RealtimeEventTypes.TranscriptionDelta:
                if (OnTranscriptionDelta is not null)
                {
                    await OnTranscriptionDelta(evt).ConfigureAwait(false);
                }
                break;
            case RealtimeEventTypes.TranscriptionCompleted:
                if (OnTranscriptionCompleted is not null)
                {
                    await OnTranscriptionCompleted(evt).ConfigureAwait(false);
                }
                break;
            case RealtimeEventTypes.TranscriptionFailed:
                if (OnTranscriptionFailed is not null)
                {
                    await OnTranscriptionFailed(evt).ConfigureAwait(false);
                }
                break;
            case RealtimeEventTypes.Error:
                if (evt.Error is not null && OnError is not null)
                {
                    await OnError(evt.Error).ConfigureAwait(false);
                }
                break;
        }
    }
}
