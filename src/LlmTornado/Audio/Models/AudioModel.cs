using System;
using System.Collections.Generic;
using System.Linq;
using LlmTornado.Audio.Models.Cohere;
using LlmTornado.Audio.Models.Google;
using LlmTornado.Audio.Models.Groq;
using LlmTornado.Audio.Models.MiniMax;
using LlmTornado.Audio.Models.Mistral;
using LlmTornado.Audio.Models.OpenAi;
using LlmTornado.Audio.Models.OpenRouter;
using LlmTornado.Audio.Models.XAi;
using LlmTornado.Audio.Models.Zai;
using LlmTornado.Code;
using LlmTornado.Code.Models;
using LlmTornado.Models;
using Newtonsoft.Json;

namespace LlmTornado.Audio.Models;

/// <summary>
/// Models supporting text based inference, such as chat or completions
/// </summary>
public class AudioModel : ModelBase
{
    /// <summary>
    /// Models from OpenAI.
    /// </summary>
    public static readonly AudioModelOpenAi OpenAi = new AudioModelOpenAi();

    /// <summary>
    /// Models provided by OpenRouter.
    /// </summary>
    public static readonly AudioModelOpenRouter OpenRouter = new AudioModelOpenRouter();
    
    /// <summary>
    /// Models provided by Groq.
    /// </summary>
    public static readonly AudioModelGroq Groq = new AudioModelGroq();
    
    /// <summary>
    /// Models provided by Mistral.
    /// </summary>
    public static readonly AudioModelMistral Mistral = new AudioModelMistral();
    
    /// <summary>
    /// Models provided by Z.AI.
    /// </summary>
    public static readonly AudioModelZai Zai = new AudioModelZai();
    
    /// <summary>
    /// Models provided by MiniMax.
    /// </summary>
    public static readonly AudioModelMiniMax MiniMax = new AudioModelMiniMax();
    
    /// <summary>
    /// Models provided by Google.
    /// </summary>
    public static readonly AudioModelGoogle Google = new AudioModelGoogle();
    
    /// <summary>
    /// Models provided by Cohere.
    /// </summary>
    public static readonly AudioModelCohere Cohere = new AudioModelCohere();
    
    /// <summary>
    /// Models provided by xAI.
    /// </summary>
    public static readonly AudioModelXAi XAi = new AudioModelXAi();
    
    /// <summary>
    /// All known models keyed by name.
    /// </summary>
    public static Dictionary<string, IModel> AllModelsMap => LazyAllModelsMap.Value;

    private static readonly Lazy<Dictionary<string, IModel>> LazyAllModelsMap = new Lazy<Dictionary<string, IModel>>(() =>
    {
        Dictionary<string, IModel> map = [];

        AllModels.ForEach(x =>
        {
            map.TryAdd(x.Name, x);
        });

        return map;
    });

    /// <summary>
    /// All known audio models.
    /// </summary>
    public static List<IModel> AllModels => LazyAllModels.Value;
    
    private static readonly Lazy<List<IModel>> LazyAllModels = new Lazy<List<IModel>>(() => AllProviders.SelectMany(x => x.AllModels).ToList());

    /// <summary>
    /// All known audio model providers.
    /// </summary>
    public static List<BaseVendorModelProvider> AllProviders => LazyAllProviders.Value;

    private static readonly Lazy<List<BaseVendorModelProvider>> LazyAllProviders = new Lazy<List<BaseVendorModelProvider>>(() => [
        OpenAi, OpenRouter, Groq, Mistral, Zai, MiniMax, Google, Cohere, XAi
    ]);
    
    /// <summary>
    /// Represents a Model with the given name.
    /// </summary>
    /// <param name="name">The id/name of the model.</param>
    /// <param name="ownedBy"></param>
    /// <param name="provider"></param>
    public AudioModel(string name, string? ownedBy = null, LLmProviders? provider = null)
    {
        Name = name;
        OwnedBy = ownedBy ?? "openai";
        Provider = provider ?? GetProvider(name) ?? LLmProviders.OpenAi;
    }

    /// <summary>
    /// Creates a new model identified by name and provider.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="provider"></param>
    public AudioModel(string name, LLmProviders provider)
    {
        Name = name;
        Provider = provider;
    }
    
    /// <summary>
    /// Creates a new model identified by name and provider.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="provider"></param>
    /// <param name="contextTokens"></param>
    public AudioModel(string name, LLmProviders provider, int contextTokens, List<string>? aliases = null)
    {
        Name = name;
        Provider = provider;
        ContextTokens = contextTokens;
        Aliases = aliases ?? [];
    }
    
    /// <summary>
    /// Creates a new model identified by name. The provider of the model is inferred automatically.
    /// </summary>
    /// <param name="name"></param>
    public AudioModel(string name)
    {
        Name = name;
        Provider = GetProvider(name) ?? LLmProviders.OpenAi;
    }

    /// <summary>
    /// Represents a generic model.
    /// </summary>
    public AudioModel()
    {
    }
    
    /// <summary>
    /// Allows a model to be implicitly cast to the string of its <see cref="ModelBase.Name" />
    /// </summary>
    /// <param name="model">The <see cref="AudioModel" /> to cast to a string.</param>
    public static implicit operator string(AudioModel model)
    {
        return model.Name;
    }

    /// <summary>
    /// Looks up the model provider. Only works for known models.
    /// </summary>
    /// <param name="modelName"></param>
    /// <returns></returns>
    public static LLmProviders? GetProvider(string? modelName)
    {
        if (modelName is not null && AllModelsMap.TryGetValue(modelName, out IModel? protoModel))
        {
            return protoModel.Provider;
        }

        return null;
    }
    
    /// <summary>
    /// Allows a string to be implicitly cast as an <see cref="Model" /> with that <see cref="IModel.Name" />
    /// </summary>
    /// <param name="name">The id/<see cref="IModel.Name" /> to use</param>
    public static implicit operator AudioModel(string? name)
    {
        return new AudioModel(name ?? string.Empty, name is null ? LLmProviders.OpenAi : GetProvider(name) ?? LLmProviders.OpenAi);
    }
}

internal class AudioModelJsonConverter : JsonConverter<AudioModel>
{
    public override void WriteJson(JsonWriter writer, AudioModel? value, JsonSerializer serializer)
    {
        writer.WriteValue(value?.GetApiName);
    }

    public override AudioModel? ReadJson(JsonReader reader, Type objectType, AudioModel? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        return existingValue;
    }
}
