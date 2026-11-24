using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using LlmTornado.Videos.Models;
using Newtonsoft.Json;

namespace LlmTornado.Videos.Vendors.OpenAi;

/// <summary>
/// Handles serialization of video generation requests for OpenAI.
/// Maps harmonized VideoGenerationRequest to OpenAI's format.
/// </summary>
internal static class VendorOpenAiVideoRequest
{
    /// <summary>
    /// Serializes a harmonized video generation request to OpenAI's multipart/form-data content.
    /// </summary>
    public static MultipartFormDataContent SerializeMultipart(VideoGenerationRequest request)
    {
        MultipartFormDataContent content = new MultipartFormDataContent();
        
        if (!string.IsNullOrEmpty(request.Prompt))
        {
            content.Add(new StringContent(request.Prompt), "prompt");
        }
        
        string modelName = request.Model?.Name ?? VideoModel.OpenAi.Sora.Sora2.Name;
        content.Add(new StringContent(modelName), "model");
        
        string? seconds = MapDurationToSeconds(request.Duration, request.DurationSeconds);
        if (seconds is not null)
        {
            content.Add(new StringContent(seconds), "seconds");
        }
        
        string? size = MapToSize(request.AspectRatio, request.Resolution);
        if (size is not null)
        {
            content.Add(new StringContent(size), "size");
        }
        
        if (request.Image is not null)
        {
            byte[]? imageBytes = GetImageBytes(request.Image);
            if (imageBytes is not null)
            {
                ByteArrayContent imageContent = new ByteArrayContent(imageBytes);
                string mimeType = request.Image.MimeType ?? "image/jpeg";
                imageContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                content.Add(imageContent, "input_reference", "input_reference.jpg");
            }
        }
        
        return content;
    }

    /// <summary>
    /// Serializes a harmonized video generation request to OpenAI's JSON body.
    /// Used for character references, Batch API, and explicit JSON requests.
    /// </summary>
    public static object SerializeJson(VideoGenerationRequest request)
    {
        Dictionary<string, object?> body = new Dictionary<string, object?>
        {
            ["prompt"] = request.Prompt,
            ["model"] = request.Model?.Name ?? VideoModel.OpenAi.Sora.Sora2.Name
        };

        string? seconds = MapDurationToSeconds(request.Duration, request.DurationSeconds);
        if (seconds is not null)
        {
            body["seconds"] = seconds;
        }

        string? size = MapToSize(request.AspectRatio, request.Resolution);
        if (size is not null)
        {
            body["size"] = size;
        }

        VideoOpenAiExtensions? ext = request.OpenAiExtensions;
        if (ext?.InputReference is not null)
        {
            body["input_reference"] = ext.InputReference;
        }
        else if (request.Image is not null && !string.IsNullOrEmpty(request.Image.Url))
        {
            // Raw base64 is wrapped as a data URI (image/jpeg when no MIME type is set, as in the multipart path).
            VideoImage image = string.IsNullOrWhiteSpace(request.Image.MimeType)
                ? new VideoImage(request.Image.Url, "image/jpeg")
                : request.Image;
            string url = image.AsProviderImageUrl();
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                body["input_reference"] = new VideoInputReference { ImageUrl = url };
            }
        }

        if (ext?.Characters is { Count: > 0 })
        {
            body["characters"] = ext.Characters;
        }

        return body;
    }

    /// <summary>
    /// Returns true when the request must use JSON instead of multipart.
    /// </summary>
    public static bool RequiresJsonContent(VideoGenerationRequest request)
    {
        if (request.OpenAiExtensions?.UseJsonContent == true)
        {
            return true;
        }

        return request.OpenAiExtensions?.Characters is { Count: > 0 };
    }

    /// <summary>
    /// Serializes an extension request to OpenAI's JSON body.
    /// </summary>
    public static object SerializeExtension(VideoExtensionRequest request)
    {
        Dictionary<string, object?> body = new Dictionary<string, object?>
        {
            ["prompt"] = request.Prompt,
            ["video"] = new VideoReference(request.VideoId)
        };

        string? seconds = MapDurationToSeconds(request.Duration, request.DurationSeconds);
        if (seconds is not null)
        {
            body["seconds"] = seconds;
        }

        return body;
    }

    /// <summary>
    /// Serializes an edit request to OpenAI's JSON body (video ID reference).
    /// </summary>
    public static object SerializeEditJson(VideoEditRequest request)
    {
        if (string.IsNullOrEmpty(request.VideoId))
        {
            throw new ArgumentException("VideoId is required for JSON video edit requests.", nameof(request));
        }

        Dictionary<string, object?> body = new Dictionary<string, object?>
        {
            ["prompt"] = request.Prompt,
            ["video"] = new VideoReference(request.VideoId)
        };

        if (request.Model is not null)
        {
            body["model"] = request.Model.Name;
        }

        return body;
    }

    /// <summary>
    /// Serializes an edit request with uploaded video to multipart/form-data.
    /// </summary>
    public static MultipartFormDataContent SerializeEditMultipart(VideoEditRequest request)
    {
        MultipartFormDataContent content = new MultipartFormDataContent();
        content.Add(new StringContent(request.Prompt), "prompt");

        if (request.Model is not null)
        {
            content.Add(new StringContent(request.Model.Name), "model");
        }

        if (request.VideoBytes is not null)
        {
            ByteArrayContent videoContent = new ByteArrayContent(request.VideoBytes);
            string mimeType = request.VideoMimeType ?? "video/mp4";
            videoContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            content.Add(videoContent, "video", "source.mp4");
        }

        return content;
    }

    /// <summary>
    /// Maps harmonized VideoDuration to OpenAI seconds string.
    /// </summary>
    internal static string? MapDurationToSeconds(VideoDuration? duration, int? customSeconds)
    {
        if (customSeconds.HasValue)
        {
            return customSeconds.Value switch
            {
                <= 4 => "4",
                <= 8 => "8",
                <= 12 => "12",
                <= 16 => "16",
                _ => "20"
            };
        }
        
        if (duration is null)
        {
            return null;
        }
        
        return duration.Value switch
        {
            VideoDuration.Seconds4 => "4",
            VideoDuration.Seconds5 => "4",
            VideoDuration.Seconds6 => "8",
            VideoDuration.Seconds8 => "8",
            VideoDuration.Seconds10 => "12",
            VideoDuration.Seconds12 => "12",
            VideoDuration.Seconds16 => "16",
            VideoDuration.Seconds20 => "20",
            _ => null
        };
    }
    
    /// <summary>
    /// Maps harmonized AspectRatio + Resolution to OpenAI size string.
    /// </summary>
    internal static string? MapToSize(VideoAspectRatio? aspectRatio, VideoResolution? resolution)
    {
        if (aspectRatio is null)
        {
            return null;
        }
        
        return aspectRatio.Value switch
        {
            VideoAspectRatio.Portrait => resolution == VideoResolution.FullHD ? "1080x1920" : "720x1280",
            VideoAspectRatio.Widescreen => resolution == VideoResolution.FullHD ? "1920x1080" : "1280x720",
            _ => null
        };
    }
    
    /// <summary>
    /// Gets image bytes from VideoImage (handles base64).
    /// </summary>
    private static byte[]? GetImageBytes(VideoImage image)
    {
        if (string.IsNullOrEmpty(image.Url))
        {
            return null;
        }
        
        try
        {
            if (image.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int commaIndex = image.Url.IndexOf(',');
                if (commaIndex > 0)
                {
                    return Convert.FromBase64String(image.Url.Substring(commaIndex + 1));
                }
            }
            
            return Convert.FromBase64String(image.Url);
        }
        catch
        {
            return null;
        }
    }
}
