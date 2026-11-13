using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using LlmTornado.Videos.Models;
using LlmTornado.Videos.Models.Zai;
using Newtonsoft.Json;

namespace LlmTornado.Videos.Vendors.Zai;

/// <summary>
/// Z.AI video generation request - handles both CogVideoX and Vidu models.
/// </summary>
internal class VendorZaiVideoGenerationRequest
{
    /// <summary>
    /// The model code to be called.
    /// </summary>
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;
    
    /// <summary>
    /// Text description of the video, maximum 512 characters.
    /// </summary>
    [JsonProperty("prompt", NullValueHandling = NullValueHandling.Ignore)]
    public string? Prompt { get; set; }
    
    /// <summary>
    /// Image URL(s) for image-to-video or first/last frame generation.
    /// Can be a single URL/base64 string or an array for first/last frame mode.
    /// </summary>
    [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
    public object? ImageUrl { get; set; }
    
    /// <summary>
    /// Output mode for CogVideoX: "speed" or "quality".
    /// </summary>
    [JsonProperty("quality", NullValueHandling = NullValueHandling.Ignore)]
    public string? Quality { get; set; }
    
    /// <summary>
    /// Whether to generate AI sound effects/background music.
    /// </summary>
    [JsonProperty("with_audio", NullValueHandling = NullValueHandling.Ignore)]
    public bool? WithAudio { get; set; }
    
    /// <summary>
    /// Video resolution. CogVideoX uses specific strings like "1920x1080".
    /// </summary>
    [JsonProperty("size", NullValueHandling = NullValueHandling.Ignore)]
    public string? Size { get; set; }
    
    /// <summary>
    /// Video frame rate (FPS) for CogVideoX. 30 or 60.
    /// </summary>
    [JsonProperty("fps", NullValueHandling = NullValueHandling.Ignore)]
    public int? Fps { get; set; }
    
    /// <summary>
    /// Video duration in seconds.
    /// </summary>
    [JsonProperty("duration", NullValueHandling = NullValueHandling.Ignore)]
    public int? Duration { get; set; }
    
    /// <summary>
    /// Style for Vidu text-to-video: "general" or "anime".
    /// </summary>
    [JsonProperty("style", NullValueHandling = NullValueHandling.Ignore)]
    public string? Style { get; set; }
    
    /// <summary>
    /// Aspect ratio for Vidu: "16:9", "9:16", "1:1".
    /// </summary>
    [JsonProperty("aspect_ratio", NullValueHandling = NullValueHandling.Ignore)]
    public string? AspectRatio { get; set; }
    
    /// <summary>
    /// Motion amplitude for Vidu: "auto", "small", "medium", "large".
    /// </summary>
    [JsonProperty("movement_amplitude", NullValueHandling = NullValueHandling.Ignore)]
    public string? MovementAmplitude { get; set; }
    
    /// <summary>
    /// Unique request ID for tracking.
    /// </summary>
    [JsonProperty("request_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? RequestId { get; set; }
    
    /// <summary>
    /// End-user ID for abuse monitoring.
    /// </summary>
    [JsonProperty("user_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? UserId { get; set; }
    
    /// <summary>
    /// Creates a Z.AI video generation request from a generic VideoGenerationRequest.
    /// </summary>
    public static VendorZaiVideoGenerationRequest FromRequest(VideoGenerationRequest request)
    {
        VendorZaiVideoGenerationRequest zaiRequest = new VendorZaiVideoGenerationRequest
        {
            Model = request.Model?.Name ?? VideoModelZaiCogVideoX.ModelCogVideoX3.Name,
            Prompt = request.Prompt
        };
        
        string modelName = zaiRequest.Model;
        bool isCogVideoX = modelName.StartsWith("cogvideox", StringComparison.OrdinalIgnoreCase);
        bool isViduText = modelName.Equals("viduq1-text", StringComparison.OrdinalIgnoreCase);
        bool isViduReference = modelName.Equals("vidu2-reference", StringComparison.OrdinalIgnoreCase);
        bool isStartEnd = modelName.Contains("start-end", StringComparison.OrdinalIgnoreCase);
        
        // Handle image inputs
        if (isViduReference && request.ReferenceImages is { Count: > 0 })
        {
            // Reference mode: array of 1-3 images
            List<string> imageUrls = [];
            foreach (VideoReferenceImage refImage in request.ReferenceImages)
            {
                imageUrls.Add(refImage.Image.AsProviderImageUrl());
            }
            zaiRequest.ImageUrl = imageUrls;
        }
        else if (isStartEnd && request.Image is not null && request.LastFrame is not null)
        {
            // First/last frame mode: array of 2 images
            zaiRequest.ImageUrl = new List<string> { request.Image.AsProviderImageUrl(), request.LastFrame.AsProviderImageUrl() };
        }
        else if (isCogVideoX && request.Image is not null && request.LastFrame is not null)
        {
            // CogVideoX also supports first/last frame mode
            zaiRequest.ImageUrl = new List<string> { request.Image.AsProviderImageUrl(), request.LastFrame.AsProviderImageUrl() };
        }
        else if (request.Image is not null)
        {
            // Single image for image-to-video
            zaiRequest.ImageUrl = request.Image.AsProviderImageUrl();
        }
        
        // Duration
        if (request.DurationSeconds.HasValue)
        {
            zaiRequest.Duration = request.DurationSeconds.Value;
        }
        else if (request.Duration.HasValue && request.Duration.Value != VideoDuration.Custom)
        {
            zaiRequest.Duration = (int)request.Duration.Value;
        }
        
        // Aspect ratio (Vidu text-to-video and reference models)
        if (request.AspectRatio.HasValue && (isViduText || isViduReference))
        {
            zaiRequest.AspectRatio = GetEnumMemberValue(request.AspectRatio.Value);
        }
        
        // Resolution/size
        if (request.Resolution.HasValue)
        {
            zaiRequest.Size = ResolutionToSizeString(request.Resolution.Value, isCogVideoX);
        }
        
        // Z.AI-specific extensions
        if (request.ZaiExtensions is not null)
        {
            VideoZaiExtensions ext = request.ZaiExtensions;
            
            if (ext.Quality.HasValue && isCogVideoX)
            {
                zaiRequest.Quality = GetEnumMemberValue(ext.Quality.Value);
            }
            
            if (ext.Fps.HasValue && isCogVideoX)
            {
                zaiRequest.Fps = ext.Fps.Value;
            }
            
            if (ext.WithAudio.HasValue)
            {
                zaiRequest.WithAudio = ext.WithAudio.Value;
            }
            
            if (ext.Style.HasValue && isViduText)
            {
                zaiRequest.Style = GetEnumMemberValue(ext.Style.Value);
            }
            
            if (ext.MovementAmplitude.HasValue && !isCogVideoX)
            {
                zaiRequest.MovementAmplitude = GetEnumMemberValue(ext.MovementAmplitude.Value);
            }
            
            if (!string.IsNullOrEmpty(ext.RequestId))
            {
                zaiRequest.RequestId = ext.RequestId;
            }
            
            if (!string.IsNullOrEmpty(ext.UserId))
            {
                zaiRequest.UserId = ext.UserId;
            }
        }
        
        return zaiRequest;
    }
    
    private static string? ResolutionToSizeString(VideoResolution resolution, bool isCogVideoX)
    {
        // Map common resolution enum to Z.AI size strings
        return resolution switch
        {
            VideoResolution.SD => "1280x720", // 480p -> use 720 as minimum
            VideoResolution.HD => "1280x720",
            VideoResolution.FullHD => "1920x1080",
            _ => null
        };
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
/// Response from Z.AI video generation request.
/// </summary>
internal class VendorZaiVideoCreateResponse
{
    /// <summary>
    /// Model name used in this call.
    /// </summary>
    [JsonProperty("model")]
    public string? Model { get; set; }
    
    /// <summary>
    /// Task order number generated by Z.AI, used when calling the result interface.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Task number submitted by the user or generated by the platform.
    /// </summary>
    [JsonProperty("request_id")]
    public string? RequestId { get; set; }
    
    /// <summary>
    /// Processing status: PROCESSING, SUCCESS, FAIL.
    /// </summary>
    [JsonProperty("task_status")]
    public string? TaskStatus { get; set; }
}

/// <summary>
/// Response from Z.AI async result query.
/// </summary>
internal class VendorZaiVideoStatusResponse
{
    /// <summary>
    /// Model name.
    /// </summary>
    [JsonProperty("model")]
    public string? Model { get; set; }
    
    /// <summary>
    /// Processing status: PROCESSING, SUCCESS, FAIL.
    /// </summary>
    [JsonProperty("task_status")]
    public string? TaskStatus { get; set; }
    
    /// <summary>
    /// Video generation results.
    /// </summary>
    [JsonProperty("video_result")]
    public List<VendorZaiVideoResultData>? VideoResult { get; set; }
    
    /// <summary>
    /// Request ID.
    /// </summary>
    [JsonProperty("request_id")]
    public string? RequestId { get; set; }
    
    /// <summary>
    /// Determines if the response indicates completion.
    /// </summary>
    public bool IsCompleted => TaskStatus?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) == true;
    
    /// <summary>
    /// Determines if the response indicates processing/in-progress state.
    /// </summary>
    public bool IsProcessing => TaskStatus?.Equals("PROCESSING", StringComparison.OrdinalIgnoreCase) == true;
    
    /// <summary>
    /// Determines if the response indicates failure.
    /// </summary>
    public bool IsFailed => TaskStatus?.Equals("FAIL", StringComparison.OrdinalIgnoreCase) == true;
}

/// <summary>
/// Video result data from Z.AI.
/// </summary>
internal class VendorZaiVideoResultData
{
    /// <summary>
    /// Video URL.
    /// </summary>
    [JsonProperty("url")]
    public string? Url { get; set; }
    
    /// <summary>
    /// Video cover/thumbnail URL.
    /// </summary>
    [JsonProperty("cover_image_url")]
    public string? CoverImageUrl { get; set; }
}
