using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Videos.Models;
using LlmTornado.Videos.Vendors.Google;
using LlmTornado.Videos.Vendors.OpenAi;
using LlmTornado.Videos.Vendors.XAi;
using LlmTornado.Videos.Vendors.MiniMax;
using LlmTornado.Videos.Vendors.Zai;
using LlmTornado.Vendor.Google;
using Newtonsoft.Json;

namespace LlmTornado.Videos;

/// <summary>
///     Represents a request to the Videos API.  Mostly matches the parameters in
///     the Google Veo API documentation.
/// </summary>
public class VideoGenerationRequest
{
    /// <summary>
    ///     Creates a new, empty <see cref="VideoGenerationRequest" />
    /// </summary>
    public VideoGenerationRequest()
    {
    }
    
    /// <summary>
    ///     Creates a new, minimal <see cref="VideoGenerationRequest" />
    /// </summary>
    public VideoGenerationRequest(string prompt)
    {
        Prompt = prompt;
    }

    /// <summary>
    ///     Creates a new <see cref="VideoGenerationRequest" /> with the specified parameters
    /// </summary>
    /// <param name="prompt">A text description of the desired video(s)</param>
    /// <param name="model">Model to use</param>
    /// <param name="duration">Length of the generated video</param>
    /// <param name="aspectRatio">The video's aspect ratio</param>
    /// <param name="resolution">The video's resolution</param>
    /// <param name="personGeneration">Controls the generation of people</param>
    public VideoGenerationRequest(string prompt, VideoModel? model = null, VideoDuration? duration = null, VideoAspectRatio? aspectRatio = null, VideoResolution? resolution = null, VideoPersonGeneration? personGeneration = null)
    {
        Prompt = prompt;
#pragma warning disable CS0618 // Type or member is obsolete
        Model = model ?? VideoModel.Google.Veo.V31;
#pragma warning restore CS0618 // Type or member is obsolete
        Duration = duration;
        AspectRatio = aspectRatio;
        Resolution = resolution;
        PersonGeneration = personGeneration;
    }

    /// <summary>
    ///     A text description of the desired video(s).
    /// </summary>
    [JsonProperty("prompt")]
    public string? Prompt { get; set; }

    /// <summary>
    ///     Text describing what not to include in the video.
    /// </summary>
    [JsonProperty("negativePrompt")]
    public string? NegativePrompt { get; set; }

    /// <summary>
    ///     An initial image to animate (image-to-video).
    /// </summary>
    [JsonIgnore]
    public VideoImage? Image { get; set; }
    
    /// <summary>
    ///     The final image for an interpolation video to transition. Must be used in combination with the Image parameter.
    /// </summary>
    [JsonIgnore]
    public VideoImage? LastFrame { get; set; }
    
    /// <summary>
    ///     Up to three images to be used as style and content references (Veo 3.1 and Veo 3.1 Fast only, not Veo 3.1 Lite).
    /// </summary>
    [JsonIgnore]
    public List<VideoReferenceImage>? ReferenceImages { get; set; }
    
    /// <summary>
    ///     Video to be used for video extension (Veo 3.1 and Veo 3.1 Fast only, not Veo 3.1 Lite).
    /// </summary>
    [JsonIgnore]
    public VideoInput? Video { get; set; }
    
    /// <summary>
    ///     The video's aspect ratio.
    /// </summary>
    [JsonIgnore]
    public VideoAspectRatio? AspectRatio { get; set; }
    
    /// <summary>
    ///     The video's resolution.
    /// </summary>
    [JsonIgnore]
    public VideoResolution? Resolution { get; set; }
    
    /// <summary>
    ///     Length of the generated video. Valid values depend on the model.
    ///     Must be Seconds8 when using extension, interpolation, reference images, 1080p, or 4k resolution.
    /// </summary>
    [JsonIgnore]
    public VideoDuration? Duration { get; set; }
    
    /// <summary>
    ///     Custom duration in seconds. If set, this takes precedence over Duration.
    ///     Only used when Duration is set to Custom, or when you need to specify a specific duration value.
    /// </summary>
    [JsonIgnore]
    public int? DurationSeconds { get; set; }
    
    /// <summary>
    ///     Controls the generation of people. See documentation for region-specific restrictions.
    /// </summary>
    [JsonIgnore]
    public VideoPersonGeneration? PersonGeneration { get; set; }
    
    /// <summary>
    ///     The model to use for video generation.
    /// </summary>
    [JsonIgnore]
    public VideoModel? Model { get; set; }
    
    /// <summary>
    ///     Google-specific extensions for video generation.
    /// </summary>
    [JsonIgnore]
    public VideoGenerationRequestGoogleExtensions? GoogleExtensions { get; set; }
    
    /// <summary>
    ///     OpenAI-specific extensions for video generation.
    /// </summary>
    [JsonIgnore]
    public VideoOpenAiExtensions? OpenAiExtensions { get; set; }
    
    /// <summary>
    ///     xAI-specific extensions for video generation.
    /// </summary>
    [JsonIgnore]
    public VideoXAiExtensions? XAiExtensions { get; set; }
    
    /// <summary>
    ///     Z.AI-specific extensions for video generation.
    /// </summary>
    [JsonIgnore]
    public VideoZaiExtensions? ZaiExtensions { get; set; }
    
    /// <summary>
    ///     MiniMax-specific extensions for video generation.
    /// </summary>
    [JsonIgnore]
    public VideoMiniMaxExtensions? MiniMaxExtensions { get; set; }

    private static readonly Dictionary<LLmProviders, Func<VideoGenerationRequest, IEndpointProvider, JsonSerializerSettings?, TornadoRequestContent>> serializeMap = new Dictionary<LLmProviders, Func<VideoGenerationRequest, IEndpointProvider, JsonSerializerSettings?, TornadoRequestContent>>
    {
        { 
            LLmProviders.Google, (request, provider, settings) =>
            {
                VendorGoogleVideoRequest googleRequest = new VendorGoogleVideoRequest(request, provider);
                string body = JsonConvert.SerializeObject(googleRequest, settings ?? new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                
                string modelName = request.Model?.Name ?? VideoModel.Google.Veo.V31.Name;
                string urlFragment = $"/{modelName}:predictLongRunning";
                return new TornadoRequestContent(body, request.Model, $"{provider.ApiUrl(CapabilityEndpoints.Videos, null)}{urlFragment}", provider, CapabilityEndpoints.Videos);
            }
        }
    };

    /// <summary>
    ///     Serializes this request to a JSON string.
    /// </summary>
    /// <param name="provider">The endpoint provider to serialize for</param>
    /// <returns>The JSON representation of this request</returns>
    public TornadoRequestContent Serialize(IEndpointProvider provider)
    {
        if (!serializeMap.TryGetValue(provider.Provider, out Func<VideoGenerationRequest, IEndpointProvider, JsonSerializerSettings?, TornadoRequestContent>? serializerFn))
        {
            throw new NotSupportedException($"Provider {provider.Provider} is not supported for video generation");
        }

        return serializerFn.Invoke(this, provider, null);
    }
}