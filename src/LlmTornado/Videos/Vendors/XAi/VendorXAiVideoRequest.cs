using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using LlmTornado.Code;
using LlmTornado.Images;
using LlmTornado.Videos.Models;
using Newtonsoft.Json;

namespace LlmTornado.Videos.Vendors.XAi;

/// <summary>
/// xAI video generation request.
/// </summary>
internal class VendorXAiVideoGenerationRequest
{
    /// <summary>
    /// Prompt for video generation.
    /// </summary>
    [JsonProperty("prompt")]
    public string Prompt { get; set; } = string.Empty;
    
    /// <summary>
    /// Aspect ratio of the generated video.
    /// </summary>
    [JsonProperty("aspect_ratio", NullValueHandling = NullValueHandling.Ignore)]
    public string? AspectRatio { get; set; }
    
    /// <summary>
    /// Video duration in seconds. Range: [1, 15]. Default: 6.
    /// </summary>
    [JsonProperty("duration", NullValueHandling = NullValueHandling.Ignore)]
    public int? Duration { get; set; }
    
    /// <summary>
    /// Optional input image for image-to-video generation.
    /// </summary>
    [JsonProperty("image", NullValueHandling = NullValueHandling.Ignore)]
    public VendorXAiVideoImage? Image { get; set; }
    
    /// <summary>
    /// Reference images that influence the video without forcing the first frame.
    /// </summary>
    [JsonProperty("reference_images", NullValueHandling = NullValueHandling.Ignore)]
    public List<VendorXAiVideoImage>? ReferenceImages { get; set; }
    
    /// <summary>
    /// Files API IDs for reference-to-video.
    /// </summary>
    [JsonProperty("reference_image_file_ids", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? ReferenceImageFileIds { get; set; }
    
    /// <summary>
    /// Optional preset voice for reference-to-video.
    /// </summary>
    [JsonProperty("voice", NullValueHandling = NullValueHandling.Ignore)]
    public string? Voice { get; set; }
    
    /// <summary>
    /// Model to be used.
    /// </summary>
    [JsonProperty("model", NullValueHandling = NullValueHandling.Ignore)]
    public string? Model { get; set; }
    
    /// <summary>
    /// Optional output destination for generated video.
    /// </summary>
    [JsonProperty("output", NullValueHandling = NullValueHandling.Ignore)]
    public VendorXAiVideoOutput? Output { get; set; }
    
    /// <summary>
    /// Resolution of the generated video.
    /// </summary>
    [JsonProperty("resolution", NullValueHandling = NullValueHandling.Ignore)]
    public string? Resolution { get; set; }
    
    /// <summary>
    /// A unique identifier representing your end-user.
    /// </summary>
    [JsonProperty("user", NullValueHandling = NullValueHandling.Ignore)]
    public string? User { get; set; }
    
    /// <summary>
    /// Creates an xAI video generation request from a generic VideoGenerationRequest.
    /// </summary>
    public static VendorXAiVideoGenerationRequest FromRequest(VideoGenerationRequest request)
    {
        VendorXAiVideoGenerationRequest xaiRequest = new VendorXAiVideoGenerationRequest
        {
            Prompt = request.Prompt ?? string.Empty,
            Model = request.Model?.Name
        };
        
        // Duration
        if (request.DurationSeconds.HasValue)
        {
            xaiRequest.Duration = request.DurationSeconds.Value;
        }
        else if (request.Duration.HasValue && request.Duration.Value != VideoDuration.Custom)
        {
            xaiRequest.Duration = (int)request.Duration.Value;
        }
        
        // Aspect ratio
        if (request.AspectRatio.HasValue)
        {
            xaiRequest.AspectRatio = GetEnumMemberValue(request.AspectRatio.Value);
        }
        
        // Resolution
        if (request.Resolution.HasValue)
        {
            xaiRequest.Resolution = GetEnumMemberValue(request.Resolution.Value);
        }
        
        if (request.Image is not null)
        {
            xaiRequest.Image = new VendorXAiVideoImage
            {
                Url = request.Image.AsProviderImageUrl()
            };
        }

        if (request.ReferenceImages is { Count: > 0 })
        {
            xaiRequest.ReferenceImages = request.ReferenceImages
                .Where(x => x.Image is not null)
                .Select(x => new VendorXAiVideoImage { Url = x.Image.AsProviderImageUrl() })
                .ToList();
        }
        
        if (request.XAiExtensions is not null)
        {
            if (!string.IsNullOrEmpty(request.XAiExtensions.User))
            {
                xaiRequest.User = request.XAiExtensions.User;
            }
            
            if (!string.IsNullOrEmpty(request.XAiExtensions.UploadUrl))
            {
                xaiRequest.Output = new VendorXAiVideoOutput
                {
                    UploadUrl = request.XAiExtensions.UploadUrl
                };
            }
            
            if (request.XAiExtensions.ImageDetail is not null)
            {
                xaiRequest.Image ??= new VendorXAiVideoImage();
                xaiRequest.Image.Detail = request.XAiExtensions.ImageDetail.ToString()?.ToLowerInvariant();
            }

            if (request.XAiExtensions.ReferenceImageFileIds is { Count: > 0 })
            {
                xaiRequest.ReferenceImageFileIds = request.XAiExtensions.ReferenceImageFileIds;
            }

            if (!string.IsNullOrEmpty(request.XAiExtensions.Voice))
            {
                xaiRequest.Voice = request.XAiExtensions.Voice;
            }
        }
        
        return xaiRequest;
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
/// xAI video edit request.
/// </summary>
internal class VendorXAiVideoEditRequest
{
    /// <summary>
    /// Prompt for video editing.
    /// </summary>
    [JsonProperty("prompt")]
    public string Prompt { get; set; } = string.Empty;
    
    /// <summary>
    /// Input video to perform edit on.
    /// </summary>
    [JsonProperty("video")]
    public VendorXAiVideoInputVideo Video { get; set; } = new VendorXAiVideoInputVideo();
    
    /// <summary>
    /// Model to be used.
    /// </summary>
    [JsonProperty("model", NullValueHandling = NullValueHandling.Ignore)]
    public string? Model { get; set; }
    
    /// <summary>
    /// Optional output destination for generated video.
    /// </summary>
    [JsonProperty("output", NullValueHandling = NullValueHandling.Ignore)]
    public VendorXAiVideoOutput? Output { get; set; }
    
    /// <summary>
    /// A unique identifier representing your end-user.
    /// </summary>
    [JsonProperty("user", NullValueHandling = NullValueHandling.Ignore)]
    public string? User { get; set; }
}

/// <summary>
/// Image input for xAI video generation.
/// </summary>
internal class VendorXAiVideoImage
{
    /// <summary>
    /// URL of the image.
    /// </summary>
    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// Specifies the detail level of the image.
    /// </summary>
    [JsonProperty("detail", NullValueHandling = NullValueHandling.Ignore)]
    public string? Detail { get; set; }
}

/// <summary>
/// Video input for xAI video editing.
/// </summary>
internal class VendorXAiVideoInputVideo
{
    /// <summary>
    /// URL of the video (public URL or base64-encoded data URL).
    /// </summary>
    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Output destination for xAI video generation.
/// </summary>
internal class VendorXAiVideoOutput
{
    /// <summary>
    /// Signed URL to upload the generated video via HTTP PUT.
    /// </summary>
    [JsonProperty("upload_url")]
    public string UploadUrl { get; set; } = string.Empty;
}

/// <summary>
/// Response from xAI video generation/edit request.
/// </summary>
internal class VendorXAiVideoCreateResponse
{
    /// <summary>
    /// A unique request ID to poll for the result.
    /// </summary>
    [JsonProperty("request_id")]
    public string RequestId { get; set; } = string.Empty;
}

/// <summary>
/// Response from xAI video status request.
/// Handles both wrapper format (with status/response) and direct format (video/model at top level).
/// </summary>
internal class VendorXAiVideoStatusResponse
{
    /// <summary>
    /// Status of the deferred request: "pending" or "done".
    /// May be null when the response contains the video directly.
    /// </summary>
    [JsonProperty("status")]
    public string? Status { get; set; }
    
    /// <summary>
    /// The generated video response. Only present if status is "done" (wrapped format).
    /// </summary>
    [JsonProperty("response")]
    public VendorXAiVideoResultResponse? Response { get; set; }
    
    // Direct format fields (when response is returned without wrapper)
    
    /// <summary>
    /// Video data when returned directly at top level.
    /// </summary>
    [JsonProperty("video")]
    public VendorXAiVideoResultData? Video { get; set; }
    
    /// <summary>
    /// Model name when returned directly at top level.
    /// </summary>
    [JsonProperty("model")]
    public string? Model { get; set; }
    
    /// <summary>
    /// Gets the effective video result, checking both wrapped and direct formats.
    /// </summary>
    public VendorXAiVideoResultData? GetVideo() => Response?.Video ?? Video;
    
    /// <summary>
    /// Gets the effective model name, checking both wrapped and direct formats.
    /// </summary>
    public string? GetModel() => Response?.Model ?? Model;
    
    /// <summary>
    /// Determines if the response indicates completion.
    /// </summary>
    public bool IsCompleted => Status?.ToLowerInvariant() == "done" || Video is not null;
    
    /// <summary>
    /// Determines if the response indicates pending/queued state.
    /// </summary>
    public bool IsPending => Status?.ToLowerInvariant() == "pending";
}

/// <summary>
/// The generated video response data.
/// </summary>
internal class VendorXAiVideoResultResponse
{
    /// <summary>
    /// The model used to generate the video.
    /// </summary>
    [JsonProperty("model")]
    public string? Model { get; set; }
    
    /// <summary>
    /// The generated video data.
    /// </summary>
    [JsonProperty("video")]
    public VendorXAiVideoResultData? Video { get; set; }
}

/// <summary>
/// The generated video data.
/// </summary>
internal class VendorXAiVideoResultData
{
    /// <summary>
    /// Duration of the generated video in seconds.
    /// </summary>
    [JsonProperty("duration")]
    public int Duration { get; set; }
    
    /// <summary>
    /// Whether the video generated by the model respects moderation rules.
    /// </summary>
    [JsonProperty("respect_moderation")]
    public bool RespectModeration { get; set; }
    
    /// <summary>
    /// A url to the generated video.
    /// </summary>
    [JsonProperty("url")]
    public string? Url { get; set; }
}
