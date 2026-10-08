using System.Collections.Generic;
using LlmTornado.Decision.Models.TypeSafe;
using LlmTornado.Decision.Models.OpenRouter;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Decision.Models;

/// <summary>
/// Models supporting structured decisions (choice, score and noul questions) over a state.
/// </summary>
public class DecisionModel : ModelBase
{
    /// <summary>
    /// Models from TypeSafe.
    /// </summary>
    public static readonly DecisionModelTypeSafe TypeSafe;

    /// <summary>Decision models provided by OpenRouter.</summary>
    public static readonly DecisionModelOpenRouter OpenRouter;
    
    /// <summary>
    /// All known models keyed by name.
    /// </summary>
    public static readonly Dictionary<string, IModel> AllModelsMap = [];

    /// <summary>
    /// All known decision models.
    /// </summary>
    public static readonly List<IModel> AllModels;
    
    static DecisionModel()
    {
        // Own the model instances here. Provider catalogs only reference this completed registry.
        AllModels =
        [
            new DecisionModel("jev-latest", LLmProviders.TypeSafe),
            new DecisionModel("jev-preview", LLmProviders.TypeSafe),
            new DecisionModel("jev-1.13.0", LLmProviders.TypeSafe),
            new DecisionModel("~typesafe/jev-latest", LLmProviders.OpenRouter) { ContextTokens = 32000 },
            new DecisionModel("cloudflare/clef", LLmProviders.OpenRouter) { ContextTokens = 65536 },
            new DecisionModel("cloudflare/clef-flash", LLmProviders.OpenRouter) { ContextTokens = 65536 },
            new DecisionModel("inception/mercury-decide:free", LLmProviders.OpenRouter) { ContextTokens = 32768 },
            new DecisionModel("jaredpalmer/kev-4b", LLmProviders.OpenRouter) { ContextTokens = 8192 },
            new DecisionModel("liquid/d1", LLmProviders.OpenRouter) { ContextTokens = 65536 },
            new DecisionModel("openai/gpt-6-luna-decisions", LLmProviders.OpenRouter) { ContextTokens = 1050000 },
            new DecisionModel("perplexity/pplx-decider-v1-27b", LLmProviders.OpenRouter) { ContextTokens = 262144 },
            new DecisionModel("respan/span-01", LLmProviders.OpenRouter) { ContextTokens = 0 },
            new DecisionModel("respan/span-01-lite", LLmProviders.OpenRouter) { ContextTokens = 0 },
            new DecisionModel("respan/span-01-lite:free", LLmProviders.OpenRouter) { ContextTokens = 0 },
            new DecisionModel("typesafe/jev-1.13", LLmProviders.OpenRouter) { ContextTokens = 32000 },
            new DecisionModel("upstage/solar-decide", LLmProviders.OpenRouter) { ContextTokens = 524288 }
        ];
        foreach (IModel model in AllModels)
        {
            AllModelsMap.Add(model.Name, model);
        }

        TypeSafe = new DecisionModelTypeSafe();
        OpenRouter = new DecisionModelOpenRouter();
    }

    internal static DecisionModel GetRegisteredModel(string name) => (DecisionModel)AllModelsMap[name];
    
    /// <summary>
    /// Represents a Model with the given name.
    /// </summary>
    public DecisionModel(string name, LLmProviders? provider = null)
    {
        Name = name;
        Provider = provider ?? GetProvider(name) ?? LLmProviders.TypeSafe;
    }

    /// <summary>
    /// Represents a generic model.
    /// </summary>
    public DecisionModel()
    {
    }
    
    /// <summary>
    /// Looks up the model provider. Only works for known models.
    /// </summary>
    public static LLmProviders? GetProvider(string? modelName)
    {
        if (modelName is not null && AllModelsMap.TryGetValue(modelName, out IModel? protoModel))
        {
            return protoModel.Provider;
        }

        return null;
    }
    
    /// <summary>
    /// Allows a string to be implicitly cast as an <see cref="DecisionModel" /> with that <see cref="IModel.Name" />
    /// </summary>
    public static implicit operator DecisionModel(string? name)
    {
        return new DecisionModel(name ?? string.Empty, name is null ? LLmProviders.TypeSafe : GetProvider(name) ?? LLmProviders.TypeSafe);
    }
}
