using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models.Mistral;

/// <summary>
/// All Premier (closed-weights) models from Mistral.
/// </summary>
public class ChatModelMistralPremier : IVendorModelClassProvider
{
    /// <summary>
    /// Mistral Medium 3.5 - frontier-class multimodal model released April 2026.
    /// Unifies instruct, reasoning, and coding with adjustable <c>reasoning_effort</c> (<c>none</c> / <c>high</c>). 256k context, Modified MIT.
    /// </summary>
    public static readonly ChatModel ModelMistralMedium35 = new ChatModel("mistral-medium-3-5", LLmProviders.Mistral, 256_000, [ "mistral-medium-latest", "mistral-medium-3" ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralMedium35"/>
    /// </summary>
    public readonly ChatModel MistralMedium35 = ModelMistralMedium35;
    
    /// <summary>
    /// Mistral Medium 3.1 - our frontier-class multimodal model released August 2025.
    /// </summary>
    [Obsolete("Retired August 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public static readonly ChatModel ModelMistralMedium2508 = new ChatModel("mistral-medium-2508", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralMedium2508"/>
    /// </summary>
    [Obsolete("Retired August 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public readonly ChatModel MistralMedium2508 = ModelMistralMedium2508;
    
    /// <summary>
    /// An enterprise grade text model, that excels at using tools to explore codebases, editing multiple files and power software engineering agents.
    /// </summary>
    [Obsolete("Retired May 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public static readonly ChatModel ModelDevstralMedium2507 = new ChatModel("devstral-medium-2507", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelDevstralMedium2507"/>
    /// </summary>
    [Obsolete("Retired May 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public readonly ChatModel DevstralMedium2507 = ModelDevstralMedium2507;
    
    /// <summary>
    /// Devstral 2 (123B) - Next-generation coding model for software development. Modified MIT license.
    /// </summary>
    [Obsolete("Retired July 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public static readonly ChatModel ModelDevstral2512 = new ChatModel("devstral-2512", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelDevstral2512"/>
    /// </summary>
    [Obsolete("Retired July 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public readonly ChatModel Devstral2512 = ModelDevstral2512;
    
    /// <summary>
    /// Our frontier-class reasoning model released June 2025. 
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public static readonly ChatModel ModelMagistralMedium2507 = new ChatModel("magistral-medium-2507", LLmProviders.Mistral, 40_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMagistralMedium2507"/>
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public readonly ChatModel MagistralMedium2507 = ModelMagistralMedium2507;
    
    /// <summary>
    /// Our frontier-class reasoning model released September 2025 with vision support. 
    /// </summary>
    [Obsolete("Retired July 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public static readonly ChatModel ModelMagistralMedium2509 = new ChatModel("magistral-medium-2509", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMagistralMedium2509"/>
    /// </summary>
    [Obsolete("Retired July 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public readonly ChatModel MagistralMedium2509 = ModelMagistralMedium2509;
    
    /// <summary>
    /// Our frontier-class reasoning model released June 2025. 
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public static readonly ChatModel ModelMagistralMedium2506 = new ChatModel("magistral-medium-2506", LLmProviders.Mistral, 40_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMagistralMedium2506"/>
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public readonly ChatModel MagistralMedium2506 = ModelMagistralMedium2506;
    
    /// <summary>
    /// Our cutting-edge language model for coding released end of July 2025, Codestral specializes in low-latency, high-frequency tasks such as fill-in-the-middle (FIM), code correction and test generation.
    /// </summary>
    public static readonly ChatModel ModelCodestral2508 = new ChatModel("codestral-2508", LLmProviders.Mistral, 256_000, [ "codestral-latest" ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelCodestral2508"/>
    /// </summary>
    public readonly ChatModel Codestral2508 = ModelCodestral2508;
    
    /// <summary>
    /// Our cutting-edge language model for coding with the second version released January 2025, Codestral specializes in low-latency, high-frequency tasks such as fill-in-the-middle (FIM), code correction and test generation.
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Premier.Codestral2508 instead.")]
    public static readonly ChatModel ModelCodestral2501 = new ChatModel("codestral-2501", LLmProviders.Mistral, 256_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelCodestral2501"/>
    /// </summary>
    [Obsolete("Retired November 30, 2025 on the Mistral API. Use ChatModel.Mistral.Premier.Codestral2508 instead.")]
    public readonly ChatModel Codestral2501 = ModelCodestral2501;
    
    /// <summary>
    /// Our top-tier reasoning model for high-complexity tasks with the lastest version released November 2024. 
    /// </summary>
    [Obsolete("Retired May 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public static readonly ChatModel ModelMistralLarge = new ChatModel("mistral-large-2411", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralLarge"/>
    /// </summary>
    [Obsolete("Retired May 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public readonly ChatModel MistralLarge = ModelMistralLarge;
    
    /// <summary>
    /// Our frontier-class multimodal model released November 2024.
    /// </summary>
    [Obsolete("Retired May 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public static readonly ChatModel ModelPixtralLarge = new ChatModel("pixtral-large-2411", LLmProviders.Mistral, 128_000, [ "pixtral-large-latest" ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelPixtralLarge"/>
    /// </summary>
    [Obsolete("Retired May 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public readonly ChatModel PixtralLarge = ModelPixtralLarge;
    
    /// <summary>
    /// A powerful and efficient model for languages from the Middle East and South Asia.
    /// </summary>
    [Obsolete("Retired September 30, 2025 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public static readonly ChatModel ModelMistralSaba = new ChatModel("mistral-saba-2502", LLmProviders.Mistral, 32_000, [ "mistral-saba-latest" ]);
    
    /// <summary>
    /// <inheritdoc cref="ModelMistralSaba"/>
    /// </summary>
    [Obsolete("Retired September 30, 2025 on the Mistral API. Use ChatModel.Mistral.Free.MistralSmall4 instead.")]
    public readonly ChatModel MistralSaba = ModelMistralSaba;
    
    /// <summary>
    /// World’s best edge model.
    /// </summary>
    [Obsolete("Retired December 31, 2025 on the Mistral API. Use ChatModel.Mistral.Free.Ministral3b2512 instead.")]
    public static readonly ChatModel ModelMinistral3B = new ChatModel("ministral-3b-2410", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMinistral3B"/>
    /// </summary>
    [Obsolete("Retired December 31, 2025 on the Mistral API. Use ChatModel.Mistral.Free.Ministral3b2512 instead.")]
    public readonly ChatModel Ministral3B = ModelMinistral3B;
    
    /// <summary>
    /// Powerful edge model with extremely high performance/price ratio.
    /// </summary>
    [Obsolete("Retired December 31, 2025 on the Mistral API. Use ChatModel.Mistral.Free.Ministral8b2512 instead.")]
    public static readonly ChatModel ModelMinistral8B = new ChatModel("ministral-8b-2410", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMinistral8B"/>
    /// </summary>
    [Obsolete("Retired December 31, 2025 on the Mistral API. Use ChatModel.Mistral.Free.Ministral8b2512 instead.")]
    public readonly ChatModel Ministral8B = ModelMinistral8B;
    
    /// <summary>
    /// Mistral Medium 3: frontier-class multimodal model released May 2025.
    /// </summary>
    [Obsolete("Retired August 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public static readonly ChatModel ModelMedium3 = new ChatModel("mistral-medium-2505", LLmProviders.Mistral, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelMedium3"/>
    /// </summary>
    [Obsolete("Retired August 31, 2026 on the Mistral API. Use ChatModel.Mistral.Premier.MistralMedium35 instead.")]
    public readonly ChatModel Medium3 = ModelMedium3;
    
    /// <summary>
    /// All known Premier models from Mistral.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelMistralMedium35, ModelMistralLarge, ModelPixtralLarge, ModelMistralSaba, ModelMinistral3B, ModelMinistral8B, ModelMedium3, 
        ModelMagistralMedium2506, ModelMagistralMedium2507, ModelDevstralMedium2507, ModelDevstral2512, ModelCodestral2501, ModelCodestral2508, 
        ModelMistralMedium2508, ModelMagistralMedium2509
    ]);

    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;

    internal ChatModelMistralPremier()
    {

    }
}