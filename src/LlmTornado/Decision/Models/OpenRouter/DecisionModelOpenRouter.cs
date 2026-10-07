using System.Collections.Generic;
using System.Linq;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Decision.Models.OpenRouter;

/// <summary>Decision models provided by OpenRouter.</summary>
public class DecisionModelOpenRouter : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.OpenRouter;

    /// <summary>All OpenRouter decision model variants.</summary>
    public readonly DecisionModelOpenRouterAll All = new DecisionModelOpenRouterAll();

    /// <summary>All known OpenRouter models for the alpha Decisions endpoint.</summary>
    public static readonly List<IModel> ModelsAll = [..DecisionModelOpenRouterAll.ModelsAll];

    /// <summary>Names of the models owned by this provider.</summary>
    public static readonly HashSet<string> AllModelsMap = new HashSet<string>(ModelsAll.Select(model => model.Name));

    /// <inheritdoc cref="BaseVendorModelProvider.AllModels"/>
    public override List<IModel> AllModels => ModelsAll;

    /// <inheritdoc cref="BaseVendorModelProvider.OwnsModel"/>
    public override bool OwnsModel(string model) => AllModelsMap.Contains(model);

    internal DecisionModelOpenRouter()
    {
    }
}
