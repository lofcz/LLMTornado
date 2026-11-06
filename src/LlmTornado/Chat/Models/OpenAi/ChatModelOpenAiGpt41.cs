using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// GPT-4.1 class models from OpenAI.
/// </summary>
public class ChatModelOpenAiGpt41 : IVendorModelClassProvider
{
    /// <summary>
    /// Latest snapshot of GPT-4.1, currently gpt-4.1-2025-04-14.
    /// </summary>
    public static readonly ChatModel ModelV41 = new ChatModel("gpt-4.1", LLmProviders.OpenAi, 1_000_000, [ "gpt-4.1-2025-04-14" ])
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Chat, ChatModelEndpointCapabilities.Batch ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelV41"/>
    /// </summary>
    public readonly ChatModel V41 = ModelV41;

    /// <summary>
    /// Latest snapshot of GPT-4.1-Mini, currently gpt-4.1-mini-2025-04-14.
    /// </summary>
    public static readonly ChatModel ModelV41Mini = new ChatModel("gpt-4.1-mini", LLmProviders.OpenAi, 1_000_000, ["gpt-4.1-mini-2025-04-14"])
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Chat, ChatModelEndpointCapabilities.Batch ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelV41Mini"/>
    /// </summary>
    public readonly ChatModel V41Mini = ModelV41Mini;
    
    /// <summary>
    /// Latest snapshot of GPT-4.1-Nano, currently gpt-4.1-mini-2025-04-14.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Luna instead.")]
    public static readonly ChatModel ModelV41Nano = new ChatModel("gpt-4.1-nano", LLmProviders.OpenAi, 1_000_000, [ "gpt-4.1-nano-2025-04-14" ]);

    /// <summary>
    /// <inheritdoc cref="ModelV41Nano"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Luna instead.")]
    public readonly ChatModel V41Nano = ModelV41Nano;

    /// <summary>
    /// All known GPT-4.1 models from OpenAI.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [ModelV41, ModelV41Mini, ModelV41Nano]);
    
    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;
    
    internal ChatModelOpenAiGpt41()
    {
        
    }
}