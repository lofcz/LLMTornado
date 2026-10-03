using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Audio.Models.OpenRouter;

/// <summary>
/// Known audio models from OpenRouter.
/// </summary>
public class AudioModelOpenRouter : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.OpenRouter;

    /// <summary>Text-to-speech models.</summary>
    public readonly AudioModelOpenRouterSpeech Speech = new AudioModelOpenRouterSpeech();

    /// <summary>All known audio models from OpenRouter.</summary>
    public static List<IModel> ModelsAll => AudioModelOpenRouterSpeech.ModelsAll;

    /// <inheritdoc cref="BaseVendorModelProvider.AllModels"/>
    public override List<IModel> AllModels => ModelsAll;

    /// <summary>Names of the models owned by OpenRouter.</summary>
    public static HashSet<string> AllModelsMap => LazyAllModelsMap.Value;

    private static readonly Lazy<HashSet<string>> LazyAllModelsMap = new Lazy<HashSet<string>>(() =>
    {
        HashSet<string> map = [];
        ModelsAll.ForEach(model => map.Add(model.Name));
        return map;
    });

    /// <inheritdoc cref="BaseVendorModelProvider.OwnsModel"/>
    public override bool OwnsModel(string model) => AllModelsMap.Contains(model);

    internal AudioModelOpenRouter()
    {
    }
}
