using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using LlmTornado.Chat.Models;
using LlmTornado.Chat.Vendors.Cohere;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static LlmTornado.Chat.ChatMessagePartCitationWebSearchResultLocation;

namespace LlmTornado.Chat.Vendors.Google;

/// <summary>
/// Generation config for Google.
/// </summary>
internal class VendorGoogleChatRequestGenerationConfig
{
    /// <summary>
    /// Optional. The set of character sequences (up to 5) that will stop output generation. If specified, the API will stop at the first appearance of a stop_sequence. The stop sequence will not be included as part of the response.
    /// </summary>
    [JsonProperty("stopSequences")]
    public List<string>? StopSequences { get; set; }
    
    /// <summary>
    /// Output response mimetype of the generated candidate text. Supported mimetype: text/plain: (default) Text output. application/json: JSON response in the candidates.
    /// </summary>
    [JsonProperty("responseMimeType")]
    public string? ResponseMimeType { get; set; }
    
    /// <summary>
    /// Output response schema of the generated candidate text when response mime type can have schema. Schema can be objects, primitives or arrays and is a subset of OpenAPI schema.
    /// <see cref="ResponseMimeType"/> has to be "application/json".
    /// </summary>
    [JsonProperty("responseSchema")]
    public object? ResponseSchema { get; set; }
    
    /// <summary>
    /// The requested modalities of the response. Represents the set of modalities that the model can return, and should be expected in the response. This is an exact match to the modalities of the response.
    /// A model may have multiple combinations of supported modalities. If the requested modalities do not match any of the supported combinations, an error will be returned.
    /// An empty list is equivalent to requesting only text.
    /// <br/>
    /// MODALITY_UNSPECIFIED<br/>
    /// TEXT<br/>
    /// IMAGE<br/>
    /// AUDIO
    /// </summary>
    [JsonProperty("responseModalities")]
    public List<string>? ResponseModalities { get; set; }
    
    /// <summary>
    /// Optional. Number of generated responses to return. Currently, this value can only be set to 1. If unset, this will default to 1.
    /// </summary>
    [JsonProperty("candidateCount")]
    public int? CandidateCount { get; set; }
    
    /// <summary>
    /// Note: The default value varies by model, see the Model.output_token_limit attribute of the Model returned from the getModel function.
    /// </summary>
    [JsonProperty("maxOutputTokens")]
    public int? MaxOutputTokens { get; set; }
    
    /// <summary>
    /// Values can range from [0.0, 2.0].
    /// </summary>
    [JsonProperty("temperature")]
    public double? Temperature { get; set; }
    
    /// <summary>
    /// Optional. The maximum cumulative probability of tokens to consider when sampling. The model uses combined Top-k and Top-p (nucleus) sampling.
    /// </summary>
    [JsonProperty("topP")]
    public double? TopP { get; set; }
    
    /// <summary>
    /// Optional. The maximum number of tokens to consider when sampling.
    /// </summary>
    [JsonProperty("topK")]
    public int? TopK { get; set; }
    
    /// <summary>
    /// Optional. Seed used in decoding. If not set, the request uses a randomly generated seed.
    /// </summary>
    [JsonProperty("seed")]
    public int? Seed { get; set; }
    
    /// <summary>
    /// Optional. Presence penalty applied to the next token's logprobs if the token has already been seen in the response. This penalty is binary on/off and not dependant on the number of times the token is used (after the first). Use frequencyPenalty for a penalty that increases with each use. A positive penalty will discourage the use of tokens that have already been used in the response, increasing the vocabulary.
    /// </summary>
    [JsonProperty("presencePenalty")]
    public double? PresencePenalty { get; set; }
    
    /// <summary>
    /// Optional. Frequency penalty applied to the next token's logprobs, multiplied by the number of times each token has been seen in the respponse so far. A positive penalty will discourage the use of tokens that have already been used, proportional to the number of times the token has been used: The more a token is used, the more dificult it is for the model to use that token again increasing the vocabulary of responses. Caution: A negative penalty will encourage the model to reuse tokens proportional to the number of times the token has been used. Small negative values will reduce the vocabulary of a response. Larger negative values will cause the model to start repeating a common token until it hits the maxOutputTokens limit: "...the the the the the...".
    /// </summary>
    [JsonProperty("frequencyPenalty")]
    public double? FrequencyPenalty { get; set; }
    
    /// <summary>
    /// Optional. If true, export the logprobs results in response.
    /// </summary>
    [JsonProperty("responseLogprobs")]
    public bool? ResponseLogprobs { get; set; }
    
    /// <summary>
    /// Optional. Only valid if responseLogprobs=True. This sets the number of top logprobs to return at each decoding step in the Candidate.logprobs_result.
    /// </summary>
    [JsonProperty("logprobs")]
    public int? Logprobs { get; set; }
    
    /// <summary>
    /// Optional. Enables enhanced civic answers. It may not be available for all models.
    /// </summary>
    [JsonProperty("enableEnhancedCivicAnswers")]
    public bool? EnableEnhancedCivicAnswers { get; set; }
    
    /// <summary>
    /// Optional. The speech generation config.
    /// </summary>
    /// <returns></returns>
    [JsonProperty("speechConfig")]
    public VendorGoogleChatRequestSpeechConfig? SpeechConfig { get; set; }
    
    /// <summary>
    /// Optional. Config for thinking features.
    /// </summary>
    [JsonProperty("thinkingConfig")]
    public VendorGoogleChatRequestThinkingConfig? ThinkingConfig { get; set; }
    
    /// <summary>
    /// MEDIA_RESOLUTION_UNSPECIFIED = Media resolution has not been set.<br/>
    /// MEDIA_RESOLUTION_LOW = Media resolution set to low (64 tokens).<br/>
    /// MEDIA_RESOLUTION_MEDIUM	= Media resolution set to medium (256 tokens).<br/>
    /// MEDIA_RESOLUTION_HIGH = Media resolution set to high (zoomed reframing with 256 tokens).<br/>
    /// </summary>
    [JsonProperty("mediaResolution")]
    public string? MediaResolution { get; set; }
    
    /// <summary>
    /// Optional. Config for image generation.
    /// </summary>
    [JsonProperty("imageConfig")]
    public VendorGoogleChatRequestImageConfig? ImageConfig { get; set; }
}

/// <summary>
/// Configuration for image output in chat responses.
/// </summary>
internal class VendorGoogleChatRequestImageConfig
{
    /// <summary>
    /// The aspect ratio of the generated image.
    /// Supported values: "1:1", "1:4", "1:8", "2:3", "3:2", "3:4", "4:1", "4:3", "4:5", "5:4", "8:1", "9:16", "16:9", "21:9"
    /// </summary>
    [JsonProperty("aspectRatio")]
    public string? AspectRatio { get; set; }
    
    /// <summary>
    /// The resolution of the generated image.
    /// Supported values: "512" (Gemini 3.1 Flash Image only), "1K", "2K", "4K" (Gemini 3 Pro Image and Gemini 3.1 Flash Image).
    /// </summary>
    [JsonProperty("imageSize")]
    public string? ImageSize { get; set; }
}

internal class VendorGoogleChatRequestVoiceConfig
{
    /// <summary>
    /// The configuration for the prebuilt speaker to use.
    /// </summary>
    [JsonProperty("prebuiltVoiceConfig")]
    public VendorGoogleChatRequestPrebuiltVoiceConfig? PrebuiltVoiceConfig { get; set; }
}

internal class VendorGoogleChatRequestMultiSpeakerVoiceConfig
{
    /// <summary>
    /// Speaker voice assignments for multi-speaker TTS.
    /// </summary>
    [JsonProperty("speakerVoiceConfigs")]
    public List<VendorGoogleChatRequestMultiSpeakerVoiceConfigSpeaker>? SpeakerVoiceConfigs { get; set; }
}

internal class VendorGoogleChatRequestMultiSpeakerVoiceConfigSpeaker
{
    [JsonProperty("speaker")]
    public string Speaker { get; set; }
    
    [JsonProperty("voiceConfig")]
    public VendorGoogleChatRequestVoiceConfig? VoiceConfig { get; set; }
}

internal class VendorGoogleChatRequestPrebuiltVoiceConfig
{
    /// <summary>
    /// The name of the preset voice to use.
    /// </summary>
    [JsonProperty("voiceName")]
    public string? VoiceName { get; set; }
}

internal class VendorGoogleChatRequestSpeechConfig
{
    /// <summary>
    /// The configuration in case of single-voice output.
    /// </summary>
    [JsonProperty("voiceConfig")]
    public VendorGoogleChatRequestVoiceConfig? VoiceConfig { get; set; }
    
    /// <summary>
    /// The configuration in case of multi-speaker output.
    /// </summary>
    [JsonProperty("multi_speaker_voice_config")]
    public VendorGoogleChatRequestMultiSpeakerVoiceConfig? MultiSpeakerVoiceConfig { get; set; }
    
    /// <summary>
    /// Optional. Language code (in BCP 47 format, e.g. "en-US") for speech synthesis.
    /// Valid values are: de-DE, en-AU, en-GB, en-IN, es-US, fr-FR, hi-IN, pt-BR, ar-XA, es-ES, fr-CA, id-ID, it-IT, ja-JP, tr-TR, vi-VN, bn-IN, gu-IN, kn-IN, ml-IN, mr-IN, ta-IN, te-IN, nl-NL, ko-KR, cmn-CN, pl-PL, ru-RU, and th-TH.
    /// </summary>
    [JsonProperty("languageCode")]
    public string? LanguageCode { get; set; }
}

internal class VendorGoogleChatRequestThinkingConfig
{
    /// <summary>
    /// Indicates whether to include thoughts in the response. If true, thoughts are returned only when available.
    /// </summary>
    [JsonProperty("includeThoughts")]
    public bool? IncludeThoughts { get; set; }
    
    /// <summary>
    /// The thinkingBudget parameter gives the model guidance on the number of thinking tokens it can use when generating a response. A greater number of tokens is typically associated with more detailed thinking, which is needed for solving more complex tasks. thinkingBudget must be an integer in the range 0 to 24576. Setting the thinking budget to 0 disables thinking. Budgets from 1 to 1024 tokens will be set to 1024.
    /// Depending on the prompt, the model might overflow or underflow the token budget.
    /// </summary>
    [JsonProperty("thinkingBudget")]
    public int? ThinkingBudget { get; set; }
    
    /// <summary>
    /// The thinkingLevel parameter controls the maximum depth of the model's internal reasoning process before it produces a response. Gemini 3 treats these levels as relative allowances for thinking rather than strict token guarantees. If thinking_level is not specified, Gemini 3 Pro will default to high.
    /// </summary>
    [JsonProperty("thinkingLevel")]
    public string? ThinkingLevel { get; set; }
}

internal class VendorGoogleChatRequestMessagePart
{
    /// <summary>
    /// Indicates if the part is thought from the model.
    /// </summary>
    [JsonProperty("thought")]
    public bool? Thought { get; set; }
    
    /// <summary>
    /// An opaque signature for the thought so it can be reused in subsequent requests. A base64-encoded string.
    /// </summary>
    [JsonProperty("thoughtSignature")]
    public string? ThoughtSignature { get; set; }
    
    /// <summary>
    /// MEDIA_RESOLUTION_UNSPECIFIED = Media resolution has not been set.<br/>
    /// MEDIA_RESOLUTION_LOW = Media resolution set to low (64 tokens).<br/>
    /// MEDIA_RESOLUTION_MEDIUM	= Media resolution set to medium (256 tokens).<br/>
    /// MEDIA_RESOLUTION_HIGH = Media resolution set to high (zoomed reframing with 256 tokens).<br/>
    /// </summary>
    [JsonProperty("mediaResolution")]
    public VendorGoogleChatRequest.VendorGoogleChatRequestMediaResolution? MediaResolution { get; set; }

    /// <summary>
    /// Inline text.
    /// </summary>
    [JsonProperty("text")]
    public string? Text { get; set; }
    
    [JsonProperty("inlineData")]
    public VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartInlineData? InlineData { get; set; }
    
    [JsonProperty("functionCall")]
    public VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartFunctionCall? FunctionCall { get; set; }
    
    [JsonProperty("functionResponse")]
    public VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartFunctionResponse? FunctionResponse { get; set; }

    [JsonProperty("fileData")]
    public VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartFileData? FileData { get; set; }
    
    [JsonProperty("executableCode")]
    public VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartExecutableCode? ExecutableCode { get; set; }
    
    [JsonProperty("codeExecutionResult")]
    public VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartCodeExecutionResult? CodeExecutionResult { get; set; }

    [JsonProperty("toolCall")]
    public VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartServerSideToolCall? ToolCall { get; set; }

    [JsonProperty("toolResponse")]
    public VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartServerSideToolResponse? ToolResponse { get; set; }
    
    [JsonProperty("videoMetadata")]
    public VendorGoogleChatRequest.VendorGoogleChatRequestMetadataVideo? VideoMetadata { get; set; }
    
    public VendorGoogleChatRequestMessagePart()
    {
        
    }

    public VendorGoogleChatRequestMessagePart(ChatMessagePart part)
    {
        if (part.MediaResolution is not null && part.MediaResolution is not ChatMessagePartMediaResolution.Unspecified)
        {
            MediaResolution = new VendorGoogleChatRequest.VendorGoogleChatRequestMediaResolution
            {
                Level = part.MediaResolution switch
                {
                    ChatMessagePartMediaResolution.Low => "MEDIA_RESOLUTION_LOW",
                    ChatMessagePartMediaResolution.Medium => "MEDIA_RESOLUTION_MEDIUM",
                    ChatMessagePartMediaResolution.High => "MEDIA_RESOLUTION_HIGH",
                    ChatMessagePartMediaResolution.UltraHigh => "MEDIA_RESOLUTION_ULTRA_HIGH",
                    _ => "MEDIA_RESOLUTION_UNSPECIFIED"
                }
            };
        }
        
        switch (part.Type)
        {
            case ChatMessageTypes.Text:
            {
                Text = part.Text;
                break;
            }
            case ChatMessageTypes.Image:
            {
                if (part.Image is not null)
                {
                    (string resolvedMimeType, string resolvedData) = ResolveInlinePayload(part.Image.Url, part.Image.MimeType, string.Empty);
                    string? imageMimeType = part.Image.MimeType ?? (resolvedMimeType.Length > 0 ? resolvedMimeType : null);

                    if (imageMimeType is null)
                    {
                        throw new Exception("Google requires MIME type of all images to be set, supported values are: image/png, image/jpeg");
                    }
                    
                    InlineData = new VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartInlineData
                    {
                        MimeType = imageMimeType,
                        Data = resolvedData
                    };
                }
                
                break;
            }
            case ChatMessageTypes.Audio:
            {
                if (part.Audio is not null)
                {
                    if (part.Audio.MimeType is null)
                    {
                        throw new Exception("Google requires MIME type of all audio to be set, supported values are: audio/wav, audio/mp3, audio/aiff, audio/aac, audio/ogg, audio/flac");
                    }

                    InlineData = new VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartInlineData
                    {
                        MimeType = part.Audio.MimeType,
                        Data = part.Audio.Data
                    };
                }

                break;
            }
            case ChatMessageTypes.FileLink:
            {
                if (part.FileLinkData is not null)
                {
                    FileData = new VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartFileData
                    {
                        FileUri = part.FileLinkData.FileUri,
                        MimeType = part.FileLinkData.MimeType
                    };

                    if (part.FileLinkData.VideoProcessing is not null ||
                        part.FileLinkData.VideoStartOffset is not null ||
                        part.FileLinkData.VideoEndOffset is not null ||
                        part.FileLinkData.VideoFps is not null)
                    {
                        VideoMetadata = new VendorGoogleChatRequest.VendorGoogleChatRequestMetadataVideo
                        {
                            Processing = part.FileLinkData.VideoProcessing switch
                            {
                                ChatVideoProcessingMode.Agentic => "agentic",
                                ChatVideoProcessingMode.Static => "static",
                                _ => null
                            },
                            StartOffset = part.FileLinkData.VideoStartOffset,
                            EndOffset = part.FileLinkData.VideoEndOffset,
                            Fps = part.FileLinkData.VideoFps
                        };
                    }
                }
                
                break;
            }
            case ChatMessageTypes.Reasoning:
            {
                if (part.Reasoning is not null)
                {
                    Text = part.Reasoning.Content;
                    Thought = true;
                    ThoughtSignature = part.Reasoning.Signature;
                }
                
                break;
            }
            case ChatMessageTypes.ExecutableCode:
            {
                if (part.ExecutableCode is not null)
                {
                    ExecutableCode = new VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartExecutableCode
                    {
                        Code = part.ExecutableCode.Code,
                        Language = part.ExecutableCode.Language switch
                        {
                            ChatMessagePartExecutableCodeLanguage.Unknown => VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartExecutableCodeLanguage.Unspecified,
                            ChatMessagePartExecutableCodeLanguage.Python => VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartExecutableCodeLanguage.Python,
                            _ => VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartExecutableCodeLanguage.Unspecified
                        }
                    };
                }
                
                break;
            }
            case ChatMessageTypes.CodeExecutionResult:
            {
                if (part.CodeExecutionResult is not null)
                {
                    CodeExecutionResult = new VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartCodeExecutionResult
                    {
                        Output = part.CodeExecutionResult.Output,
                        Outcome = part.CodeExecutionResult.Outcome switch
                        {
                            ChatMessagePartCodeExecutionResultOutcomes.Unknown => VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartCodeExecutionResultOutcome.Unspecified,
                            ChatMessagePartCodeExecutionResultOutcomes.Timeout => VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartCodeExecutionResultOutcome.DeadlineExceeded,
                            ChatMessagePartCodeExecutionResultOutcomes.Failed => VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartCodeExecutionResultOutcome.Failed,
                            ChatMessagePartCodeExecutionResultOutcomes.Ok => VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartCodeExecutionResultOutcome.Ok,
                            _ => VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartCodeExecutionResultOutcome.Unspecified
                        }
                    };
                }
                
                break;
            }
            case ChatMessageTypes.ServerSideToolCall:
            case ChatMessageTypes.ServerSideToolResponse:
            case ChatMessageTypes.GoogleVendorPart:
            {
                if (part.NativeObject is VendorGoogleChatRequestMessagePart nativePart)
                {
                    Thought = nativePart.Thought;
                    ThoughtSignature = nativePart.ThoughtSignature;
                    Text = nativePart.Text;
                    FunctionCall = nativePart.FunctionCall;
                    FunctionResponse = nativePart.FunctionResponse;
                    ToolCall = nativePart.ToolCall;
                    ToolResponse = nativePart.ToolResponse;
                    ExecutableCode = nativePart.ExecutableCode;
                    CodeExecutionResult = nativePart.CodeExecutionResult;
                    InlineData = nativePart.InlineData;
                    FileData = nativePart.FileData;
                    MediaResolution = nativePart.MediaResolution;
                }

                break;
            }
        }
    }

    void SetInlinePayload(string content, string? mimeType, string defaultMimeType)
    {
        (string resolvedMimeType, string resolvedData) = ResolveInlinePayload(content, mimeType, defaultMimeType);
        InlineData = new VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartInlineData
        {
            MimeType = resolvedMimeType,
            Data = resolvedData
        };
    }

    void SetFilePayload(string fileUri, string? mimeType)
    {
        FileData = new VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartFileData
        {
            FileUri = fileUri,
            MimeType = mimeType
        };
    }

    static (string MimeType, string Data) ResolveInlinePayload(string content, string? mimeType, string defaultMimeType)
    {
        if (content.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            int semicolonIndex = content.IndexOf(';', 5);
            int commaIndex = content.IndexOf(',', 5);

            if (commaIndex > 0)
            {
                string parsedMimeType = semicolonIndex > 5 && semicolonIndex < commaIndex
                    ? content[5..semicolonIndex]
                    : defaultMimeType;

                return (parsedMimeType, content[(commaIndex + 1)..]);
            }
        }

        return (mimeType ?? defaultMimeType, content.StripDataUriPrefix());
    }

    public ChatMessagePart ToMessagePart(StringBuilder sb)
    {
        ChatMessagePart part = new ChatMessagePart();

        if (MediaResolution is not null)
        {
            part.MediaResolution = MediaResolution.Level switch
            {
                "MEDIA_RESOLUTION_LOW" => ChatMessagePartMediaResolution.Low,
                "MEDIA_RESOLUTION_MEDIUM" => ChatMessagePartMediaResolution.Medium,
                "MEDIA_RESOLUTION_HIGH" => ChatMessagePartMediaResolution.High,
                "MEDIA_RESOLUTION_ULTRA_HIGH" => ChatMessagePartMediaResolution.UltraHigh,
                _ => ChatMessagePartMediaResolution.Unspecified
            };
        }

        if (ExecutableCode is not null)
        {
            part.Type = ChatMessageTypes.ExecutableCode;
            part.ExecutableCode = new ChatMessagePartExecutableCode
            {
                Code = ExecutableCode.Code,
                Language = ExecutableCode.Language switch
                {
                    VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartExecutableCodeLanguage.Unspecified => ChatMessagePartExecutableCodeLanguage.Unknown,
                    VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartExecutableCodeLanguage.Python => ChatMessagePartExecutableCodeLanguage.Python,
                    _ => ChatMessagePartExecutableCodeLanguage.Unknown
                },
                NativeObject = ExecutableCode
            };
        }
        else if (CodeExecutionResult is not null)
        {
            part.Type = ChatMessageTypes.CodeExecutionResult;
            part.CodeExecutionResult = new ChatMessagePartCodeExecutionResult
            {
                Outcome = CodeExecutionResult.Outcome switch
                {
                    VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartCodeExecutionResultOutcome.Unspecified => ChatMessagePartCodeExecutionResultOutcomes.Unknown,
                    VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartCodeExecutionResultOutcome.Ok => ChatMessagePartCodeExecutionResultOutcomes.Ok,
                    VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartCodeExecutionResultOutcome.Failed => ChatMessagePartCodeExecutionResultOutcomes.Failed,
                    VendorGoogleChatRequest.VendorGoogleChatRequestMessagePartCodeExecutionResultOutcome.DeadlineExceeded => ChatMessagePartCodeExecutionResultOutcomes.Timeout,
                    _ => ChatMessagePartCodeExecutionResultOutcomes.Unknown
                },
                Output = CodeExecutionResult.Output,
                NativeObject = CodeExecutionResult
            };
        }
        else if (Text is not null)
        {
            if (Thought ?? false)
            {
                part.Type = ChatMessageTypes.Reasoning;
                part.Reasoning = new ChatMessageReasoningData
                {
                    Provider = LLmProviders.Google,
                    Content = Text,
                    Signature = ThoughtSignature
                };
            }
            else
            {
                part.Type = ChatMessageTypes.Text;
                part.Text = Text;
                sb.Append(Text);
            }
        }
        else if (InlineData is not null)
        {
            if (InlineData.MimeType.StartsWith("audio"))
            {
                part.Type = ChatMessageTypes.Audio;
                part.Audio = new ChatAudio(InlineData.Data, ChatAudioFormats.L16, InlineData.MimeType);
            }
            else
            {
                part.Type = ChatMessageTypes.Image;
                part.Image = new ChatImage(InlineData.Data)
                {
                    MimeType = InlineData.MimeType
                };   
            }
        }

        if (ToolCall is not null)
        {
            part.Type = ChatMessageTypes.ServerSideToolCall;
            part.NativeObject = this;
        }
        else if (ToolResponse is not null)
        {
            part.Type = ChatMessageTypes.ServerSideToolResponse;
            part.NativeObject = this;
        }
        else if (Thought ?? false)
        {
            part.Type = ChatMessageTypes.Reasoning;
            part.Reasoning = new ChatMessageReasoningData
            {
                Provider = LLmProviders.Google,
                Content = Text,
                Signature = ThoughtSignature
            };
        }

        return part;
    }

    public ToolCall ToToolCall()
    {
        if (FunctionCall is null)
        {
            return new ToolCall();
        }

        FunctionCall fc = new FunctionCall(FunctionCall.Args)
        {
            Name = FunctionCall.Name
        };

        ToolCall tc = new ToolCall
        {
            Id = FunctionCall.Id ?? FunctionCall.Name,
            FunctionCall = fc,
            ThoughtSignature = ThoughtSignature
        };

        return tc;
    }
}

internal class VendorGoogleChatRequest
{
    internal class VendorGoogleChatRequestMediaResolution
    {
        [JsonProperty("level")]
        public string? Level { get; set; }
    }

    internal class VendorGoogleChatRequestMessagePartInlineData
    {
        /// <summary>
        /// image/png - image/jpeg... https://ai.google.dev/gemini-api/docs/prompting_with_media#supported_file_formats
        /// audio/L16;codec=pcm;rate=24000
        /// </summary>
        [JsonProperty("mimeType")]
        public string MimeType { get; set; }
        
        /// <summary>
        /// A base64-encoded string.
        /// </summary>
        [JsonProperty("data")]
        public string Data { get; set; }
    }

    internal class VendorGoogleChatRequestMessagePartFunctionCall
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("args")]
        public Dictionary<string, object?> Args { get; set; } = [];
    }

    internal class VendorGoogleChatRequestMessagePartServerSideToolCall
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("toolType")]
        public string? ToolType { get; set; }

        [JsonProperty("args")]
        public Dictionary<string, object?>? Args { get; set; }
    }

    internal class VendorGoogleChatRequestMessagePartServerSideToolResponse
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("toolType")]
        public string? ToolType { get; set; }

        [JsonProperty("response")]
        public object? Response { get; set; }
    }

    internal class VendorGoogleChatRequestMessagePartFunctionResponse
    {
        /// <summary>
        /// Optional. The id of the function call this response is for.
        /// </summary>
        [JsonProperty("id")]
        public string? Id { get; set; }
        
        /// <summary>
        /// Required. The name of the function to call.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
        
        /// <summary>
        /// Required. The function response in JSON object format (Struct format).
        /// </summary>
        [JsonProperty("response")]
        public object? Response { get; set; }
        
        /// <summary>
        /// Optional. Ordered Parts that constitute a function response. Parts may have different IANA MIME types.
        /// </summary>
        [JsonProperty("parts")]
        public List<VendorGoogleChatRequestMessagePartFunctionResponsePart>? Parts { get; set; }
        
        /// <summary>
        /// Optional. Signals that function call continues, and more responses will be returned.
        /// </summary>
        [JsonProperty("willContinue")]
        public bool? WillContinue { get; set; }
        
        /// <summary>
        /// Optional. Specifies how the response should be scheduled in the conversation.
        /// Possible values:
        /// <list type="bullet">
        /// <item><description>"SCHEDULING_UNSPECIFIED" - This value is unused.</description></item>
        /// <item><description>"SILENT" - Only add the result to the conversation context, do not interrupt or trigger generation.</description></item>
        /// <item><description>"WHEN_IDLE" - Add the result to the conversation context, and prompt to generate output without interrupting ongoing generation. (Default)</description></item>
        /// <item><description>"INTERRUPT" - Add the result to the conversation context, interrupt ongoing generation and prompt to generate output.</description></item>
        /// </list>
        /// Only applicable to NON_BLOCKING function calls, is ignored otherwise. Defaults to WHEN_IDLE.
        /// </summary>
        [JsonProperty("scheduling")]
        public string? Scheduling { get; set; }
    }
    
    internal class VendorGoogleChatRequestMessagePartFunctionResponsePart
    {
        /// <summary>
        /// Inline media bytes.
        /// </summary>
        [JsonProperty("inlineData", NullValueHandling = NullValueHandling.Ignore)]
        public VendorGoogleChatRequestMessagePartFunctionResponseBlob? InlineData { get; set; }

        /// <summary>
        /// URI-based media.
        /// </summary>
        [JsonProperty("fileData", NullValueHandling = NullValueHandling.Ignore)]
        public VendorGoogleChatRequestMessagePartFunctionResponseFileData? FileData { get; set; }
    }
    
    internal class VendorGoogleChatRequestMessagePartFunctionResponseFileData
    {
        [JsonProperty("mimeType")]
        public string? MimeType { get; set; }
        
        [JsonProperty("fileUri")]
        public string FileUri { get; set; }
        
        [JsonProperty("displayName")]
        public string? DisplayName { get; set; }
    }
    
    internal class VendorGoogleChatRequestMessagePartFunctionResponseBlob
    {
        /// <summary>
        /// The IANA standard MIME type of the source data.
        /// </summary>
        [JsonProperty("mimeType")]
        public string MimeType { get; set; }
        
        /// <summary>
        /// Raw bytes for media formats (base64-encoded string).
        /// </summary>
        [JsonProperty("data")]
        public string Data { get; set; }

        /// <summary>
        /// Unique identifier for the part.
        /// </summary>
        [JsonProperty("displayName")]
        public string? DisplayName { get; set; }
    }

    /// <summary>
    /// Video metadata. The metadata should only be specified while the video data is presented in inlineData or fileData.
    /// </summary>
    internal class VendorGoogleChatRequestMetadataVideo
    {
        /// <summary>
        /// Optional. The start offset of the video.
        /// A duration in seconds with up to nine fractional digits, ending with 's'. Example: "3.5s".
        /// </summary>
        [JsonProperty("startOffset")]
        [JsonConverter(typeof(VendorGoogleChatRequestDurationConverter))]
        public TimeSpan? StartOffset { get; set; }

        /// <summary>
        /// Optional. The end offset of the video.
        /// A duration in seconds with up to nine fractional digits, ending with 's'. Example: "3.5s".
        /// </summary>
        [JsonProperty("endOffset")]
        [JsonConverter(typeof(VendorGoogleChatRequestDurationConverter))]
        public TimeSpan? EndOffset { get; set; }

        /// <summary>
        /// Optional. The frame rate of the video sent to the model. If not specified, the default value will be 1.0. The fps range is (0.0, 24.0].
        /// Only used with static video processing.
        /// </summary>
        [JsonProperty("fps")]
        public double? Fps { get; set; }

        /// <summary>
        /// Optional. Video processing mode. Use <c>agentic</c> on Gemini 3.6 / 3.7 / 3.8 Flash and 3.5 Flash-Lite
        /// so the model navigates the timeline on demand instead of sampling a static 1 FPS stream.
        /// </summary>
        [JsonProperty("processing")]
        public string? Processing { get; set; }
    }

    internal class VendorGoogleChatRequestDurationConverter : JsonConverter<TimeSpan?>
    {
        public override void WriteJson(JsonWriter writer, TimeSpan? value, JsonSerializer serializer)
        {
            if (value.HasValue)
            {
                writer.WriteValue($"{value.Value.TotalSeconds}s");
            }
            else
            {
                writer.WriteNull();
            }
        }

        public override TimeSpan? ReadJson(JsonReader reader, Type objectType, TimeSpan? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                string? s = (string?)reader.Value;
                
                if (s is not null && s.EndsWith('s'))
                {
                    if (double.TryParse(s[..^1], out double seconds))
                    {
                        return TimeSpan.FromSeconds(seconds);
                    }
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Result of executing the ExecutableCode.
    /// </summary>
    internal class VendorGoogleChatRequestMessagePartCodeExecutionResult
    {
        /// <summary>
        /// Required. Outcome of the code execution.
        /// </summary>
        [JsonProperty("outcome")]
        public VendorGoogleChatRequestMessagePartCodeExecutionResultOutcome Outcome { get; set; }

        /// <summary>
        /// Optional. Contains stdout when code execution is successful, stderr or other description otherwise.
        /// </summary>
        [JsonProperty("output")]
        public string? Output { get; set; }
    }

    /// <summary>
    /// Enumeration of possible outcomes of the code execution.
    /// </summary>
    internal enum VendorGoogleChatRequestMessagePartCodeExecutionResultOutcome
    {
        /// <summary>
        /// Unspecified status. This value should not be used.
        /// </summary>
        [EnumMember(Value = "OUTCOME_UNSPECIFIED")]
        Unspecified,
        
        /// <summary>
        /// Code execution completed successfully.
        /// </summary>
        [EnumMember(Value = "OUTCOME_OK")]
        Ok,
        
        /// <summary>
        /// Code execution finished but with a failure. stderr should contain the reason.
        /// </summary>
        [EnumMember(Value = "OUTCOME_FAILED")]
        Failed,
        
        /// <summary>
        /// Code execution ran for too long, and was cancelled. There may or may not be a partial output present.
        /// </summary>
        [EnumMember(Value = "OUTCOME_DEADLINE_EXCEEDED")]
        DeadlineExceeded
    }

    /// <summary>
    /// Code generated by the model that is meant to be executed.
    /// </summary>
    internal class VendorGoogleChatRequestMessagePartExecutableCode
    {
        /// <summary>
        /// Required. Programming language of the code.
        /// </summary>
        [JsonProperty("language")]
        public VendorGoogleChatRequestMessagePartExecutableCodeLanguage Language { get; set; }

        /// <summary>
        /// Required. The code to be executed.
        /// </summary>
        [JsonProperty("code")]
        public string Code { get; set; }
    }

    /// <summary>
    /// Supported programming languages for the generated code.
    /// </summary>
    internal enum VendorGoogleChatRequestMessagePartExecutableCodeLanguage
    {
        /// <summary>
        /// Unspecified language. This value should not be used.
        /// </summary>
        [EnumMember(Value = "LANGUAGE_UNSPECIFIED")]
        Unspecified,
        
        /// <summary>
        /// Python >= 3.10, with numpy and simpy available.
        /// </summary>
        [EnumMember(Value = "PYTHON")]
        Python
    }

    internal class VendorGoogleChatRequestMessagePartFileData
    {
        [JsonProperty("mimeType")]
        public string? MimeType { get; set; }
        
        [JsonProperty("fileUri")]
        public string FileUri { get; set; }
    }
    
    internal class VendorGoogleChatRequestMessage
    {
        [JsonProperty("parts")]
        public List<VendorGoogleChatRequestMessagePart>? Parts { get; set; } = [];
        
        /// <summary>
        /// The producer of the content. Must be either 'user' or 'model'.
        /// </summary>
        [JsonProperty("role")]
        public string? Role { get; set; }

        public VendorGoogleChatRequestMessage()
        {
            
        }

        public VendorGoogleChatRequestMessage(ChatMessage msg, VendorGoogleChatRequest? request)
        {
            if (msg.Role is ChatMessageRoles.Assistant && HasGoogleToolCombinationParts(msg))
            {
                Parts ??= [];

                foreach (ChatMessagePart part in msg.Parts!)
                {
                    if (part.NativeObject is VendorGoogleChatRequestMessagePart nativePart)
                    {
                        Parts.Add(nativePart);
                    }
                    else
                    {
                        Parts.Add(new VendorGoogleChatRequestMessagePart(part));
                    }
                }

                Role = "model";
            }
            else if (msg is { Role: ChatMessageRoles.Assistant, ToolCalls.Count: > 0 })
            {
                foreach (ToolCall call in msg.ToolCalls)
                {
                    string? thoughtSignature = call.ThoughtSignature;

                    if (thoughtSignature is null && request?.Model is not null && ChatModelGoogle.SupportsModernToolCalling(request.Model) && (request.VendorExtensions?.Google?.AutoInjectThoughtSignature ?? true))
                    {
                        thoughtSignature = "context_engineering_is_the_way_to_go";
                    }

                    Parts ??= [];
                    Parts.Add(new VendorGoogleChatRequestMessagePart
                    {
                        FunctionCall = new VendorGoogleChatRequestMessagePartFunctionCall
                        {
                            Id = call.Id,
                            Name = call.FunctionCall?.Name ?? call.Id ?? string.Empty,
                            Args = call.FunctionCall?.GetArguments() ?? []
                        },
                        ThoughtSignature = thoughtSignature
                    });
                }

                Role = "model";
            }
            else if (msg.Role is ChatMessageRoles.Tool)
            {
                Parts ??= [];

                object response;
                List<VendorGoogleChatRequestMessagePartFunctionResponsePart>? responseParts = null;
                
                if (msg.FunctionCall?.Result?.RawContentBlocks is not null)
                {
                    Dictionary<string, object> structuredResponse = new Dictionary<string, object>();
                    int partCounter = 0;
                    
                    foreach (IFunctionResultBlock block in msg.FunctionCall.Result.RawContentBlocks)
                    {
                        if (block is FunctionResultBlockText textBlock)
                        {
                            structuredResponse[$"text_{partCounter++}"] = textBlock.Text;
                        }
                        else if (block is FunctionResultBlockImage imageBlock)
                        {
                            responseParts ??= [];
                            string? displayName = null;

                            if (imageBlock.Source is FunctionResultBlockImageSourceBase64 b64)
                            {
                                displayName = b64.DisplayName ?? $"image_{partCounter++}.png";
                                responseParts.Add(new VendorGoogleChatRequestMessagePartFunctionResponsePart
                                {
                                    InlineData = new VendorGoogleChatRequestMessagePartFunctionResponseBlob
                                    {
                                        Data = b64.Data,
                                        MimeType = b64.MediaType,
                                        DisplayName = displayName
                                    }
                                });
                            }
                            else if (imageBlock.Source is FunctionResultBlockImageSourceUrl url)
                            {
                                displayName = url.DisplayName ?? $"image_{partCounter++}.png";
                                string mimeType = url.MimeType ?? (url.Url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || url.Url.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : url.Url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp" : "image/png");

                                responseParts.Add(new VendorGoogleChatRequestMessagePartFunctionResponsePart
                                {
                                    FileData = new VendorGoogleChatRequestMessagePartFunctionResponseFileData
                                    {
                                        FileUri = url.Url,
                                        MimeType = mimeType,
                                        DisplayName = displayName
                                    }
                                });
                            }
                            else if (imageBlock.Source is FunctionResultBlockImageSourceFile file)
                            {
                                displayName = file.DisplayName ?? $"image_{partCounter++}.png";
                                responseParts.Add(new VendorGoogleChatRequestMessagePartFunctionResponsePart
                                {
                                    FileData = new VendorGoogleChatRequestMessagePartFunctionResponseFileData
                                    {
                                        FileUri = file.FileId,
                                        MimeType = file.MimeType ?? "image/png",
                                        DisplayName = displayName
                                    }
                                });
                            }
                            
                            if (displayName is not null)
                            {
                                structuredResponse[$"image_ref_{partCounter}"] = new Dictionary<string, string> { { "$ref", displayName } };
                            }
                        }
                    }

                    response = structuredResponse;
                }
                else if ((msg.FunctionCall?.Result?.ContentIsSerialized ?? false) && msg.FunctionCall.Result.RawContent is not null)
                {
                    response = msg.FunctionCall.Result.RawContent;
                }
                else
                {
                    response = new
                    {
                        content = msg.Content ?? string.Empty
                    };
                }
                
                Parts.Add(new VendorGoogleChatRequestMessagePart
                {
                    FunctionResponse = new VendorGoogleChatRequestMessagePartFunctionResponse
                    {
                        Id = msg.ToolCallId,
                        Name = msg.FunctionCall?.Name ?? msg.Name ?? msg.ToolCallId ?? string.Empty,
                        Response = response,
                        Parts = responseParts
                    }
                });

                Role = "user";
            }
            else
            {
                if (msg.Parts?.Count > 0)
                {
                    Parts ??= [];
                    
                    foreach (ChatMessagePart x in msg.Parts)
                    {
                        Parts.Add(new VendorGoogleChatRequestMessagePart(x));
                    }
                }
                else if (msg.Content is not null)
                {
                    Parts ??= [];
                    Parts.Add(new VendorGoogleChatRequestMessagePart
                    {
                        Text = msg.Content
                    });
                }

                Role = msg.Role is ChatMessageRoles.User ? "user" : "model";
            }
        }

        public ChatMessage ToChatMessage(VendorGoogleChatRequest? request, ChatRequest? chatRequest, VendorGoogleChatResult.VendorGoogleChatResultMessage nativeResult)
        {
            ChatMessage msg = new ChatMessage
            {
                Parts = []
            };

            StringBuilder sb = new StringBuilder();
            bool roleSolved = false;
            bool contentSolved = false;
            bool preservePartOrder = Parts?.Any(p => p.ToolCall is not null || p.ToolResponse is not null) == true;
            
            foreach (VendorGoogleChatRequestMessagePart x in Parts ?? [])
            {
                if (x.FunctionCall is not null)
                {
                    msg.ToolCalls ??= [];
                    msg.ToolCalls.Add(x.ToToolCall());

                    if (preservePartOrder)
                    {
                        msg.Parts.Add(new ChatMessagePart(ChatMessageTypes.GoogleVendorPart) { NativeObject = x });
                    }
                }
                else if (x.ToolCall is not null || x.ToolResponse is not null)
                {
                    msg.Parts.Add(x.ToMessagePart(sb));
                }
                else
                {
                    if (x.Thought is not true && request?.GenerationConfig?.ResponseMimeType is "application/json")
                    {
                        string? fnName = chatRequest?.ResponseFormat?.Schema?.Name ?? request.ToolConfig?.FunctionConfig?.AllowedFunctionNames?.FirstOrDefault();

                        if (fnName is null)
                        {
                            Tool? firstTool = chatRequest?.Tools?.FirstOrDefault();

                            if (firstTool is not null)
                            {
                                fnName = firstTool.ToolName;
                                fnName ??= firstTool.Function?.Name;
                            }
                        }
                        
                        msg.ToolCalls ??= [];
                        msg.ToolCalls.Add(new ToolCall
                        {
                            Id = fnName ?? string.Empty,
                            FunctionCall = new FunctionCall
                            {
                                Name = fnName ?? string.Empty,
                                Arguments = x.Text ?? string.Empty
                            }
                        });

                        contentSolved = true;
                    }
                    else
                    {
                        msg.Parts.Add(x.ToMessagePart(sb));      
                    }
                }
            }

            if (!contentSolved)
            {
                msg.Content = sb.ToString();   
            }

            if (nativeResult.GroundingMetadata?.GroundingSupports.Count > 0)
            {
                ChatMessagePart? lastPart = msg.Parts.LastOrDefault(x => x.Type is ChatMessageTypes.Text) ?? msg.Parts.LastOrDefault();
 
                if (lastPart is not null)
                {
                    lastPart.Citations ??= [];

                    foreach (VendorGoogleChatResultGroundingSupport nativeCitation in nativeResult.GroundingMetadata.GroundingSupports)
                    {
                        List<VendorGoogleChatResultGroundingChunk> matchingSources = nativeResult.GroundingMetadata.GroundingChunks.Where((x, i) => nativeCitation.GroundingChunkIndices.Contains(i)).ToList();
                        
                        List<ChatMessagePartCitationWebGroundingSource> webSources = matchingSources.Where(x => x.Web is not null).Select(x => new ChatMessagePartCitationWebGroundingSource
                        {
                            Url = x.Web!.Uri,
                            Title = x.Web.Title
                        }).ToList();
                        
                        List<ChatMessagePartCitationWebGroundingSource> mapsSources = matchingSources.Where(x => x.Maps is not null).Select(x => new ChatMessagePartCitationWebGroundingSource
                        {
                            Url = x.Maps!.Uri,
                            Title = x.Maps.Title
                        }).ToList();

                        List<ChatMessagePartCitationWebGroundingSource> retrievedSources = matchingSources.Where(x => x.RetrievedContext is not null).Select(x => new ChatMessagePartCitationWebGroundingSource
                        {
                            Url = x.RetrievedContext!.Uri,
                            Title = x.RetrievedContext.Title,
                            Content = x.RetrievedContext.Text
                        }).ToList();
                        
                        List<ChatMessagePartCitationWebGroundingSource> allSources = [];
                        allSources.AddRange(webSources);
                        allSources.AddRange(mapsSources);
                        allSources.AddRange(retrievedSources);
                        
                        lastPart.Citations.Add(new ChatMessagePartCitationWebGrounding
                        {
                            Sources = allSources,
                            CitedText = nativeCitation.Segment.Text,
                            NativeObject = nativeCitation
                        });
                    }
                }
            }
            
            msg.Role = Role is "user" ? ChatMessageRoles.User : ChatMessageRoles.Assistant;
            return msg;
        }
    }
    
    internal class VendorGoogleChatRequestSafetySetting
    {
        /// <summary>
        /// HARM_CATEGORY_UNSPECIFIED, HARM_CATEGORY_DEROGATORY, HARM_CATEGORY_TOXICITY, HARM_CATEGORY_VIOLENCE,
        /// HARM_CATEGORY_SEXUAL, HARM_CATEGORY_MEDICAL, HARM_CATEGORY_DANGEROUS, HARM_CATEGORY_HARASSMENT,
        /// HARM_CATEGORY_HATE_SPEECH, HARM_CATEGORY_SEXUALLY_EXPLICIT, HARM_CATEGORY_DANGEROUS_CONTENT
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; }
        
        /// <summary>
        /// HARM_BLOCK_THRESHOLD_UNSPECIFIED, BLOCK_LOW_AND_ABOVE, BLOCK_MEDIUM_AND_ABOVE, BLOCK_ONLY_HIGH, BLOCK_NONE
        /// </summary>
        [JsonProperty("threshold")]
        public string Threshold { get; set; }

        internal VendorGoogleChatRequestSafetySetting()
        {
            
        }

        internal VendorGoogleChatRequestSafetySetting(string category, string threshold)
        {
            Category = category;
            Threshold = threshold;
        }
        
        /// <summary>
        /// Commented values are listed in docs but including them in the request results in 400 response
        /// </summary>
        internal static readonly List<string> AllCategories =
        [
            /*"HARM_CATEGORY_DEROGATORY",*/ /*"HARM_CATEGORY_TOXICITY", "HARM_CATEGORY_VIOLENCE",*/
            /*"HARM_CATEGORY_SEXUAL", "HARM_CATEGORY_MEDICAL",*/ /*"HARM_CATEGORY_DANGEROUS",*/ "HARM_CATEGORY_HARASSMENT",
            "HARM_CATEGORY_HATE_SPEECH", "HARM_CATEGORY_SEXUALLY_EXPLICIT", "HARM_CATEGORY_DANGEROUS_CONTENT"
        ];

        internal static readonly List<VendorGoogleChatRequestSafetySetting> DisableAll = [];

        static VendorGoogleChatRequestSafetySetting()
        {
            foreach (string str in AllCategories)
            {
                DisableAll.Add(new VendorGoogleChatRequestSafetySetting(str, "BLOCK_NONE"));
            }
        }
    }

    internal class VendorGoogleChatToolFunctionDeclaration
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("description")]
        public string Description { get; set; }
        
        /// <summary>
        /// JSON schema subset: https://ai.google.dev/api/rest/v1beta/Schema
        /// </summary>
        [JsonProperty("parameters")]
        public object? Parameters { get; set; }

        public VendorGoogleChatToolFunctionDeclaration()
        {
            
        }

        public VendorGoogleChatToolFunctionDeclaration(ToolFunction tool)
        {
            Name = tool.Name;
            Description = tool.Description;
            Parameters = tool.Parameters;
        }
    }

    internal class VendorGoogleChatTool
    {
        [JsonProperty("functionDeclarations")]
        public List<VendorGoogleChatToolFunctionDeclaration>? FunctionDeclarations { get; set; }

        [JsonProperty("googleSearchRetrieval")]
        public ChatRequestVendorGoogleSearchRetrieval? GoogleSearchRetrieval { get; set; }

        [JsonProperty("codeExecution")]
        public ChatRequestVendorGoogleCodeExecution? CodeExecution { get; set; }

        [JsonProperty("googleSearch")]
        public ChatRequestVendorGoogleSearch? GoogleSearch { get; set; }

        [JsonProperty("fileSearch")]
        public ChatRequestVendorGoogleFileSearch? FileSearch { get; set; }

        [JsonProperty("urlContext")]
        public ChatRequestVendorGoogleUrlContext? UrlContext { get; set; }

        [JsonProperty("computerUse")]
        public ChatRequestVendorGoogleComputerUse? ComputerUse { get; set; }

        [JsonProperty("googleMaps")]
        public ChatRequestVendorGoogleMaps? GoogleMaps { get; set; }

        public VendorGoogleChatTool()
        {

        }

        public VendorGoogleChatTool(Tool tool)
        {
            if (tool.Function is not null)
            {
                FunctionDeclarations ??= [];
                FunctionDeclarations.Add(new VendorGoogleChatToolFunctionDeclaration(tool.Function));
            }
        }
    }

    /// <summary>
    /// At least one member needs to be set, otherwise the request crashes if we include this field.
    /// </summary>
    internal class VendorGoogleChatToolConfigFunctionConfig
    {
        /// <summary>
        /// AUTO - Default model behavior, model decides to predict either a function call or a natural language response.
        /// ANY - Model is constrained to always predicting a function call only. If "allowedFunctionNames" are set, the predicted function call will be limited to any one of "allowedFunctionNames", else the predicted function call will be any one of the provided "functionDeclarations".
        /// NONE - Model will not predict any function call. Model behavior is same as when not passing any function declarations.
        /// VALIDATED - Like AUTO but validates function calls with constrained decoding. Required when combining built-in and custom tools.
        /// </summary>
        [JsonProperty("mode")]
        public string? Mode { get; set; } = "AUTO";
        
        /// <summary>
        /// This should only be set when the Mode is ANY. Function names should match [FunctionDeclaration.name]. With mode set to ANY, model will predict a function call from the set of function names provided.
        /// </summary>
        [JsonProperty("allowedFunctionNames")]
        public List<string>? AllowedFunctionNames { get; set; }
    }

    internal class VendorGoogleChatToolConfig
    {
        [JsonProperty("functionCallingConfig")]
        public VendorGoogleChatToolConfigFunctionConfig? FunctionConfig { get; set; }
        
        /// <summary>
        /// Retrieval configuration for Maps tool.
        /// </summary>
        [JsonProperty("retrievalConfig")]
        public ChatRequestVendorGoogleMapsRetrievalConfig? RetrievalConfig { get; set; }

        /// <summary>
        /// When true, server-side tool calls and responses are included in model content for tool context circulation.
        /// Required for combining built-in tools with custom function calling on Gemini 3 models.
        /// </summary>
        [JsonProperty("includeServerSideToolInvocations")]
        public bool? IncludeServerSideToolInvocations { get; set; }

        public static VendorGoogleChatToolConfig Default => new VendorGoogleChatToolConfig
        {
            FunctionConfig = new VendorGoogleChatToolConfigFunctionConfig
            {
                Mode = "AUTO"
            }
        };
    }
    
    [JsonProperty("contents")] 
    public List<VendorGoogleChatRequestMessage> Contents { get; set; } = [];
    
    [JsonProperty("tools")]
    public List<VendorGoogleChatTool>? Tools { get; set; }
    
    [JsonProperty("toolConfig")]
    public VendorGoogleChatToolConfig? ToolConfig { get; set; }
    
    [JsonProperty("safetySettings")]
    public List<VendorGoogleChatRequestSafetySetting>? SafetySettings { get; set; }
    
    [JsonProperty("generationConfig")]
    public VendorGoogleChatRequestGenerationConfig? GenerationConfig { get; set; }
    
    [JsonProperty("systemInstruction")]
    public VendorGoogleChatRequestMessage? SystemInstruction { get; set; }
    
    [JsonProperty("cachedContent")]
    public string? CachedContent { get; set; }
    
    /// <summary>
    /// Optional. Inference tier for cost/latency optimization. Supported values: flex, priority. Defaults to standard when omitted.
    /// </summary>
    [JsonProperty("service_tier")]
    public ChatRequestServiceTiers? ServiceTier { get; set; }
    
    public VendorGoogleChatRequest()
    {
        
    }

    public enum VendorGoogleRequestToolsResponseMode
    {
        Default,
        StructuredJson,
        Json
    }

    public static Tuple<List<VendorGoogleChatTool>?, VendorGoogleChatToolConfig?, VendorGoogleChatRequestGenerationConfig?> GetToolsAndToolChoice(ChatRequestResponseFormats? responseFormat, List<Tool>? tools, OutboundToolChoice? outboundToolChoice)
    {
        if (tools is null || tools.Count is 0)
        {
            return new Tuple<List<VendorGoogleChatTool>?, VendorGoogleChatToolConfig?, VendorGoogleChatRequestGenerationConfig?>(null, null, null);
        }
        
        List<VendorGoogleChatTool>? localTools = [];
        VendorGoogleChatToolConfig? localToolConfig = null;
        
        VendorGoogleChatRequestGenerationConfig? localConfig = null;
        bool anyStrictTool = false;
        
        // serialize all user-defined tools into one "tool" with multiple "functionDeclarations" as responses are better this way
        VendorGoogleChatTool toolWrapper = new VendorGoogleChatTool();
        
        foreach (Tool tool in tools)
        {
            if (tool.Function is not null)
            {
                toolWrapper.FunctionDeclarations ??= [];
                toolWrapper.FunctionDeclarations.Add(new VendorGoogleChatToolFunctionDeclaration(tool.Function));   
            }
        }

        if (toolWrapper.FunctionDeclarations?.Count > 0)
        {
            localTools.Add(toolWrapper);   
        }
        
        if (outboundToolChoice is not null)
        {
            localToolConfig = VendorGoogleChatToolConfig.Default;
            localToolConfig.FunctionConfig ??= new VendorGoogleChatToolConfigFunctionConfig();
            
            switch (outboundToolChoice.Mode)
            {
                case OutboundToolChoiceModes.Auto or OutboundToolChoiceModes.Legacy:
                {
                    localToolConfig.FunctionConfig.Mode = "AUTO";
                    break;
                }
                case OutboundToolChoiceModes.Required:
                {
                    localToolConfig.FunctionConfig.Mode = "ANY";
                    break;
                }
                case OutboundToolChoiceModes.None:
                {
                    localToolConfig.FunctionConfig.Mode = "NONE";
                    break;
                }
                case OutboundToolChoiceModes.ToolFunction:
                {
                    localToolConfig.FunctionConfig.Mode = "ANY";
                    localToolConfig.FunctionConfig.AllowedFunctionNames = [ outboundToolChoice.Function?.Name ?? string.Empty ];

                    Tool? match = tools.FirstOrDefault(x => x.Function?.Name == outboundToolChoice.Function?.Name);

                    if ((match?.Strict ?? false) && match.Function?.Parameters is not null)
                    {
                        bool canUpcast = false;
                        object? schemaObj = match.Function.Parameters;
                        
                        // check if we have compatible data in params
                        if (match.Function.Parameters is JObject jObject)
                        {
                            JToken? type = jObject["type"];
                            
                            if (type?.Type is JTokenType.String)
                            {
                                canUpcast = true;
                            }
                            else
                            {
                                JToken? pars = jObject["parameters"];

                                if (pars?.Type is JTokenType.Object)
                                {
                                    canUpcast = true;
                                    schemaObj = pars;
                                }
                            }
                        }
                        else
                        {
                            canUpcast = true;
                        }

                        if (canUpcast)
                        {
                            localConfig = new VendorGoogleChatRequestGenerationConfig
                            {
                                ResponseMimeType = "application/json",
                                ResponseSchema = schemaObj
                            };

                            // if we force strict json mode, these two fields must be cleared, otherwise the api throws due to these having precedence over responseMimeType
                            localTools = null;
                            localToolConfig = null;    
                        }
                    }
                    
                    break;
                }
            }
        }

        return new Tuple<List<VendorGoogleChatTool>?, VendorGoogleChatToolConfig?, VendorGoogleChatRequestGenerationConfig?>(localTools, localToolConfig, localConfig);
    }
    
    [JsonIgnore]
    public ChatModel? Model { get; set; }

    [JsonIgnore]
    public ChatRequestVendorExtensions? VendorExtensions { get; set; }

    [JsonIgnore]
    private bool RequiresV1Alpha { get; set; }

    public VendorGoogleChatRequest(ChatRequest request, IEndpointProvider provider)
    {
        Model = request.Model;
        VendorExtensions = request.VendorExtensions;
        
        IList<ChatMessage>? msgs = request.Messages;

        if (msgs is not null)
        {
            ChatMessage? sysMsg = msgs.FirstOrDefault(x => x.Role is ChatMessageRoles.System);

            if (sysMsg is not null)
            {
                if (request.Model is not null && ChatModelGoogle.ModelsWithDisabledDeveloperMessage.Contains(request.Model))
                {
                    // system prompt is unsupported
                }
                else
                {
                    SystemInstruction = new VendorGoogleChatRequestMessage(sysMsg, this);   
                }
            }
            
            foreach (ChatMessage msg in msgs)
            {
                if (msg.Role is ChatMessageRoles.System)
                {
                    continue;
                }
                
                Contents.Add(new VendorGoogleChatRequestMessage(msg, this));
            }
        }

        double? temperature = request.Temperature is null ? null : (request.Temperature ?? 0).Clamp(0, 2);

        GenerationConfig = new VendorGoogleChatRequestGenerationConfig
        {
            Temperature = temperature,
            TopP = request.TopP,
            TopK = request.TopK,
            MaxOutputTokens = request.MaxTokens,
            StopSequences = request.MultipleStopSequences is not null ? request.MultipleStopSequences.Take(5).ToList() : request.StopSequence is not null ? [ request.StopSequence ] : null,
            ResponseLogprobs = request.Logprobs,
            Logprobs = request.TopLogprobs,
            Seed = request.Seed,
            PresencePenalty = request.PresencePenalty,
            FrequencyPenalty = request.FrequencyPenalty
        };

        // thinkingConfig is not supported for non-thinking models
        if (request.Model is not null && ChatModelGoogle.ReasoningModels.Contains(request.Model))
        {
            int? clamped = request.Model.ClampReasoningTokens(request.ReasoningBudget);
            string? thinkingLevel = null;

            if (request.Model.Name == ChatModelGoogleGemini.ModelGeminiNanoBanana21.Name)
            {
                thinkingLevel = request.ReasoningEffort switch
                {
                    ChatReasoningEfforts.Minimal or ChatReasoningEfforts.Low => "minimal",
                    ChatReasoningEfforts.High or ChatReasoningEfforts.XHigh => "high",
                    _ => "medium"
                };
            }
            else if (request.Model.Name == ChatModelGoogleGemini.ModelGemini4Argon.Name || ChatModelGoogle.Gemini36PlusFlashModels.Contains(request.Model))
            {
                // Gemini 3.6 / 3.7 / 3.8 Flash: low, medium (default), high. minimal is rejected by the API.
                thinkingLevel = request.ReasoningEffort switch
                {
                    ChatReasoningEfforts.Minimal => "low",
                    ChatReasoningEfforts.Low => "low",
                    ChatReasoningEfforts.Medium => "medium",
                    ChatReasoningEfforts.High or ChatReasoningEfforts.XHigh => "high",
                    ChatReasoningEfforts.Default => "medium",
                    _ => null
                };
            }
            else if (ChatModelGoogle.Gemini35Models.Contains(request.Model))
            {
                // Gemini 3.5 Flash supports: minimal, low, medium, high (default: medium)
                thinkingLevel = request.ReasoningEffort switch
                {
                    ChatReasoningEfforts.Minimal => "minimal",
                    ChatReasoningEfforts.Low => "low",
                    ChatReasoningEfforts.Medium => "medium",
                    ChatReasoningEfforts.High or ChatReasoningEfforts.XHigh => "high",
                    ChatReasoningEfforts.Default => "medium",
                    _ => null
                };
            }
            else if (ChatModelGoogle.Gemini35FlashLiteModels.Contains(request.Model) || ChatModelGoogle.Gemini31FlashLiteModels.Contains(request.Model))
            {
                // Gemini 3.5 / 3.1 Flash-Lite supports: minimal (default), low, medium, high
                thinkingLevel = request.ReasoningEffort switch
                {
                    ChatReasoningEfforts.Minimal => "minimal",
                    ChatReasoningEfforts.Low => "low",
                    ChatReasoningEfforts.Medium => "medium",
                    ChatReasoningEfforts.High or ChatReasoningEfforts.XHigh => "high",
                    ChatReasoningEfforts.Default => "minimal",
                    _ => null
                };
            }
            else if (ChatModelGoogle.Gemini31Models.Contains(request.Model))
            {
                // Gemini 3.1 Pro supports: low, medium, high (not minimal)
                thinkingLevel = request.ReasoningEffort switch
                {
                    ChatReasoningEfforts.Minimal => "low",
                    ChatReasoningEfforts.Low => "low",
                    ChatReasoningEfforts.Medium => "medium",
                    ChatReasoningEfforts.High or ChatReasoningEfforts.XHigh => "high",
                    ChatReasoningEfforts.Default => "high",
                    _ => null
                };
            }
            else if (ChatModelGoogle.Gemini3Models.Contains(request.Model))
            {
                bool isFlash = request.Model == ChatModelGoogleGeminiPreview.ModelGemini3FlashPreview;

                // Gemini 3 Flash supports: minimal, low, medium, high
                // Gemini 3 Pro supports: low, high (not minimal, not medium)
                thinkingLevel = request.ReasoningEffort switch
                {
                    ChatReasoningEfforts.Minimal => isFlash ? "minimal" : "low",
                    ChatReasoningEfforts.Low => "low",
                    ChatReasoningEfforts.Medium => isFlash ? "medium" : "high",
                    ChatReasoningEfforts.High or ChatReasoningEfforts.XHigh => "high",
                    ChatReasoningEfforts.Default => "high",
                    _ => null
                };
            }
            else
            {
                thinkingLevel = request.ReasoningEffort switch
                {
                    ChatReasoningEfforts.Default => "low",
                    ChatReasoningEfforts.Minimal => "low",
                    ChatReasoningEfforts.Low => "low",
                    ChatReasoningEfforts.Medium => "high",
                    ChatReasoningEfforts.High => "high",
                    _ => null
                };
            }

            if (request.Model.Name is "gemini-4-argon" or "gemini-nano-banana-2.1")
            {
                thinkingLevel ??= "medium";
            }

            GenerationConfig.ThinkingConfig = new VendorGoogleChatRequestThinkingConfig
            {
                ThinkingBudget = thinkingLevel is null ? clamped : null,
                IncludeThoughts = clamped is not 0 && (request.VendorExtensions?.Google?.IncludeThoughts ?? true),
                ThinkingLevel = thinkingLevel
            };
        } 

        if (request.Modalities?.Count > 0 && request.Model is not null)
        {
            if (request.Modalities.Contains(ChatModelModalities.Image) && ChatModelGoogle.ImageModalitySupportingModels.Contains(request.Model))
            {
                GenerationConfig.ResponseModalities ??= [];
                GenerationConfig.ResponseModalities.Add("IMAGE");
                
                // Handle image output configuration
                if (request.ImageOutput is not null)
                {
                    GenerationConfig.ImageConfig = new VendorGoogleChatRequestImageConfig();
                    
                    if (request.ImageOutput.AspectRatio is not null)
                    {
                        GenerationConfig.ImageConfig.AspectRatio = request.ImageOutput.AspectRatio switch
                        {
                            ChatImageAspectRatios.Square => "1:1",
                            ChatImageAspectRatios.Portrait2x3 => "2:3",
                            ChatImageAspectRatios.Landscape3x2 => "3:2",
                            ChatImageAspectRatios.Portrait3x4 => "3:4",
                            ChatImageAspectRatios.Landscape4x3 => "4:3",
                            ChatImageAspectRatios.Portrait4x5 => "4:5",
                            ChatImageAspectRatios.Landscape5x4 => "5:4",
                            ChatImageAspectRatios.Portrait9x16 => "9:16",
                            ChatImageAspectRatios.Landscape16x9 => "16:9",
                            ChatImageAspectRatios.Ultrawide21x9 => "21:9",
                            ChatImageAspectRatios.Portrait1x4 => "1:4",
                            ChatImageAspectRatios.Landscape4x1 => "4:1",
                            ChatImageAspectRatios.Portrait1x8 => "1:8",
                            ChatImageAspectRatios.Landscape8x1 => "8:1",
                            _ => null
                        };
                    }
                    
                    if (request.ImageOutput.Resolution is not null)
                    {
                        GenerationConfig.ImageConfig.ImageSize = request.ImageOutput.Resolution switch
                        {
                            ChatImageResolutions.Resolution512 => "512",
                            ChatImageResolutions.Resolution1K => "1K",
                            ChatImageResolutions.Resolution2K => "2K",
                            ChatImageResolutions.Resolution4K => "4K",
                            _ => null
                        };
                    }
                }
            }

            if (request.Modalities.Contains(ChatModelModalities.Text))
            {
                GenerationConfig.ResponseModalities ??= [];
                GenerationConfig.ResponseModalities.Add("TEXT");
            }
            
            if (request.Modalities.Contains(ChatModelModalities.Audio))
            {
                GenerationConfig.ResponseModalities ??= [];
                GenerationConfig.ResponseModalities.Add("AUDIO");
            }
        }
        
        if (request.Tools?.Count > 0)
        {
            Tuple<List<VendorGoogleChatTool>?, VendorGoogleChatToolConfig?, VendorGoogleChatRequestGenerationConfig?> configUpdate = GetToolsAndToolChoice(request.ResponseFormat, request.Tools, request.ToolChoice);

            Tools = configUpdate.Item1;
            ToolConfig = configUpdate.Item2;

            if (configUpdate.Item3 is not null)
            {
                GenerationConfig.ResponseMimeType = configUpdate.Item3.ResponseMimeType;
                GenerationConfig.ResponseSchema = configUpdate.Item3.ResponseSchema;
            }
        }
        else
        {
            if (request.ResponseFormat?.Type is ChatRequestResponseFormatTypes.StructuredJson or ChatRequestResponseFormatTypes.Json)
            {
                string fnName = request.ResponseFormat.Schema?.Name ?? string.Empty;
                
                Tuple<List<VendorGoogleChatTool>?, VendorGoogleChatToolConfig?, VendorGoogleChatRequestGenerationConfig?> configUpdate = GetToolsAndToolChoice(request.ResponseFormat, request.ResponseFormat.Type is ChatRequestResponseFormatTypes.Json ? null : [
                    new Tool(new ToolFunction(fnName, string.Empty, request.ResponseFormat.Schema?.Schema ?? new
                    {
                        
                    }), request.ResponseFormat.Type is ChatRequestResponseFormatTypes.StructuredJson)
                ], request.ToolChoice ?? (request.ResponseFormat.Type is ChatRequestResponseFormatTypes.StructuredJson ? new OutboundToolChoice(OutboundToolChoiceModes.ToolFunction)
                {
                    Function = new OutboundToolCallFunction
                    {
                        Name = fnName
                    }
                } : null));
                
                Tools = configUpdate.Item1;
                ToolConfig = null; // configUpdate.Item2;

                if (configUpdate.Item3 is not null)
                {
                    GenerationConfig.ResponseMimeType = configUpdate.Item3.ResponseMimeType;
                    GenerationConfig.ResponseSchema = configUpdate.Item3.ResponseSchema;
                }
            }
        }

        if (request.ServiceTier is ChatRequestServiceTiers.Flex or ChatRequestServiceTiers.Priority)
        {
            ServiceTier = request.ServiceTier;
        }

        if (request.VendorExtensions?.Google is not null)
        {
            VendorGoogleChatTool? builtInTool = null;

            if (request.VendorExtensions.Google.CodeExecution is not null)
            {
                builtInTool ??= new VendorGoogleChatTool();
                builtInTool.CodeExecution = request.VendorExtensions.Google.CodeExecution;
            }

            if (request.VendorExtensions.Google.GoogleSearchRetrieval is not null)
            {
                builtInTool ??= new VendorGoogleChatTool();
                builtInTool.GoogleSearchRetrieval = request.VendorExtensions.Google.GoogleSearchRetrieval;
            }

            if (request.VendorExtensions.Google.GoogleSearch is not null)
            {
                builtInTool ??= new VendorGoogleChatTool();
                builtInTool.GoogleSearch = request.VendorExtensions.Google.GoogleSearch;
            }

            if (request.VendorExtensions.Google.FileSearch is not null)
            {
                builtInTool ??= new VendorGoogleChatTool();
                builtInTool.FileSearch = request.VendorExtensions.Google.FileSearch;
            }

            if (request.VendorExtensions.Google.UrlContext is not null)
            {
                builtInTool ??= new VendorGoogleChatTool();
                builtInTool.UrlContext = request.VendorExtensions.Google.UrlContext;
            }

            if (request.VendorExtensions.Google.ComputerUse is not null)
            {
                builtInTool ??= new VendorGoogleChatTool();
                builtInTool.ComputerUse = request.VendorExtensions.Google.ComputerUse;
            }

            if (request.VendorExtensions.Google.GoogleMaps is not null)
            {
                builtInTool ??= new VendorGoogleChatTool();
                builtInTool.GoogleMaps = request.VendorExtensions.Google.GoogleMaps;

                if (request.VendorExtensions.Google.GoogleMaps.RetrievalConfig is not null)
                {
                    ToolConfig ??= new VendorGoogleChatToolConfig();
                    ToolConfig.RetrievalConfig = request.VendorExtensions.Google.GoogleMaps.RetrievalConfig;
                }
            }

            if (builtInTool is not null)
            {
                Tools ??= [];
                Tools.Add(builtInTool);
            }
            
            if (request.VendorExtensions.Google.CachedContent is not null)
            {
                if (request.Model?.Name == "gemini-4-argon")
                {
                    throw new ArgumentException("Gemini 4 Argon supports implicit caching only; cachedContent is not supported.");
                }
                CachedContent = request.VendorExtensions.Google.CachedContent;
            }

            if (request.VendorExtensions.Google.ResponseSchema?.Function is not null)
            {
                GenerationConfig.ResponseMimeType = "application/json";
                GenerationConfig.ResponseSchema = request.VendorExtensions.Google.ResponseSchema.Function.Parameters;
            }

            if (request.VendorExtensions.Google.SpeechConfig is not null)
            {
                GenerationConfig.SpeechConfig = new VendorGoogleChatRequestSpeechConfig
                {
                    LanguageCode = request.VendorExtensions.Google.SpeechConfig.LanguageCode,
                    VoiceConfig = request.VendorExtensions.Google.SpeechConfig.VoiceName is not null
                        ? new VendorGoogleChatRequestVoiceConfig
                        {
                            PrebuiltVoiceConfig = new VendorGoogleChatRequestPrebuiltVoiceConfig
                            {
                                VoiceName = request.VendorExtensions.Google.SpeechConfig.VoiceName.VoiceName?.ToString().ToLowerInvariant()
                            }
                        }
                        : null,
                    MultiSpeakerVoiceConfig = request.VendorExtensions.Google.SpeechConfig.MultiSpeaker is not null
                        ? new VendorGoogleChatRequestMultiSpeakerVoiceConfig
                        {
                            SpeakerVoiceConfigs = request.VendorExtensions.Google.SpeechConfig.MultiSpeaker.Speakers.Select(x => new VendorGoogleChatRequestMultiSpeakerVoiceConfigSpeaker
                            {
                                Speaker = x.Speaker,
                                VoiceConfig = x.Voice is null ? null : new VendorGoogleChatRequestVoiceConfig
                                {
                                    PrebuiltVoiceConfig = new VendorGoogleChatRequestPrebuiltVoiceConfig
                                    {
                                        VoiceName = x.Voice.VoiceName?.ToString().ToLowerInvariant()
                                    }
                                }
                            }).ToList()
                        }
                        : null
                };
            }

            if (request.VendorExtensions.Google.SafetyFilters is not null)
            {
                if (request.VendorExtensions.Google.SafetyFilters == ChatRequestVendorGoogleSafetyFilters.Default)
                {
                    
                }
                else if (request.VendorExtensions.Google.SafetyFilters == ChatRequestVendorGoogleSafetyFilters.Minimal)
                {
                    SafetySettings = VendorGoogleChatRequestSafetySetting.DisableAll;   
                }
                else
                {
                    SafetySettings = [];

                    if (request.VendorExtensions.Google.SafetyFilters.SexuallyExplicit is not null)
                    {
                        SafetySettings.Add(new VendorGoogleChatRequestSafetySetting("HARM_CATEGORY_SEXUALLY_EXPLICIT", HarmFilter(request.VendorExtensions.Google.SafetyFilters.SexuallyExplicit)));
                    }
                    
                    if (request.VendorExtensions.Google.SafetyFilters.Harassment is not null)
                    {
                        SafetySettings.Add(new VendorGoogleChatRequestSafetySetting("HARM_CATEGORY_HARASSMENT", HarmFilter(request.VendorExtensions.Google.SafetyFilters.Harassment)));
                    }
                    
                    if (request.VendorExtensions.Google.SafetyFilters.DangerousContent is not null)
                    {
                        SafetySettings.Add(new VendorGoogleChatRequestSafetySetting("HARM_CATEGORY_DANGEROUS_CONTENT", HarmFilter(request.VendorExtensions.Google.SafetyFilters.DangerousContent)));
                    }
                    
                    if (request.VendorExtensions.Google.SafetyFilters.HateSpeech is not null)
                    {
                        SafetySettings.Add(new VendorGoogleChatRequestSafetySetting("HARM_CATEGORY_HATE_SPEECH", HarmFilter(request.VendorExtensions.Google.SafetyFilters.HateSpeech)));
                    }
                }
            }
            else
            {
                SafetySettings = VendorGoogleChatRequestSafetySetting.DisableAll;   
            }
        }
        else
        {
            SafetySettings = VendorGoogleChatRequestSafetySetting.DisableAll;   
        }

        // Check if we need to escalate to v1alpha API
        // MediaResolution on parts requires v1alpha
        if (Contents.Any(msg => msg.Parts?.Any(part => part.MediaResolution is not null) == true))
        {
            RequiresV1Alpha = true;
        }

        // URL override must be at the end after we determine if we need v1alpha
        string apiVersion = RequiresV1Alpha ? "v1alpha" : "v1beta";
        string baseUrl = provider.ApiUrl(CapabilityEndpoints.Chat, null);
        
        if (RequiresV1Alpha)
        {
            baseUrl = baseUrl.Replace("/v1beta/", $"/{apiVersion}/");
        }

        request.OverrideUrl($"{baseUrl}/{request.Model?.Name}:{(request.StreamResolved ? "streamGenerateContent" : "generateContent")}");
    }

    static bool HasGoogleToolCombinationParts(ChatMessage msg) =>
        msg.Parts?.Any(p => p.Type is ChatMessageTypes.ServerSideToolCall
            or ChatMessageTypes.ServerSideToolResponse
            or ChatMessageTypes.GoogleVendorPart) == true;

    static bool HasBuiltInGoogleTools(ChatRequestVendorGoogleExtensions google) =>
        google.GoogleSearch is not null
        || google.GoogleSearchRetrieval is not null
        || google.GoogleMaps is not null
        || google.UrlContext is not null
        || google.FileSearch is not null
        || google.CodeExecution is not null;

    static bool HasCustomFunctionTools(List<VendorGoogleChatTool>? tools) =>
        tools?.Any(t => t.FunctionDeclarations?.Count > 0) == true;

    static void ApplyCombinedToolConfiguration(VendorGoogleChatRequest vendorRequest, ChatRequest request)
    {
        ChatRequestVendorGoogleExtensions? google = request.VendorExtensions?.Google;
        if (google is null)
        {
            return;
        }

        bool isGemini3Family = ChatModelGoogle.SupportsModernToolCalling(request.Model);

        bool hasBuiltInTools = HasBuiltInGoogleTools(google);
        bool hasCustomTools = HasCustomFunctionTools(vendorRequest.Tools);
        bool? includeServerSide = google.IncludeServerSideToolInvocations;

        if (google.IncludeServerSideToolInvocations == true)
        {
            includeServerSide = true;
        }
        else if (includeServerSide is not false && hasBuiltInTools && hasCustomTools && isGemini3Family)
        {
            includeServerSide = true;
        }

        if (includeServerSide is not true)
        {
            return;
        }

        vendorRequest.ToolConfig ??= new VendorGoogleChatToolConfig();
        vendorRequest.ToolConfig.IncludeServerSideToolInvocations = true;

        if (vendorRequest.ToolConfig.FunctionConfig is null && hasCustomTools)
        {
            vendorRequest.ToolConfig.FunctionConfig = new VendorGoogleChatToolConfigFunctionConfig
            {
                Mode = "VALIDATED"
            };
            return;
        }

        if (vendorRequest.ToolConfig.FunctionConfig?.Mode is "AUTO" or null)
        {
            vendorRequest.ToolConfig.FunctionConfig ??= new VendorGoogleChatToolConfigFunctionConfig();
            vendorRequest.ToolConfig.FunctionConfig.Mode = "VALIDATED";
        }
    }

    static string HarmFilter(GoogleSafetyFilterTypes? val)
    {
        return val switch
        {
            GoogleSafetyFilterTypes.BlockNone => "BLOCK_NONE",
            GoogleSafetyFilterTypes.BlockFew => "BLOCK_ONLY_HIGH",
            GoogleSafetyFilterTypes.BlockSome => "BLOCK_MEDIUM_AND_ABOVE",
            GoogleSafetyFilterTypes.BlockMost => "BLOCK_LOW_AND_ABOVE",
            GoogleSafetyFilterTypes.Default => "HARM_BLOCK_THRESHOLD_UNSPECIFIED",
            _ => "BLOCK_NONE"
        };
    }
 }
