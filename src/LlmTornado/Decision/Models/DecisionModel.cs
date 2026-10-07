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
    public static readonly DecisionModelTypeSafe TypeSafe = new();

    /// <summary>Decision models provided by OpenRouter.</summary>
    public static readonly DecisionModelOpenRouter OpenRouter = new();
    
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
        AllModels =
        [
            ..TypeSafe.AllModels,
            ..OpenRouter.AllModels
        ];
        
        AllModels.ForEach(x =>
        {
            AllModelsMap.TryAdd(x.Name, x);
        });
    }
    
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
