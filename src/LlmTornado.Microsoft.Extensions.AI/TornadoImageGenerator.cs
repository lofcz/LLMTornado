using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Images;
using Microsoft.Extensions.AI;
#pragma warning disable MEAI001

namespace LlmTornado.Microsoft.Extensions.AI;

/// <summary>
/// Provides an <see cref="IImageGenerator"/> implementation for LlmTornado.
/// </summary>
public sealed class TornadoImageGenerator : IImageGenerator
{
    private static readonly ActivitySource ActivitySource = new ActivitySource("LlmTornado.Microsoft.Extensions.AI.Images");

    private readonly TornadoApi _api;
    private readonly string _defaultModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="TornadoImageGenerator"/> class.
    /// </summary>
    /// <param name="api">The LlmTornado API instance.</param>
    /// <param name="defaultModel">The default model to use for image generation.</param>
    public TornadoImageGenerator(TornadoApi api, string defaultModel)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _defaultModel = defaultModel ?? throw new ArgumentNullException(nameof(defaultModel));
    }

    /// <inheritdoc cref="IImageGenerator" />
    public ImageGeneratorMetadata Metadata => new ImageGeneratorMetadata(providerName: "LlmTornado", providerUri: new Uri("https://github.com/lofcz/LlmTornado"), defaultModelId: _defaultModel);
    
    /// <inheritdoc />
    public async Task<ImageGenerationResponse> GenerateAsync(
        global::Microsoft.Extensions.AI.ImageGenerationRequest request,
        ImageGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = ActivitySource.StartActivity("GenerateAsync");

        string model = options?.ModelId ?? _defaultModel;
        
        activity?.SetTag("llm.model", model);
        activity?.SetTag("llm.request.prompt", request.Prompt);

        try
        {
            Images.ImageGenerationRequest tornadoRequest = request.ToLlmTornado(model, options);
            tornadoRequest.CancellationToken = cancellationToken;
            ImageGenerationResult? result = await _api.ImageGenerations.CreateImage(tornadoRequest);

            if (result == null)
            {
                throw new InvalidOperationException("Image generation returned null result.");
            }

            ImageGenerationResponse response = result.ToMicrosoftAI(options?.MediaType);
            
            activity?.SetTag("llm.usage.output_count", response.Contents?.Count ?? 0);

            return response;
        }
        catch (Exception ex)
        {
            activity?.SetTag("error", true);
            activity?.SetTag("error.type", ex.GetType().FullName);
            activity?.SetTag("error.message", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType == typeof(TornadoApi) ? _api : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // TornadoApi doesn't implement IDisposable, so nothing to dispose.
    }
}
