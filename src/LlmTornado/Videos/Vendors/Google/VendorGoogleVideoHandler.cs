using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;
using LlmTornado.Videos.Models;
using Newtonsoft.Json;

namespace LlmTornado.Videos.Vendors.Google;

/// <summary>
/// Handles all video operations for Google, mapping to harmonized VideoJob.
/// </summary>
internal static class VendorGoogleVideoHandler
{
    /// <summary>
    /// Creates a new video generation job.
    /// </summary>
    public static async Task<HttpCallResult<VideoJob>> Create(
        VideoGenerationRequest request, 
        IEndpointProvider provider, 
        EndpointBase endpoint, 
        CancellationToken cancellationToken)
    {
        // Serialize to Google format
        VendorGoogleVideoRequest googleRequest = new VendorGoogleVideoRequest(request, provider);
        string body = JsonConvert.SerializeObject(googleRequest, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
        
#pragma warning disable CS0618 // Type or member is obsolete
        string modelName = request.Model?.Name ?? VideoModel.Google.Veo.V31.Name;
#pragma warning restore CS0618 // Type or member is obsolete
        string urlFragment = $"/{modelName}:predictLongRunning";
        string url = $"{provider.ApiUrl(CapabilityEndpoints.Videos, null)}{urlFragment}";
        
        HttpCallResult<VendorGoogleVideoResult> result = await endpoint.HttpPost<VendorGoogleVideoResult>(
            provider, 
            CapabilityEndpoints.Videos, 
            url,
            postData: body,
            ct: cancellationToken
        ).ConfigureAwait(false);
        
        return MapToVideoJob(result, provider.Provider, request.Model?.Name);
    }
    
    /// <summary>
    /// Retrieves the status of a video job.
    /// </summary>
    public static async Task<HttpCallResult<VideoJob>> Get(
        string operationName, 
        IEndpointProvider provider, 
        EndpointBase endpoint,
        string? modelName,
        CancellationToken cancellationToken)
    {
        // The operation name is already a complete path like "models/veo-3.1-generate-preview/operations/xyz"
        // We need to append it directly to the base URL
        string url = provider.ApiUrl(CapabilityEndpoints.BaseUrl, operationName);
        
        HttpCallResult<VendorGoogleVideoResult> result = await endpoint.HttpGet<VendorGoogleVideoResult>(
            provider, 
            CapabilityEndpoints.Videos, 
            url,
            ct: cancellationToken
        ).ConfigureAwait(false);
        
        return MapToVideoJob(result, provider.Provider, modelName);
    }
    
    /// <summary>
    /// Downloads video content from Google.
    /// </summary>
    public static async Task<StreamResponse?> GetContent(
        string videoUri,
        IEndpointProvider provider, 
        EndpointBase endpoint, 
        CancellationToken cancellationToken)
    {
        string downloadUrl;
        
        // Check if URI already contains :download (it's already a download URL)
        if (videoUri.Contains(":download"))
        {
            downloadUrl = videoUri;
        }
        else
        {
            // Extract file ID from URI like: https://generativelanguage.googleapis.com/v1beta/files/{fileId}
            string uriWithoutQuery = videoUri.Split('?')[0];
            string fileId = uriWithoutQuery.Split('/').Last();
            downloadUrl = $"https://generativelanguage.googleapis.com/download/v1beta/files/{fileId}:download?alt=media";
        }
        
        Dictionary<string, string>? headers = null;
        ProviderAuthentication? auth = provider.Auth;
        
        if (auth?.ApiKey is not null)
        {
            headers = new Dictionary<string, string> { { "x-goog-api-key", auth.ApiKey.Trim() } };
        }
        
        return await endpoint.HttpGetRawStream(provider, downloadUrl, headers, cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Maps Google response to harmonized VideoJob.
    /// </summary>
    private static HttpCallResult<VideoJob> MapToVideoJob(
        HttpCallResult<VendorGoogleVideoResult> result, 
        LLmProviders provider,
        string? modelName)
    {
        if (!result.Ok || result.Data is null)
        {
            return new HttpCallResult<VideoJob>(result.Code, result.Response, null, false, result.Request)
            {
                Exception = result.Exception
            };
        }
        
        VendorGoogleVideoResult google = result.Data;
        
        // Extract video URI from completed result
        string? videoUri = google.Response?.GenerateVideoResponse?.GeneratedSamples?.FirstOrDefault()?.Video?.Uri;
        
        VideoJob job = new VideoJob
        {
            // Use operation name as ID
            Id = google.Name ?? string.Empty,
            Object = "video",
            Model = modelName ?? "veo",
            Status = MapGoogleStatus(google),
            Progress = google.Metadata?.ProgressPercent,
            SourceProvider = provider,
            
            // Google-specific fields
            OperationName = google.Name,
            VideoUri = videoUri
        };
        
        // Map error if present
        if (google.Error is not null)
        {
            job.Error = new VideoJobError
            {
                Code = google.Error.Code?.ToString(),
                Message = google.Error.Message
            };
        }
        
        return new HttpCallResult<VideoJob>(result.Code, result.Response, job, true, result.Request);
    }
    
    /// <summary>
    /// Maps Google's done/error state to VideoJobStatus.
    /// </summary>
    private static VideoJobStatus MapGoogleStatus(VendorGoogleVideoResult result)
    {
        if (result.Error is not null)
        {
            return VideoJobStatus.Failed;
        }
        
        if (result.Done)
        {
            return VideoJobStatus.Completed;
        }
        
        // Google doesn't have explicit in_progress vs queued, but if we have progress, it's in progress
        if (result.Metadata?.ProgressPercent > 0)
        {
            return VideoJobStatus.InProgress;
        }
        
        return VideoJobStatus.Queued;
    }
}
