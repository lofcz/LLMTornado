using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace LlmTornado.Videos;

/// <summary>
/// Represents an image for video generation (initial frame, last frame, etc.)
/// </summary>
public class VideoImage
{
    /// <summary>
    /// Creates a new video image
    /// </summary>
    /// <param name="content">Publicly available URL to the image or base64 encoded content</param>
    public VideoImage(string content)
    {
        Url = content;
    }

    /// <summary>
    /// Creates a new video image with MIME type
    /// </summary>
    /// <param name="content">Publicly available URL to the image or base64 encoded content</param>
    /// <param name="mimeType">MIME type of the image (e.g., "image/png", "image/jpeg")</param>
    public VideoImage(string content, string mimeType)
    {
        Url = content;
        MimeType = mimeType;
    }

    /// <summary>
    /// The image URL or base64 encoded content
    /// </summary>
    [JsonProperty("bytesBase64Encoded")]
    public string Url { get; set; }

    /// <summary>
    /// The MIME type of the image
    /// </summary>
    [JsonProperty("mimeType")]
    public string? MimeType { get; set; }

    /// <summary>
    /// Image reference for providers that accept a public URL or a data URI.
    /// HTTP(S) URLs and existing data URIs pass through. Raw base64 is wrapped as
    /// <c>data:&lt;mime&gt;;base64,&lt;content&gt;</c> when <see cref="MimeType"/> is set.
    /// </summary>
    internal string AsProviderImageUrl()
    {
        if (string.IsNullOrEmpty(Url) || string.IsNullOrWhiteSpace(MimeType))
        {
            return Url;
        }

        if (Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Url;
        }

        return $"data:{MimeType};base64,{Url}";
    }
    
    /// <summary>
    /// Implicit conversion from string to VideoImage
    /// </summary>
    public static implicit operator VideoImage(string content) => new VideoImage(content);
}

/// <summary>
/// Represents a reference image for video generation with type specification
/// </summary>
public class VideoReferenceImage
{
    /// <summary>
    /// Creates a new reference image
    /// </summary>
    /// <param name="image">The image to use as reference</param>
    /// <param name="referenceType">The type of reference (asset, style, etc.)</param>
    public VideoReferenceImage(VideoImage image, VideoReferenceType referenceType = VideoReferenceType.Asset)
    {
        Image = image;
        ReferenceType = referenceType;
    }

    /// <summary>
    /// The reference image
    /// </summary>
    [JsonProperty("image")]
    public VideoImage Image { get; set; }

    /// <summary>
    /// The type of reference
    /// </summary>
    [JsonProperty("referenceType")]
    public VideoReferenceType ReferenceType { get; set; }
}

/// <summary>
/// Represents a video for video extension
/// </summary>
public class VideoInput
{
    /// <summary>
    /// Creates a new video input
    /// </summary>
    /// <param name="content">Publicly available URL to the video or base64 encoded content</param>
    public VideoInput(string content)
    {
        Url = content;
    }

    /// <summary>
    /// Creates a new video input with MIME type
    /// </summary>
    /// <param name="content">Publicly available URL to the video or base64 encoded content</param>
    /// <param name="mimeType">MIME type of the video (e.g., "video/mp4")</param>
    public VideoInput(string content, string mimeType)
    {
        Url = content;
        MimeType = mimeType;
    }

    /// <summary>
    /// The video URL or base64 encoded content
    /// </summary>
    [JsonProperty("url")]
    public string Url { get; set; }

    /// <summary>
    /// The MIME type of the video
    /// </summary>
    [JsonProperty("mimeType", NullValueHandling = NullValueHandling.Ignore)]
    public string? MimeType { get; set; }
    
    /// <summary>
    /// Implicit conversion from string to VideoInput
    /// </summary>
    public static implicit operator VideoInput(string content) => new VideoInput(content);
}

/// <summary>
/// Video aspect ratio options
/// </summary>
public enum VideoAspectRatio
{
    /// <summary>
    /// 1:1 aspect ratio (square). Supported by xAI.
    /// </summary>
    [EnumMember(Value = "1:1")]
    Square,
    
    /// <summary>
    /// 16:9 aspect ratio (widescreen). Supported by Google Veo, xAI.
    /// </summary>
    [EnumMember(Value = "16:9")]
    Widescreen,

    /// <summary>
    /// 9:16 aspect ratio (portrait). Supported by Google Veo, xAI.
    /// </summary>
    [EnumMember(Value = "9:16")]
    Portrait,
    
    /// <summary>
    /// 4:3 aspect ratio (standard). Supported by xAI.
    /// </summary>
    [EnumMember(Value = "4:3")]
    Standard,
    
    /// <summary>
    /// 3:4 aspect ratio (standard portrait). Supported by xAI.
    /// </summary>
    [EnumMember(Value = "3:4")]
    StandardPortrait,
    
    /// <summary>
    /// 3:2 aspect ratio (classic). Supported by xAI.
    /// </summary>
    [EnumMember(Value = "3:2")]
    Classic,
    
    /// <summary>
    /// 2:3 aspect ratio (classic portrait). Supported by xAI.
    /// </summary>
    [EnumMember(Value = "2:3")]
    ClassicPortrait,
    
    /// <summary>
    /// 21:9 cinematic widescreen. Supported by MiniMax-H3.
    /// </summary>
    [EnumMember(Value = "21:9")]
    Cinema
}

/// <summary>
/// Video resolution options
/// </summary>
public enum VideoResolution
{
    /// <summary>
    /// 360p resolution. Supported by Gemini Omni Flash.
    /// </summary>
    [EnumMember(Value = "360p")]
    NHD,
    
    /// <summary>
    /// 480p resolution (854x480). Supported by xAI.
    /// </summary>
    [EnumMember(Value = "480p")]
    SD,
    
    /// <summary>
    /// 720p resolution (1280x720). Supported by Google Veo, xAI.
    /// </summary>
    [EnumMember(Value = "720p")]
    HD,

    /// <summary>
    /// 1080p resolution (1920x1080). Supported by Google Veo (only 8s duration for Veo 3.1), sora-2-pro, and xAI grok-imagine-video-1.5 (T2V / I2V).
    /// </summary>
    [EnumMember(Value = "1080p")]
    FullHD,

    /// <summary>
    /// 4k resolution. Supported by Google Veo 3.1 and Veo 3 (8s duration only). Not available for Veo 3.1 Lite.
    /// </summary>
    [EnumMember(Value = "4k")]
    UltraHD4K,
    
    /// <summary>
    /// 2K resolution. Supported by MiniMax-H3.
    /// </summary>
    [EnumMember(Value = "2K")]
    UHD2K
}

/// <summary>
/// Person generation control options
/// </summary>
public enum VideoPersonGeneration
{
    /// <summary>
    /// Allow generation of all people (text-to-video and extension)
    /// </summary>
    [EnumMember(Value = "allow_all")]
    AllowAll,

    /// <summary>
    /// Allow generation of adults only (image-to-video, interpolation, and reference images)
    /// </summary>
    [EnumMember(Value = "allow_adult")]
    AllowAdult,

    /// <summary>
    /// Don't allow generation of people (Veo 2 only)
    /// </summary>
    [EnumMember(Value = "dont_allow")]
    DontAllow
}

/// <summary>
/// Reference image type for Veo 3.1
/// </summary>
public enum VideoReferenceType
{
    /// <summary>
    /// Asset reference - preserves subject appearance
    /// </summary>
    [EnumMember(Value = "asset")]
    Asset,

    /// <summary>
    /// Style reference - guides visual style
    /// </summary>
    [EnumMember(Value = "style")]
    Style
}

/// <summary>
/// Video duration options for different Veo models
/// </summary>
public enum VideoDuration
{
    /// <summary>
    /// 4 seconds - Supported by Veo 3.1, Veo 3.1 Fast, Veo 3.1 Lite, Veo 3, Veo 3 Fast
    /// </summary>
    Seconds4 = 4,

    /// <summary>
    /// 5 seconds - Supported by Veo 2
    /// </summary>
    Seconds5 = 5,

    /// <summary>
    /// 6 seconds - Supported by Veo 3.1, Veo 3.1 Fast, Veo 3.1 Lite, Veo 3, Veo 3 Fast, Veo 2
    /// </summary>
    Seconds6 = 6,

    /// <summary>
    /// 8 seconds - Supported by Veo 3.1, Veo 3.1 Fast, Veo 3.1 Lite, Veo 3, Veo 3 Fast, Veo 2
    /// Required when using extension, interpolation, reference images, 1080p, or 4k resolution
    /// </summary>
    Seconds8 = 8,
    
    /// <summary>
    /// 10 seconds - Supported by Z.AI CogVideoX
    /// </summary>
    Seconds10 = 10,
    
    /// <summary>
    /// 12 seconds - Supported by sora-2 and sora-2-pro
    /// </summary>
    Seconds12 = 12,

    /// <summary>
    /// 16 seconds - Supported by sora-2 and sora-2-pro
    /// </summary>
    Seconds16 = 16,

    /// <summary>
    /// 20 seconds - Supported by sora-2 and sora-2-pro
    /// </summary>
    Seconds20 = 20,

    /// <summary>
    /// Custom duration - Use DurationSecondsCustom property to specify
    /// </summary>
    Custom = 0
}

