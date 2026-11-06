using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// GPT-3.5 class models from OpenAI.
/// </summary>
public class ChatModelOpenAiGpt35 : IVendorModelClassProvider
{
    /// <summary>
    /// Currently points to gpt-3.5-turbo-0125.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public static readonly ChatModel ModelTurbo = new ChatModel("gpt-3.5-turbo", LLmProviders.OpenAi, 16_385);

    /// <summary>
    /// <inheritdoc cref="ModelTurbo"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public readonly ChatModel Turbo = ModelTurbo;
    
    /// <summary>
    /// The latest GPT-3.5 Turbo model with higher accuracy at responding in requested formats and a fix for a bug which caused a text encoding issue for non-English language function calls. Returns a maximum of 4,096 output tokens.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public static readonly ChatModel ModelTurbo240125 = new ChatModel("gpt-3.5-turbo-0125", LLmProviders.OpenAi, 16_385);

    /// <summary>
    /// <inheritdoc cref="ModelTurbo240125"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public readonly ChatModel Turbo240125 = ModelTurbo240125;
    
    /// <summary>
    /// GPT-3.5 Turbo model with improved instruction following, JSON mode, reproducible outputs, parallel function calling, and more. Returns a maximum of 4,096 output tokens.
    /// </summary>
    [Obsolete("Retired September 28, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public static readonly ChatModel ModelTurbo231106 = new ChatModel("gpt-3.5-turbo-1106", LLmProviders.OpenAi, 16_385);

    /// <summary>
    /// <inheritdoc cref="ModelTurbo231106"/>
    /// </summary>
    [Obsolete("Retired September 28, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public readonly ChatModel Turbo231106 = ModelTurbo231106;

    /// <summary>
    /// Similar capabilities as GPT-3 era models. Compatible with legacy Completions endpoint and not Chat Completions.
    /// </summary>
    [Obsolete("Retired September 28, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public static readonly ChatModel ModelTurboInstruct = new ChatModel("gpt-3.5-turbo-instruct", LLmProviders.OpenAi, 4_096);

    /// <summary>
    /// <inheritdoc cref="ModelTurboInstruct"/>
    /// </summary>
    [Obsolete("Retired September 28, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public readonly ChatModel TurboInstruct = ModelTurboInstruct;

    /// <summary>
    /// All known GPT 3.5 models from OpenAI.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [ModelTurbo, ModelTurbo240125, ModelTurbo231106, ModelTurboInstruct]);
    
    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;

    internal ChatModelOpenAiGpt35()
    {
        
    }
}