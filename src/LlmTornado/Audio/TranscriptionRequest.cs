using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Audio.Google;
using LlmTornado.Audio.Vendors.Google;
using LlmTornado.Audio.Models;
using LlmTornado.Audio.Models.OpenAi;
using LlmTornado.Audio.Vendors.Zai;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Chat.Vendors.Cohere;
using LlmTornado.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace LlmTornado.Audio;

/// <summary>
///     Transcribes audio into the input language.
/// </summary>
public class TranscriptionRequest
{
    /// <summary>Gemini transcription input MIME type and native recognition options.</summary>
    [JsonIgnore]
    public GoogleTranscriptionOptions? Google { get; set; }

    /// <summary>
    ///     The language of the input audio. Supplying the input language in ISO-639-1 format will improve accuracy and
    ///     latency.Visit to list ISO-639-1 formats <see href="https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes" />
    /// </summary>
    [JsonProperty("language")]
    public string Language { get; set; }
    
    /// <summary>
    ///     Specifies the response should be streamed. This is set automatically by the library.
    /// </summary>
    [JsonProperty("stream")]
    public bool? Stream { get; internal set; }
    
    /// <summary>
    ///     The audio file to transcribe, in one of these formats: mp3, mp4, mpeg, mpga, m4a, wav, or webm.
    ///     Either File or Url must be provided.
    /// </summary>
    [JsonProperty("file")]
    public AudioFile File { get; set; }

    /// <summary>
    ///     The audio URL to transcribe (supports Base64 data URLs). Either File or Url must be provided.
    ///     Supported by Groq. When using Groq's Batch API, only Url is supported.
    /// </summary>
    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public string? Url { get; set; }

    /// <summary>
    ///     ID of the model to use. Only whisper-1 is currently available.
    /// </summary>
    [JsonProperty("model")]
    public AudioModel Model { get; set; } = AudioModel.OpenAi.Whisper.V2;

    /// <summary>
    ///     An optional text to guide the model's style or continue a previous audio segment. The Prompt should match the audio
    ///     language. Please review href="https://platform.openai.com/docs/guides/speech-to-text/prompting"/>
    /// </summary>
    [JsonProperty("prompt")]
    public string? Prompt { get; set; }

    /// <summary>
    ///     The format of the transcript output, in one of these options: json, text, srt, verbose_json, or vtt.
    /// </summary>
    [JsonProperty("response_format")]
    public AudioTranscriptionResponseFormats ResponseFormat
    {
        get => responseFormat ?? DefaultResponseFormat;
        set => responseFormat = value;
    }

    private AudioTranscriptionResponseFormats? responseFormat;

    // OpenAI models outside the Whisper family (gpt-4o-transcribe, gpt-4o-mini-transcribe, ...) only accept "json".
    private AudioTranscriptionResponseFormats DefaultResponseFormat => Model?.Provider is LLmProviders.OpenAi && !AudioModelOpenAi.VerboseJsonCompatibleModels.Contains(Model)
        ? AudioTranscriptionResponseFormats.Json
        : AudioTranscriptionResponseFormats.VerboseJson;

    internal string GetResponseFormat => ResponseFormat switch
    {
        AudioTranscriptionResponseFormats.Json => "json",
        AudioTranscriptionResponseFormats.Text => "text",
        AudioTranscriptionResponseFormats.Srt => "srt",
        AudioTranscriptionResponseFormats.VerboseJson => "verbose_json",
        AudioTranscriptionResponseFormats.Vtt => "vtt",
        AudioTranscriptionResponseFormats.DiarizedJson => "diarized_json",
        _ => string.Empty
    };
    
    /// <summary>
    /// Controls how the audio is cut into chunks.
    /// </summary>
    [JsonProperty("chunking_strategy")]
    public TranscriptionChunkingStrategy? ChunkingStrategy { get; set; }
    
    /// <summary>
    /// Optional list of speaker names that correspond to the audio samples provided in `known_speaker_references[]`.
    /// </summary>
    [JsonProperty("known_speaker_names")]
    public List<string>? KnownSpeakerNames { get; set; }

    /// <summary>
    /// Optional list of audio samples (as data URLs) that contain known speaker references matching `known_speaker_names[]`.
    /// </summary>
    [JsonProperty("known_speaker_references")]
    public List<string>? KnownSpeakerReferences { get; set; }

    /// <summary>
    ///     The sampling temperature, between 0 and 1. Higher values like 0.8 will make the output more random, while lower
    ///     values like 0.2 will make it more focused and deterministic. If set to 0, the model will use log probability to
    ///     automatically increase the temperature until certain thresholds are hit.
    /// </summary>
    [JsonProperty("temperature")]
    public float? Temperature { get; set; }
    
    /// <summary>
    /// The timestamp_granularities[] parameter enables a more structured and timestamped json output format, with timestamps at the segment, word level, or both. This enables word-level precision for transcripts and video edits, which allows for the removal of specific frames tied to individual words.
    /// </summary>
    public HashSet<TimestampGranularities>? TimestampGranularities { get; set; }
    
    /// <summary>
    /// Additional information to include in the transcription response. logprobs will return the log probabilities of the tokens in the response to understand the model's confidence in the transcription. logprobs only works with response_format set to json and only with the models gpt-4o-transcribe and gpt-4o-mini-transcribe.
    /// </summary>
    public HashSet<TranscriptionRequestIncludeItems>? Include { get; set; }

    /// <summary>
    /// When true, Mistral Voxtral Transcribe 2 returns speaker labels and per-segment start/end times.
    /// Not compatible with the realtime transcription model.
    /// </summary>
    [JsonProperty("diarize")]
    public bool? Diarize { get; set; }

    /// <summary>
    /// Up to 100 words or phrases that bias Mistral transcription toward correct spellings (names, technical terms).
    /// </summary>
    [JsonProperty("context_bias")]
    public List<string>? ContextBias { get; set; }

    /// <summary>
    /// Cancellation token.
    /// </summary>
    [JsonIgnore]
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
    
    /// <summary>
    /// Z.AI-specific extensions for transcription requests.
    /// Includes hotwords for domain-specific vocabulary, request/user IDs, and base64 file alternative.
    /// </summary>
    [JsonIgnore]
    public TranscriptionRequestZaiExtensions? ZaiExtensions { get; set; }
    
    [JsonIgnore]
    internal string? UrlOverride { get; set; }
    
    private static readonly Dictionary<LLmProviders, Func<TranscriptionRequest, IEndpointProvider, string>> SerializeMap = new Dictionary<LLmProviders, Func<TranscriptionRequest, IEndpointProvider, string>>
    {
        { LLmProviders.OpenAi, (x, y) => JsonConvert.SerializeObject(x, EndpointBase.NullSettings) },
        { LLmProviders.Groq, (x, y) => JsonConvert.SerializeObject(x, EndpointBase.NullSettings) },
        { LLmProviders.Zai, (x, y) => JsonConvert.SerializeObject(x, EndpointBase.NullSettings) },
        { LLmProviders.Cohere, (x, y) => JsonConvert.SerializeObject(x, EndpointBase.NullSettings) }
    };
    
    /// <summary>
    ///		Serializes the chat request into the request body, based on the conventions used by the LLM provider.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public TornadoRequestContent Serialize(IEndpointProvider provider)
    {
        if (provider.Provider == LLmProviders.Google)
            throw new InvalidOperationException("Use SerializeAsync for Gemini transcription, which can read an audio stream.");
        return SerializeMap.TryGetValue(provider.Provider, out Func<TranscriptionRequest, IEndpointProvider, string>? serializerFn) ? new TornadoRequestContent(serializerFn.Invoke(this, provider), Model, UrlOverride, provider, CapabilityEndpoints.Audio) : new TornadoRequestContent(string.Empty, Model, UrlOverride, provider, CapabilityEndpoints.Audio);
    }

    /// <summary>Serializes transcription input, reading a Gemini inline audio stream asynchronously when needed.</summary>
    public async Task<TornadoRequestContent> SerializeAsync(IEndpointProvider provider)
    {
        if (provider.Provider != LLmProviders.Google) return Serialize(provider);
        var request = await VendorGoogleTranscriptionHandler.CreateRequest(this).ConfigureAwait(false);
        return new TornadoRequestContent(request.Serialize(), Model, provider.ApiUrl(CapabilityEndpoints.Interactions, null), provider, CapabilityEndpoints.Interactions);
    }
}

/// <summary>
/// Additional information to include in the transcription response. 
/// </summary>
public enum TranscriptionRequestIncludeItems
{
    /// <summary>
    /// logprobs will return the log probabilities of the tokens in the response to understand the model's confidence in the transcription.
    /// </summary>
    Logprobs
}

internal class TranscriptionRequestIncludeItemsCls
{
    public static string Encode(TranscriptionRequestIncludeItems item)
    {
        return item switch
        {
            TranscriptionRequestIncludeItems.Logprobs => "logprobs",
            _ => string.Empty
        };
    }
}

/// <summary>
/// Additional metadata for JSON transcriptions.
/// </summary>
public enum TimestampGranularities
{
    /// <summary>
    /// Word level timestamps.
    /// </summary>
    Word,
    
    /// <summary>
    /// Segment level timestamps.
    /// </summary>
    Segment
}

internal class TimestampGranularitiesCls
{
    public static string Encode(TimestampGranularities granularity)
    {
        return granularity switch
        {
            TimestampGranularities.Word => "word",
            TimestampGranularities.Segment => "segment",
            _ => string.Empty
        };
    }
}

/// <summary>
/// Controls how the audio is cut into chunks.
/// </summary>
public class TranscriptionChunkingStrategy
{
    /// <summary>
    /// The type of chunking strategy.
    /// </summary>
    [JsonProperty("type")]
    public TranscriptionChunkingStrategyType Type { get; set; }

    /// <summary>
    /// Amount of audio to include before the VAD detected speech (in milliseconds).
    /// </summary>
    [JsonProperty("prefix_padding_ms")]
    public int? PrefixPaddingMs { get; set; }
    
    /// <summary>
    /// Duration of silence to detect speech stop (in milliseconds).
    /// </summary>
    [JsonProperty("silence_duration_ms")]
    public int? SilenceDurationMs { get; set; }
    
    /// <summary>
    /// Sensitivity threshold (0.0 to 1.0) for voice activity detection.
    /// </summary>
    [JsonProperty("threshold")]
    public float? Threshold { get; set; }

    /// <summary>
    /// Automatically set chunking parameters based on the audio.
    /// </summary>
    public static readonly TranscriptionChunkingStrategy Auto = new TranscriptionChunkingStrategy { Type = TranscriptionChunkingStrategyType.Auto };
    
    /// <summary>
    /// Manual chunking using server side VAD.
    /// </summary>
    public static TranscriptionChunkingStrategy ServerVad(int? prefixPaddingMs = 300, int? silenceDurationMs = 200, float? threshold = 0.5f)
    {
        return new TranscriptionChunkingStrategy 
        { 
            Type = TranscriptionChunkingStrategyType.ServerVad,
            PrefixPaddingMs = prefixPaddingMs,
            SilenceDurationMs = silenceDurationMs,
            Threshold = threshold
        };
    }
}

/// <summary>
///     Strategies for chunking audio.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum TranscriptionChunkingStrategyType
{
    /// <summary>
    ///     Auto.
    /// </summary>
    [EnumMember(Value = "auto")]
    Auto,
    
    /// <summary>
    ///     Server VAD.
    /// </summary>
    [EnumMember(Value = "server_vad")]
    ServerVad
}
