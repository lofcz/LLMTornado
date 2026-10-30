using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models.Mistral;

/// <summary>
/// All Free (as in open-weights) models from Mistral.
/// </summary>
public class ChatModelMistralFree : IVendorModelClassProvider
{
    /// <summary>
    /// Voxtral Small (24B).
    /// </summary>
    public static readonly ChatModel ModelVoxtralSmall2507 = new ChatModel("voxtral-small-2507", LLmProviders.Mistral, 32_000, [ "voxtral-small-latest" ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelVoxtralSmall2507"/>
    /// </summary>
    public readonly ChatModel VoxtralSmall2507 = ModelVoxtralSmall2507;
    
    /// <summary>
    /// Voxtral Mini (3B).
    /// </summary>
    [Obsolete("Retired May 31, 2026 on the Mistral API. Use AudioModel.Mistral.Free.VoxtralMiniTranscribe2 instead.")]
    public static readonly ChatModel ModelVoxtralMini2507 = new ChatModel("voxtral-mini-2507", LLmProviders.Mistral, 32_000, [ "voxtral-mini-latest" ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelVoxtralMini2507"/>
    /// </summary>
    [Obsolete("Retired May 31, 2026 on the Mistral API. Use AudioModel.Mistral.Free.VoxtralMiniTranscribe2 instead.")]
    public readonly ChatModel VoxtralMini2507 = ModelVoxtralMini2507;
    
    /// <summary>
    /// Devstral Small 1.1 (24B)
    /// </summary>
    [Obsolete("Retired May 31, 2026 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public static readonly ChatModel ModelDevstralSmall2507 = new ChatModel("devstral-small-2507", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelDevstralSmall2507"/>
    /// </summary>
    [Obsolete("Retired May 31, 2026 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public readonly ChatModel DevstralSmall2507 = ModelDevstralSmall2507;
    
    /// <summary>
    /// Mistral Small 4 — hybrid instruct / reasoning / coding multimodal model released March 2026.
    /// 256k context. Toggle reasoning per request with <c>reasoning_effort</c> (<c>none</c> / <c>high</c>). Apache 2.0.
    /// </summary>
    public static readonly ChatModel ModelMistralSmall2603 = new ChatModel("mistral-small-2603", LLmProviders.Mistral, 256_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralSmall2603"/>
    /// </summary>
    public readonly ChatModel MistralSmall2603 = ModelMistralSmall2603;
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralSmall2603"/>
    /// </summary>
    public readonly ChatModel MistralSmall4 = ModelMistralSmall2603;
    
    /// <summary>
    /// Leanstral 1.5 — Lean 4 formal proof engineering model released June 2026. 256k context. Apache 2.0.
    /// </summary>
    public static readonly ChatModel ModelLeanstral15 = new ChatModel("labs-leanstral-1-5", LLmProviders.Mistral, 256_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelLeanstral15"/>
    /// </summary>
    public readonly ChatModel Leanstral15 = ModelLeanstral15;
    
    /// <summary>
    /// Leanstral — first Lean 4 formal proof engineering model released March 2026.
    /// </summary>
    public static readonly ChatModel ModelLeanstral2603 = new ChatModel("labs-leanstral-2603", LLmProviders.Mistral, 256_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelLeanstral2603"/>
    /// </summary>
    public readonly ChatModel Leanstral2603 = ModelLeanstral2603;
    
    /// <summary>
    /// Devstral Small 2 (24B) - Next-generation coding model for software development. Apache 2.0 license.
    /// </summary>
    [Obsolete("Retired March 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public static readonly ChatModel ModelDevstralSmall2512 = new ChatModel("devstral-small-2512", LLmProviders.Mistral, 128_000, [ "labs-devstral-small-2512" ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelDevstralSmall2512"/>
    /// </summary>
    [Obsolete("Retired March 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public readonly ChatModel DevstralSmall2512 = ModelDevstralSmall2512;
    
    /// <summary>
    /// Mistral Large 3 — open-weights, multimodal MoE model released December 2025.
    /// </summary>
    public static readonly ChatModel ModelMistralLarge2512 = new ChatModel("mistral-large-2512", LLmProviders.Mistral, 256_000, [ "mistral-large-latest" ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralLarge2512"/>
    /// </summary>
    public readonly ChatModel MistralLarge2512 = ModelMistralLarge2512;
    
    /// <summary>
    /// Ministral 3B 2512 — compact open-weights model (256k context, text & vision).
    /// </summary>
    public static readonly ChatModel ModelMinistral3b2512 = new ChatModel("ministral-3b-2512", LLmProviders.Mistral, 256_000, [ "ministral-3b-latest" ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelMinistral3b2512"/>
    /// </summary>
    public readonly ChatModel Ministral3b2512 = ModelMinistral3b2512;
    
    /// <summary>
    /// Ministral 8B 2512 — balanced open-weights model (256k context, text & vision).
    /// </summary>
    public static readonly ChatModel ModelMinistral8b2512 = new ChatModel("ministral-8b-2512", LLmProviders.Mistral, 256_000, [ "ministral-8b-latest" ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelMinistral8b2512"/>
    /// </summary>
    public readonly ChatModel Ministral8b2512 = ModelMinistral8b2512;
    
    /// <summary>
    /// Ministral 14B 2512 — largest Ministral 3 model (256k context, text & vision).
    /// </summary>
    public static readonly ChatModel ModelMinistral14b2512 = new ChatModel("ministral-14b-2512", LLmProviders.Mistral, 256_000, [ "ministral-14b-latest" ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelMinistral14b2512"/>
    /// </summary>
    public readonly ChatModel Ministral14b2512 = ModelMinistral14b2512;
    
    /// <summary>
    /// Mistral Small 3.2
    /// </summary>
    [Obsolete("Retired July 31, 2026 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public static readonly ChatModel ModelMistralSmall2506 = new ChatModel("mistral-small-2506", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralSmall2506"/>
    /// </summary>
    [Obsolete("Retired July 31, 2026 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public readonly ChatModel MistralSmall2506 = ModelMistralSmall2506;
    
    /// <summary>
    /// Our small reasoning model released September 2025 with vision support.
    /// </summary>
    [Obsolete("Retired July 31, 2026 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public static readonly ChatModel ModelMagistralSmall2509 = new ChatModel("magistral-small-2509", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMagistralSmall2509"/>
    /// </summary>
    [Obsolete("Retired July 31, 2026 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public readonly ChatModel MagistralSmall2509 = ModelMagistralSmall2509;
    
    /// <summary>
    /// Building upon Mistral Small 3.1 (2503), with added reasoning capabilities, undergoing SFT from Magistral Medium traces and RL on top, it's a small, efficient reasoning model with 24B parameters.
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public static readonly ChatModel ModelMagistralSmall2507 = new ChatModel("magistral-small-2507", LLmProviders.Mistral, 40_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMagistralSmall2507"/>
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public readonly ChatModel MagistralSmall2507 = ModelMagistralSmall2507;
    
    /// <summary>
    /// Building upon Mistral Small 3.1 (2503), with added reasoning capabilities, undergoing SFT from Magistral Medium traces and RL on top, it's a small, efficient reasoning model with 24B parameters.
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public static readonly ChatModel ModelMagistralSmall2506 = new ChatModel("magistral-small-2506", LLmProviders.Mistral, 40_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMagistralSmall2506"/>
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public readonly ChatModel MagistralSmall2506 = ModelMagistralSmall2506;
    
    /// <summary>
    /// A 24B text model, open source model that excels at using tools to explore codebases, editing multiple files and power software engineering agents.
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public static readonly ChatModel ModelDevstralSmall2505 = new ChatModel("devstral-small-2505", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelDevstralSmall2505"/>
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public readonly ChatModel DevstralSmall = ModelDevstralSmall2505;
    
    /// <summary>
    /// <summary>
    /// Alias for the latest Small family snapshot. Currently Mistral Small 4 (<c>mistral-small-2603</c>): hybrid instruct, reasoning, and coding with 256k context.
    /// </summary>
    public static readonly ChatModel ModelMistralSmall = new ChatModel("mistral-small-latest", LLmProviders.Mistral, 256_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralSmall"/>
    /// </summary>
    public readonly ChatModel MistralSmall = ModelMistralSmall;
    
    /// <summary>
    /// A new leader in the small models category with image understanding capabilities, with the lastest version v3.1 released March 2025.
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public static readonly ChatModel ModelMistralSmall2503 = new ChatModel("mistral-small-2503", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralSmall2503"/>
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public readonly ChatModel MistralSmall2503 = ModelMistralSmall2503;
    
    /// <summary>
    /// A 12B model with image understanding capabilities in addition to text. 
    /// </summary>
    [Obsolete("Retired December 31, 2025 on the Mistral API. Use ChatModel.Mistral.Free.Ministral14b2512 instead.")]
    public static readonly ChatModel ModelPixtral = new ChatModel("pixtral-12b-2409", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelPixtral"/>
    /// </summary>
    [Obsolete("Retired December 31, 2025 on the Mistral API. Use ChatModel.Mistral.Free.Ministral14b2512 instead.")]
    public readonly ChatModel Pixtral = ModelPixtral;
    
    /// <summary>
    /// Mistral Small Creative - Labs model released December 2025 for creative writing tasks.
    /// </summary>
    [Obsolete("Retired April 30, 2026 on the Mistral API. Use ChatModel.Mistral.Free.Ministral8b2512 instead.")]
    public static readonly ChatModel ModelMistralSmallCreative = new ChatModel("labs-mistral-small-creative", LLmProviders.Mistral, 32_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralSmallCreative"/>
    /// </summary>
    [Obsolete("Retired April 30, 2026 on the Mistral API. Use ChatModel.Mistral.Free.Ministral8b2512 instead.")]
    public readonly ChatModel MistralSmallCreative = ModelMistralSmallCreative;
    
    /// <summary>
    /// All known Free models from Mistral.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelMistralSmall2503, ModelMistralSmall, ModelMistralSmall2603, ModelPixtral, ModelDevstralSmall2505, ModelMagistralSmall2506, ModelMagistralSmall2507, 
        ModelMistralSmall2506, ModelDevstralSmall2507, ModelDevstralSmall2512, ModelVoxtralSmall2507, ModelVoxtralMini2507, ModelMagistralSmall2509,
        ModelMistralLarge2512, ModelMinistral3b2512, ModelMinistral8b2512, ModelMinistral14b2512, ModelMistralSmallCreative, ModelLeanstral15, ModelLeanstral2603
    ]);

    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;

    internal ChatModelMistralFree()
    {

    }
}