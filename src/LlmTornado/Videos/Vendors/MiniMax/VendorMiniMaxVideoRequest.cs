using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using LlmTornado.Videos;
using LlmTornado.Videos.Models;
using LlmTornado.Videos.Models.MiniMax;
using Newtonsoft.Json;

namespace LlmTornado.Videos.Vendors.MiniMax;

/// <summary>
/// MiniMax video generation request DTO.
/// </summary>
internal class VendorMiniMaxVideoGenerationRequest
{
    /// <summary>
    /// Model name.
    /// </summary>
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;
    
    /// <summary>
    /// Text description of the video, up to 2000 characters.
    /// </summary>
    [JsonProperty("prompt", NullValueHandling = NullValueHandling.Ignore)]
    public string? Prompt { get; set; }
    
    /// <summary>
    /// Image as the starting frame of the video. URL or base64-encoded data URL.
    /// </summary>
    [JsonProperty("first_frame_image", NullValueHandling = NullValueHandling.Ignore)]
    public string? FirstFrameImage { get; set; }
    
    /// <summary>
    /// Image as the ending frame of the video. URL or base64-encoded data URL.
    /// </summary>
    [JsonProperty("last_frame_image", NullValueHandling = NullValueHandling.Ignore)]
    public string? LastFrameImage { get; set; }
    
    /// <summary>
    /// Whether to automatically optimize the prompt. Defaults to true.
    /// </summary>
    [JsonProperty("prompt_optimizer", NullValueHandling = NullValueHandling.Ignore)]
    public bool? PromptOptimizer { get; set; }
    
    /// <summary>
    /// Reduces optimization time when prompt_optimizer is enabled.
    /// </summary>
    [JsonProperty("fast_pretreatment", NullValueHandling = NullValueHandling.Ignore)]
    public bool? FastPretreatment { get; set; }
    
    /// <summary>
    /// Video length in seconds. Default is 6.
    /// </summary>
    [JsonProperty("duration", NullValueHandling = NullValueHandling.Ignore)]
    public int? Duration { get; set; }
    
    /// <summary>
    /// Video resolution: 512P, 720P, 768P, or 1080P.
    /// </summary>
    [JsonProperty("resolution", NullValueHandling = NullValueHandling.Ignore)]
    public string? Resolution { get; set; }
    
    /// <summary>
    /// Callback URL for asynchronous task status updates.
    /// </summary>
    [JsonProperty("callback_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? CallbackUrl { get; set; }
    
    /// <summary>
    /// Creates a MiniMax video generation request from a generic VideoGenerationRequest.
    /// </summary>
    public static VendorMiniMaxVideoGenerationRequest FromRequest(VideoGenerationRequest request)
    {
        VendorMiniMaxVideoGenerationRequest miniMaxRequest = new VendorMiniMaxVideoGenerationRequest
        {
            Model = request.Model?.Name ?? VideoModelMiniMaxHailuo.ModelHailuo23.Name,
            Prompt = request.Prompt
        };
        
        // Handle image input for image-to-video
        if (request.Image is not null)
        {
            miniMaxRequest.FirstFrameImage = request.Image.AsProviderImageUrl();
        }

        if (request.LastFrame is not null)
        {
            miniMaxRequest.LastFrameImage = request.LastFrame.AsProviderImageUrl();
        }
        
        // Duration
        if (request.DurationSeconds.HasValue)
        {
            miniMaxRequest.Duration = request.DurationSeconds.Value;
        }
        else if (request.Duration.HasValue && request.Duration.Value != VideoDuration.Custom)
        {
            miniMaxRequest.Duration = (int)request.Duration.Value;
        }
        
        // Resolution - map from harmonized enum if set, otherwise use MiniMax extensions
        if (request.Resolution.HasValue)
        {
            miniMaxRequest.Resolution = request.Resolution.Value switch
            {
                VideoResolution.SD => "720P",
                VideoResolution.HD => "720P",
                VideoResolution.FullHD => "1080P",
                _ => null
            };
        }
        
        // MiniMax-specific extensions
        if (request.MiniMaxExtensions is not null)
        {
            VideoMiniMaxExtensions ext = request.MiniMaxExtensions;
            
            if (ext.Resolution.HasValue)
            {
                miniMaxRequest.Resolution = GetEnumMemberValue(ext.Resolution.Value);
            }
            
            if (ext.PromptOptimizer.HasValue)
            {
                miniMaxRequest.PromptOptimizer = ext.PromptOptimizer.Value;
            }
            
            if (ext.FastPretreatment.HasValue)
            {
                miniMaxRequest.FastPretreatment = ext.FastPretreatment.Value;
            }
            
            if (!string.IsNullOrEmpty(ext.CallbackUrl))
            {
                miniMaxRequest.CallbackUrl = ext.CallbackUrl;
            }
        }
        
        return miniMaxRequest;
    }
    
    private static string? GetEnumMemberValue<T>(T enumValue) where T : Enum
    {
        FieldInfo? memberInfo = typeof(T).GetField(enumValue.ToString());
        object[]? attributes = memberInfo?.GetCustomAttributes(typeof(EnumMemberAttribute), false);
        
        if (attributes?.Length > 0 && attributes[0] is EnumMemberAttribute enumMemberAttr)
        {
            return enumMemberAttr.Value;
        }
        
        return enumValue.ToString();
    }
}

/// <summary>
/// Response from MiniMax video generation create request.
/// </summary>
internal class VendorMiniMaxVideoCreateResponse
{
    /// <summary>
    /// The video generation task ID.
    /// </summary>
    [JsonProperty("task_id")]
    public string TaskId { get; set; } = string.Empty;
    
    /// <summary>
    /// Error status code and details.
    /// </summary>
    [JsonProperty("base_resp")]
    public VendorMiniMaxBaseResp? BaseResp { get; set; }
}

/// <summary>
/// Response from MiniMax video generation query request.
/// </summary>
internal class VendorMiniMaxVideoQueryResponse
{
    /// <summary>
    /// The queried task ID.
    /// </summary>
    [JsonProperty("task_id")]
    public string? TaskId { get; set; }
    
    /// <summary>
    /// Current status: Preparing, Queueing, Processing, Success, Fail.
    /// </summary>
    [JsonProperty("status")]
    public string? Status { get; set; }
    
    /// <summary>
    /// File ID of the generated video (returned on success).
    /// </summary>
    [JsonProperty("file_id")]
    public string? FileId { get; set; }
    
    /// <summary>
    /// Width of the generated video in pixels (returned on success).
    /// </summary>
    [JsonProperty("video_width")]
    public int? VideoWidth { get; set; }
    
    /// <summary>
    /// Height of the generated video in pixels (returned on success).
    /// </summary>
    [JsonProperty("video_height")]
    public int? VideoHeight { get; set; }
    
    /// <summary>
    /// Error status code and details.
    /// </summary>
    [JsonProperty("base_resp")]
    public VendorMiniMaxBaseResp? BaseResp { get; set; }
}

/// <summary>
/// MiniMax base response containing status code and message.
/// </summary>
internal class VendorMiniMaxBaseResp
{
    /// <summary>
    /// Status code. 0 means success.
    /// </summary>
    [JsonProperty("status_code")]
    public int StatusCode { get; set; }
    
    /// <summary>
    /// Status message details.
    /// </summary>
    [JsonProperty("status_msg")]
    public string? StatusMsg { get; set; }
}

/// <summary>
/// Response from MiniMax file retrieve endpoint (GET /v1/files/retrieve).
/// </summary>
internal class VendorMiniMaxFileRetrieveResponse
{
    /// <summary>
    /// The file object containing download URL and metadata.
    /// </summary>
    [JsonProperty("file")]
    public VendorMiniMaxFileObject? File { get; set; }
    
    /// <summary>
    /// Error status code and details.
    /// </summary>
    [JsonProperty("base_resp")]
    public VendorMiniMaxBaseResp? BaseResp { get; set; }
}

/// <summary>
/// MiniMax file object returned by the file retrieve endpoint.
/// </summary>
internal class VendorMiniMaxFileObject
{
    /// <summary>
    /// The unique identifier for the file.
    /// </summary>
    [JsonProperty("file_id")]
    public long FileId { get; set; }
    
    /// <summary>
    /// The size of the file in bytes.
    /// </summary>
    [JsonProperty("bytes")]
    public long Bytes { get; set; }
    
    /// <summary>
    /// Unix timestamp (seconds) when the file was created.
    /// </summary>
    [JsonProperty("created_at")]
    public long CreatedAt { get; set; }
    
    /// <summary>
    /// The name of the file.
    /// </summary>
    [JsonProperty("filename")]
    public string? Filename { get; set; }
    
    /// <summary>
    /// The purpose of the file (e.g. "video_generation").
    /// </summary>
    [JsonProperty("purpose")]
    public string? Purpose { get; set; }
    
    /// <summary>
    /// The URL for downloading the file. Valid for 1 hour.
    /// </summary>
    [JsonProperty("download_url")]
    public string? DownloadUrl { get; set; }
}

/// <summary>
/// MiniMax H3 V2 video generation request DTO.
/// </summary>
internal class VendorMiniMaxVideoGenerationV2Request
{
    [JsonProperty("model")]
    public string Model { get; set; } = VideoModelMiniMaxH3.ModelH3.Name;
    
    [JsonProperty("content")]
    public List<VendorMiniMaxVideoContentItem> Content { get; set; } = [];
    
    [JsonProperty("resolution")]
    public string Resolution { get; set; } = "768P";
    
    [JsonProperty("duration")]
    public int Duration { get; set; } = 5;
    
    [JsonProperty("ratio", NullValueHandling = NullValueHandling.Ignore)]
    public string? Ratio { get; set; }
    
    [JsonProperty("extra", NullValueHandling = NullValueHandling.Ignore)]
    public VendorMiniMaxVideoV2Extra? Extra { get; set; }
    
    [JsonProperty("callback_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? CallbackUrl { get; set; }

    public static VendorMiniMaxVideoGenerationV2Request FromRequest(VideoGenerationRequest request)
    {
        VideoMiniMaxExtensions? ext = request.MiniMaxExtensions;
        bool hasReferenceMedia = (ext?.ReferenceAudios?.Count ?? 0) > 0
                                 || (ext?.ReferenceVideos?.Count ?? 0) > 0
                                 || (request.ReferenceImages?.Count ?? 0) > 0
                                 || request.Video is not null;
        
        VendorMiniMaxVideoGenerationV2Request v2 = new VendorMiniMaxVideoGenerationV2Request
        {
            Model = request.Model?.Name ?? VideoModelMiniMaxH3.ModelH3.Name,
            CallbackUrl = ext?.CallbackUrl
        };

        v2.Content.Add(new VendorMiniMaxVideoContentItem
        {
            Type = "text",
            Text = request.Prompt ?? string.Empty
        });

        if (hasReferenceMedia)
        {
            if (request.ReferenceImages is not null)
            {
                foreach (VideoReferenceImage reference in request.ReferenceImages)
                {
                    if (string.IsNullOrEmpty(reference.Image?.Url))
                    {
                        continue;
                    }

                    v2.Content.Add(new VendorMiniMaxVideoContentItem
                    {
                        Type = "image_url",
                        ImageUrl = new VendorMiniMaxVideoMediaUrl { Url = reference.Image.AsProviderImageUrl() },
                        Role = "reference_image"
                    });
                }
            }

            if (request.Video is not null && !string.IsNullOrEmpty(request.Video.Url))
            {
                v2.Content.Add(new VendorMiniMaxVideoContentItem
                {
                    Type = "video_url",
                    VideoUrl = new VendorMiniMaxVideoMediaUrl { Url = request.Video.Url },
                    Role = "reference_video"
                });
            }

            if (ext?.ReferenceVideos is not null)
            {
                foreach (string url in ext.ReferenceVideos)
                {
                    v2.Content.Add(new VendorMiniMaxVideoContentItem
                    {
                        Type = "video_url",
                        VideoUrl = new VendorMiniMaxVideoMediaUrl { Url = url },
                        Role = "reference_video"
                    });
                }
            }

            if (ext?.ReferenceAudios is not null)
            {
                foreach (string url in ext.ReferenceAudios)
                {
                    v2.Content.Add(new VendorMiniMaxVideoContentItem
                    {
                        Type = "audio_url",
                        AudioUrl = new VendorMiniMaxVideoMediaUrl { Url = url },
                        Role = "reference_audio"
                    });
                }
            }
        }
        else
        {
            if (request.Image is not null && !string.IsNullOrEmpty(request.Image.Url))
            {
                v2.Content.Add(new VendorMiniMaxVideoContentItem
                {
                    Type = "image_url",
                    ImageUrl = new VendorMiniMaxVideoMediaUrl { Url = request.Image.AsProviderImageUrl() },
                    Role = "first_frame"
                });
            }

            if (request.LastFrame is not null && !string.IsNullOrEmpty(request.LastFrame.Url))
            {
                v2.Content.Add(new VendorMiniMaxVideoContentItem
                {
                    Type = "image_url",
                    ImageUrl = new VendorMiniMaxVideoMediaUrl { Url = request.LastFrame.AsProviderImageUrl() },
                    Role = "last_frame"
                });
            }
        }

        if (request.DurationSeconds.HasValue)
        {
            v2.Duration = request.DurationSeconds.Value;
        }
        else if (request.Duration.HasValue && request.Duration.Value != VideoDuration.Custom)
        {
            v2.Duration = (int)request.Duration.Value;
        }

        if (ext?.Resolution is not null)
        {
            v2.Resolution = GetEnumMemberValue(ext.Resolution.Value) ?? "768P";
        }
        else if (request.Resolution.HasValue)
        {
            v2.Resolution = request.Resolution.Value switch
            {
                VideoResolution.SD => "480P",
                VideoResolution.HD => "768P",
                VideoResolution.FullHD => "768P",
                VideoResolution.UltraHD4K => "2K",
                VideoResolution.UHD2K => "2K",
                _ => "768P"
            };
        }

        bool textOnly = v2.Content.TrueForAll(item => item.Type == "text");
        if (ext?.AspectRatio is not null)
        {
            v2.Ratio = GetEnumMemberValue(ext.AspectRatio.Value);
        }
        else if (request.AspectRatio.HasValue)
        {
            v2.Ratio = request.AspectRatio.Value switch
            {
                VideoAspectRatio.Widescreen => "16:9",
                VideoAspectRatio.Portrait => "9:16",
                VideoAspectRatio.Square => "1:1",
                VideoAspectRatio.Standard => "4:3",
                VideoAspectRatio.StandardPortrait => "3:4",
                VideoAspectRatio.Cinema => "21:9",
                _ => textOnly ? "16:9" : "adaptive"
            };
        }
        else if (textOnly)
        {
            v2.Ratio = "16:9";
        }

        if (ext?.PromptExpansionMode is not null)
        {
            v2.Extra = new VendorMiniMaxVideoV2Extra
            {
                PromptExpansionMode = GetEnumMemberValue(ext.PromptExpansionMode.Value)
            };
        }

        return v2;
    }

    private static string? GetEnumMemberValue<T>(T enumValue) where T : Enum
    {
        FieldInfo? memberInfo = typeof(T).GetField(enumValue.ToString());
        object[]? attributes = memberInfo?.GetCustomAttributes(typeof(EnumMemberAttribute), false);
        
        if (attributes?.Length > 0 && attributes[0] is EnumMemberAttribute enumMemberAttr)
        {
            return enumMemberAttr.Value;
        }
        
        return enumValue.ToString();
    }
}

internal class VendorMiniMaxVideoContentItem
{
    [JsonProperty("type")]
    public string Type { get; set; } = "text";
    
    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public string? Text { get; set; }
    
    [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
    public VendorMiniMaxVideoMediaUrl? ImageUrl { get; set; }
    
    [JsonProperty("video_url", NullValueHandling = NullValueHandling.Ignore)]
    public VendorMiniMaxVideoMediaUrl? VideoUrl { get; set; }
    
    [JsonProperty("audio_url", NullValueHandling = NullValueHandling.Ignore)]
    public VendorMiniMaxVideoMediaUrl? AudioUrl { get; set; }
    
    [JsonProperty("role", NullValueHandling = NullValueHandling.Ignore)]
    public string? Role { get; set; }
}

internal class VendorMiniMaxVideoMediaUrl
{
    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;
}

internal class VendorMiniMaxVideoV2Extra
{
    [JsonProperty("prompt_expansion_mode", NullValueHandling = NullValueHandling.Ignore)]
    public string? PromptExpansionMode { get; set; }
}

internal class VendorMiniMaxVideoV2QueryResponse
{
    [JsonProperty("task")]
    public VendorMiniMaxVideoV2Task? Task { get; set; }
    
    [JsonProperty("task_id")]
    public string? TaskId { get; set; }
    
    [JsonProperty("error")]
    public VendorMiniMaxVideoV2Error? Error { get; set; }
}

internal class VendorMiniMaxVideoV2Task
{
    [JsonProperty("id")]
    public string? Id { get; set; }
    
    [JsonProperty("model")]
    public string? Model { get; set; }
    
    [JsonProperty("status")]
    public string? Status { get; set; }
    
    [JsonProperty("error")]
    public VendorMiniMaxVideoV2Error? Error { get; set; }
    
    [JsonProperty("content")]
    public VendorMiniMaxVideoV2Content? Content { get; set; }
    
    [JsonProperty("resolution")]
    public string? Resolution { get; set; }
    
    [JsonProperty("duration")]
    public int? Duration { get; set; }
    
    [JsonProperty("ratio")]
    public string? Ratio { get; set; }
}

internal class VendorMiniMaxVideoV2Content
{
    [JsonProperty("url")]
    public string? Url { get; set; }
    
    [JsonProperty("prompt")]
    public string? Prompt { get; set; }
}

internal class VendorMiniMaxVideoV2Error
{
    [JsonProperty("code")]
    public string? Code { get; set; }
    
    [JsonProperty("message")]
    public string? Message { get; set; }
}
