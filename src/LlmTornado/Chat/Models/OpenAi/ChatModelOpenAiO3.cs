using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// O3 class models from OpenAI.
/// </summary>
public class ChatModelOpenAiO3 : IVendorModelClassProvider
{
    /// <summary>
    /// o3-deep-research is our most advanced model for deep research, designed to tackle complex, multi-step research tasks. It can search and synthesize information from across the internet as well as from your own data—brought in through MCP connectors.
    /// </summary>
    public static readonly ChatModel ModelV3DeepResearch = new ChatModel("o3-deep-research", LLmProviders.OpenAi, 200_000, [ "o3-deep-research-2025-06-26" ])
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Batch ]
    };

    /// <summary>
    /// <inheritdoc cref="ModelV3DeepResearch"/>
    /// </summary>
    public readonly ChatModel V3DeepResearch = ModelV3DeepResearch;
    
    /// <summary>
    /// Latest o3 mini model snapshot.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelMini250131 = new ChatModel("o3-mini-2025-01-31", LLmProviders.OpenAi, 200_000);

    /// <summary>
    /// <inheritdoc cref="ModelMini250131"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel Mini250131 = ModelMini250131;
    
    /// <summary>
    /// O3 is a well-rounded and powerful model across domains. It sets a new standard for math, science, coding, and visual reasoning tasks. It also excels at technical writing and instruction-following. Use it to think through multi-step problems that involve analysis across text, code, and images.
    /// </summary>
    public static readonly ChatModel ModelV3 = new ChatModel("o3", LLmProviders.OpenAi, 200_000, [ "o3-2025-04-16" ]);

    /// <summary>
    /// <inheritdoc cref="ModelV3"/>
    /// </summary>
    public readonly ChatModel V3 = ModelV3;
    
    /// <summary>
    /// Latest o3 mini model snapshot.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelMini = new ChatModel("o3-mini", LLmProviders.OpenAi, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMini"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel Mini = ModelMini;
    
    /// <summary>
    /// All known O3 models from OpenAI.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [ModelMini250131, ModelMini, ModelV3, ModelV3DeepResearch]);
    
    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;
    
    internal ChatModelOpenAiO3()
    {
        
    }
}