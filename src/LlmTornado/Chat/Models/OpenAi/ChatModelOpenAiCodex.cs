using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// Codex class models from OpenAI.
/// </summary>
public class ChatModelOpenAiCodex : IVendorModelClassProvider
{
    /// <summary>
    /// GPT-5.3-Codex is an agentic coding model, deprecated October 1, 2026.
    /// Available in the API until April 1, 2027. Use GPT-6 Sol for new requests.
    /// Supports low, medium, high, and xhigh reasoning effort settings.
    /// 400,000 context window, 128,000 max output tokens, Aug 31, 2025 knowledge cutoff.
    /// Like the other Codex models, it is served only via the Responses (and Batch) endpoints,
    /// not via /v1/chat/completions.
    /// </summary>
    public static readonly ChatModel ModelGpt53Codex = new ChatModel("gpt-5.3-codex", LLmProviders.OpenAi, 400_000)
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Batch ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelGpt53Codex"/>
    /// </summary>
    public readonly ChatModel Gpt53Codex = ModelGpt53Codex;

    /// <summary>
    /// All known Codex models from OpenAI.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [ModelGpt53Codex]);
    
    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;

    internal ChatModelOpenAiCodex()
    {
        
    }
}
