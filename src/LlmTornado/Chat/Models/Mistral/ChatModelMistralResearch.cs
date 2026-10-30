using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models.Mistral;

/// <summary>
/// All Research (open-weights) models from Mistral.
/// </summary>
public class ChatModelMistralResearch : IVendorModelClassProvider
{
    /// <summary>
    /// Our best multilingual open source model released July 2024.
    /// </summary>
    [Obsolete("Retired July 31, 2026 on the Mistral API. Use ChatModel.Mistral.Free.Ministral8b2512 instead.")]
    public static readonly ChatModel ModelMistralNemo = new ChatModel("open-mistral-nemo", LLmProviders.Mistral, 131_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralNemo"/>
    /// </summary>
    [Obsolete("Retired July 31, 2026 on the Mistral API. Use ChatModel.Mistral.Free.Ministral8b2512 instead.")]
    public readonly ChatModel MistralNemo = ModelMistralNemo;
    
    /// <summary>
    /// Our first mamba 2 open source model released July 2024. 
    /// </summary>
    [Obsolete("Retired June 6, 2025 on the Mistral API. Use ChatModel.Mistral.Premier.Codestral2508 instead.")]
    public static readonly ChatModel ModelCodestralMamba = new ChatModel("open-codestral-mamba", LLmProviders.Mistral, 256_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelCodestralMamba"/>
    /// </summary>
    [Obsolete("Retired June 6, 2025 on the Mistral API. Use ChatModel.Mistral.Premier.Codestral2508 instead.")]
    public readonly ChatModel CodestralMamba = ModelCodestralMamba;
    
    /// <summary>
    /// All known Premier models from Mistral.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [ModelMistralNemo, ModelCodestralMamba]);

    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;

    internal ChatModelMistralResearch()
    {

    }
}