using System.Collections.Generic;
using System.Linq;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Decision.Models.TypeSafe;

/// <summary>
/// Jev: TypeSafe's flagship System One model. Evaluates typed questions (choice, score, noul) against a state
/// and returns calibrated probabilities. Text only, 64k tokens per request (32k for state plus the longest question).
/// </summary>
public class DecisionModelTypeSafeJev : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.TypeSafe;
    
    /// <summary>
    /// Alias of the most recent stable, official Jev release. Moves when a new release ships.
    /// </summary>
    public static readonly DecisionModel ModelLatest = DecisionModel.GetRegisteredModel("jev-latest");

    /// <summary>
    /// <inheritdoc cref="ModelLatest"/>
    /// </summary>
    public readonly DecisionModel Latest = DecisionModel.GetRegisteredModel("jev-latest");
    
    /// <summary>
    /// Alias of the most recent Jev release, whether or not it is an official one. Moves ahead of <see cref="Latest"/> when a preview build is available.
    /// </summary>
    public static readonly DecisionModel ModelPreview = DecisionModel.GetRegisteredModel("jev-preview");
    
    /// <summary>
    /// <inheritdoc cref="ModelPreview"/>
    /// </summary>
    public readonly DecisionModel Preview = DecisionModel.GetRegisteredModel("jev-preview");
    
    /// <summary>
    /// Jev 1.13, pinned version. Prefer pinning when confidence thresholds were tuned against a specific version.
    /// </summary>
    public static readonly DecisionModel ModelV1_13 = DecisionModel.GetRegisteredModel("jev-1.13.0");
    
    /// <summary>
    /// <inheritdoc cref="ModelV1_13"/>
    /// </summary>
    public readonly DecisionModel V1_13 = DecisionModel.GetRegisteredModel("jev-1.13.0");
    
    /// <summary>
    /// All known Jev models.
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
    /// All known Jev models.
    /// </summary>
    public static readonly List<IModel> ModelsAll =
    [
        ModelLatest,
        ModelPreview,
        ModelV1_13
    ];

    static DecisionModelTypeSafeJev()
    {
        AllModelsMap = new HashSet<string>(ModelsAll.Select(x => x.Name));
    }
    
    internal DecisionModelTypeSafeJev()
    {
        
    }
}
