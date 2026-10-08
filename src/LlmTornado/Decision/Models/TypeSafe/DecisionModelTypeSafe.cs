using System.Collections.Generic;
using System.Linq;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Decision.Models.TypeSafe;

/// <summary>
/// Known decision models from TypeSafe.
/// </summary>
public class DecisionModelTypeSafe : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.TypeSafe;
    
    /// <summary>
    /// Jev models (System One).
    /// </summary>
    public readonly DecisionModelTypeSafeJev Jev = new DecisionModelTypeSafeJev();
    
    /// <summary>
    /// All known decision models from TypeSafe.
    /// </summary>
    public override List<IModel> AllModels => ModelsAll;
    
    /// <summary>
    /// Checks whether the model is owned by the provider.
    /// </summary>
    public override bool OwnsModel(string model)
    {
        return AllModelsMap.Contains(model);
    }

    /// <summary>
    /// Map of models owned by the provider.
    /// </summary>
    public static readonly HashSet<string> AllModelsMap;
    
    /// <summary>
    /// <inheritdoc cref="AllModels"/>
    /// </summary>
    public static readonly List<IModel> ModelsAll = DecisionModel.AllModels.Where(model => model.Provider is LLmProviders.TypeSafe).ToList();
    
    static DecisionModelTypeSafe()
    {
        AllModelsMap = new HashSet<string>(ModelsAll.Select(x => x.Name));
    }
    
    internal DecisionModelTypeSafe()
    {
        
    }
}
