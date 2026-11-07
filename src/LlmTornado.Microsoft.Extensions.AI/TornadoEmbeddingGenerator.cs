using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Embedding;
using LlmTornado.Embedding.Models;
using Microsoft.Extensions.AI;

namespace LlmTornado.Microsoft.Extensions.AI;

/// <summary>
/// Provides an <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> implementation for LlmTornado.
/// </summary>
public sealed class TornadoEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private static readonly ActivitySource ActivitySource = new ActivitySource("LlmTornado.Microsoft.Extensions.AI.Embeddings");

    private readonly TornadoApi _api;
    private readonly EmbeddingModel _defaultModel;
    private readonly int? _defaultDimensions;

    /// <summary>
    /// Initializes a new instance of <see cref="TornadoEmbeddingGenerator"/>.
    /// </summary>
    /// <param name="api">The LlmTornado API instance.</param>
    /// <param name="defaultModel">The default model to use for embedding operations.</param>
    /// <param name="defaultDimensions">Optional default dimensions for embeddings.</param>
    public TornadoEmbeddingGenerator(TornadoApi api, EmbeddingModel defaultModel, int? defaultDimensions = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _defaultModel = defaultModel ?? throw new ArgumentNullException(nameof(defaultModel));
        _defaultDimensions = defaultDimensions;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TornadoEmbeddingGenerator"/>.
    /// </summary>
    /// <param name="api">The LlmTornado API instance.</param>
    /// <param name="defaultModel">The default model string to use for embedding operations.</param>
    /// <param name="defaultDimensions">Optional default dimensions for embeddings.</param>
    public TornadoEmbeddingGenerator(TornadoApi api, string defaultModel, int? defaultDimensions = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _defaultModel = defaultModel ?? throw new ArgumentNullException(nameof(defaultModel));
        _defaultDimensions = defaultDimensions;
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata => new EmbeddingGeneratorMetadata(providerName: "LlmTornado", providerUri: new Uri("https://github.com/lofcz/LlmTornado"), defaultModelId: _defaultModel.ToString(), defaultModelDimensions: _defaultDimensions);

    /// <inheritdoc />
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        List<string> valuesList = values.ToList();
        EmbeddingModel model = !string.IsNullOrEmpty(options?.ModelId) ? options.ModelId : _defaultModel;
        
        using Activity? activity = ActivitySource.StartActivity("GenerateAsync");

        activity?.SetTag("llm.model", model.ToString());
        activity?.SetTag("llm.embedding.input.count", valuesList.Count);

        try
        {
            // Determine dimensions
            int? dimensions = _defaultDimensions;
            if (options?.Dimensions.HasValue == true)
            {
                dimensions = options.Dimensions.Value;
            }

            activity?.SetTag("llm.embedding.dimensions", dimensions);

            // Create request. A single string stays a scalar input; a list stays a vector.
            EmbeddingRequest request = valuesList.Count == 1
                ? new EmbeddingRequest(model, valuesList[0])
                : new EmbeddingRequest(model, valuesList);
            if (dimensions.HasValue)
            {
                request.Dimensions = dimensions.Value;
            }

            request.CancellationToken = cancellationToken;
            EmbeddingResult? result = await _api.Embeddings.CreateEmbedding(request);

            if (result?.Data == null)
            {
                throw new InvalidOperationException("Embedding generation returned null result.");
            }

            // Convert to Microsoft.Extensions.AI format
            List<Embedding<float>> embeddings = result.Data.Select((entry, index) => 
                new Embedding<float>(entry.Embedding ?? [])
                {
                    ModelId = result.Model,
                    CreatedAt = result.Created
                }).ToList();

            GeneratedEmbeddings<Embedding<float>> generated = new GeneratedEmbeddings<Embedding<float>>(embeddings);

            // Add usage information if available
            if (result.Usage != null)
            {
                generated.Usage = new UsageDetails
                {
                    InputTokenCount = result.Usage.PromptTokens,
                    TotalTokenCount = result.Usage.TotalTokens
                };
            }

            // Set additional properties
            generated.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            generated.AdditionalProperties["RawRepresentation"] = result;

            activity?.SetTag("llm.embedding.output.count", embeddings.Count);
            activity?.SetTag("llm.usage.input_tokens", generated.Usage?.InputTokenCount);
            activity?.SetTag("llm.usage.total_tokens", generated.Usage?.TotalTokenCount);

            return generated;
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
        // TornadoApi doesn't implement IDisposable, so nothing to dispose
    }
}
