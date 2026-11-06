using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// GPT-4 class models from OpenAI.
/// </summary>
public class ChatModelOpenAiGpt4 : IVendorModelClassProvider
{
    /// <summary>
    /// GPT-4o Search Preview is a specialized model trained to understand and execute web search queries with the Chat Completions API.
    /// </summary>
    public static readonly ChatModel ModelOSearchPreview = new ChatModel("gpt-4o-search-preview", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelOSearchPreview"/>
    /// </summary>
    public readonly ChatModel OSearchPreview = ModelOSearchPreview;
    
    /// <summary>
    /// GPT-4o mini Search Preview is a specialized model trained to understand and execute web search queries with the Chat Completions API. 
    /// </summary>
    public static readonly ChatModel ModelOMiniSearchPreview = new ChatModel("gpt-4o-mini-search-preview", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelOMiniSearchPreview"/>
    /// </summary>
    public readonly ChatModel OMiniSearchPreview = ModelOMiniSearchPreview;
    
    /// <summary>
    /// Latest o1 model snapshot.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelO1Pro = new ChatModel("o1-pro", LLmProviders.OpenAi, 200_000)
    {
        EndpointCapabilities = [ ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Batch ]
    };
    
    /// <summary>
    /// <inheritdoc cref="ModelO1Pro"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel O1Pro = ModelO1Pro;
    
    /// <summary>
    /// Latest o1 model snapshot.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelO1241217 = new ChatModel("o1-2024-12-17", LLmProviders.OpenAi, 200_000);

    /// <summary>
    /// <inheritdoc cref="ModelO1241217"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel O1241217 = ModelO1241217;
    
    /// <summary>
    /// Points to the most recent snapshot of the o1 model: o1-preview-2024-09-12.
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelO1 = new ChatModel("o1-preview", LLmProviders.OpenAi, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelO1"/>
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel O1 = ModelO1;
    
    /// <summary>
    /// O1 Snaphot from 24/09/12.
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelO1240912 = new ChatModel("o1-preview-2024-09-12", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelO1240912"/>
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel O1240912 = ModelO1240912;
    
    /// <summary>
    /// Points to the most recent o1-mini snapshot: o1-mini-2024-09-12
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public static readonly ChatModel ModelO1Mini = new ChatModel("o1-mini", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelO1Mini"/>
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public readonly ChatModel O1Mini = ModelO1Mini;
    
    /// <summary>
    /// Points to the most recent o1-mini snapshot: o1-mini-2024-09-12
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public static readonly ChatModel ModelO1Mini240912 = new ChatModel("o1-mini-2024-09-12", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelO1Mini240912"/>
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Terra instead.")]
    public readonly ChatModel O1Mini240912 = ModelO1Mini240912;
    
    /// <summary>
    /// GPT-4o mini (“o” for “omni”) is our most advanced model in the small models category, and our cheapest model yet. It is multimodal (accepting text or image inputs and outputting text), has higher intelligence than gpt-3.5-turbo but is just as fast. It is meant to be used for smaller tasks, including vision tasks.
    /// </summary>
    public static readonly ChatModel ModelOMini = new ChatModel("gpt-4o-mini", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelOMini"/>
    /// </summary>
    public readonly ChatModel OMini = ModelOMini;
    
    /// <summary>
    /// <see cref="ModelOMini"/> currently points to this version.
    /// </summary>
    public static readonly ChatModel ModelOMini240718 = new ChatModel("gpt-4o-mini-2024-07-18", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelOMini"/>
    /// </summary>
    public readonly ChatModel OMini240718 = ModelOMini240718;
    
    /// <summary>
    /// Most advanced, multimodal flagship model that’s cheaper and faster than GPT-4 Turbo. Currently points to gpt-4o-2024-05-13.
    /// </summary>
    public static readonly ChatModel ModelO = new ChatModel("gpt-4o", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelO"/>
    /// </summary>
    public readonly ChatModel O = ModelO;
    
    /// <summary>
    /// Most advanced, multimodal flagship model that’s cheaper and faster than GPT-4 Turbo. gpt-4o currently points to this version.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelO240513 = new ChatModel("gpt-4o-2024-05-13", LLmProviders.OpenAi, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelO240513"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel O240513 = ModelO240513;
    
    /// <summary>
    /// Most advanced, multimodal flagship model that’s cheaper and faster than GPT-4 Turbo. gpt-4o currently points to this version. Supports structured JSON.
    /// </summary>
    public static readonly ChatModel ModelO240806 = new ChatModel("gpt-4o-2024-08-06", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelO240806"/>
    /// </summary>
    public readonly ChatModel O240806 = ModelO240806;
    
    /// <summary>
    /// Latest gpt-4o snapshot from November 20th, 2024.
    /// </summary>
    public static readonly ChatModel ModelO241120 = new ChatModel("gpt-4o-2024-11-20", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelO241120"/>
    /// </summary>
    public readonly ChatModel O241120 = ModelO241120;
    
    /// <summary>
    /// Preview release for audio inputs in chat completions.
    /// </summary>
    public static readonly ChatModel ModelAudioPreview = new ChatModel("gpt-4o-audio-preview", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelAudioPreview"/>
    /// </summary>
    public readonly ChatModel AudioPreview = ModelAudioPreview;
    
    /// <summary>
    /// Current snapshot for the Audio API model.
    /// </summary>
    public static readonly ChatModel ModelAudioPreview241001 = new ChatModel("gpt-4o-audio-preview-2024-10-01", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelAudioPreview241001"/>
    /// </summary>
    public readonly ChatModel AudioPreview241001 = ModelAudioPreview241001;
    
    /// <summary>
    /// Snapshot for the Audio API model.
    /// </summary>
    public static readonly ChatModel ModelAudioPreview241217 = new ChatModel("gpt-4o-audio-preview-2024-12-17", LLmProviders.OpenAi, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelAudioPreview241217"/>
    /// </summary>
    public readonly ChatModel AudioPreview241217 = ModelAudioPreview241217;
    
    /// <summary>
    /// Newest snapshot for the Audio API model.
    /// </summary>
    public static readonly ChatModel ModelAudioPreview250603 = new ChatModel("gpt-4o-audio-preview-2025-06-03", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelAudioPreview250603"/>
    /// </summary>
    public readonly ChatModel AudioPreview250603 = ModelAudioPreview250603;
    
    /// <summary>
    /// Dynamic model continuously updated to the current version of GPT-4o in ChatGPT.
    /// </summary>
    public static readonly ChatModel ModelChatGptLatest = new ChatModel("chatgpt-4o-latest", LLmProviders.OpenAi, 128_000);
    
    /// <summary>
    /// <inheritdoc cref="ModelChatGptLatest"/>
    /// </summary>
    public readonly ChatModel ChatGptLatest = ModelChatGptLatest;
    
    /// <summary>
    /// The latest GPT-4 Turbo model with vision capabilities. Vision requests can now use JSON mode and function calling. Currently points to gpt-4-turbo-2024-04-09.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelTurbo = new ChatModel("gpt-4-turbo", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelTurbo"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel Turbo = ModelTurbo;
    
    /// <summary>
    /// GPT-4 Turbo with Vision model. Vision requests can now use JSON mode and function calling. gpt-4-turbo currently points to this version.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelTurbo240409 = new ChatModel("gpt-4-turbo-2024-04-09", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelTurbo240409"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel Turbo240409 = ModelTurbo240409;
    
    /// <summary>
    /// GPT-4 Turbo preview model intended to reduce cases of “laziness” where the model doesn’t complete a task. Returns a maximum of 4,096 output tokens.
    /// </summary>
    [Obsolete("Retired March 26, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelPreview240125 = new ChatModel("gpt-4-0125-preview", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelPreview240125"/>
    /// </summary>
    [Obsolete("Retired March 26, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel Preview240125 = ModelPreview240125;
    
    /// <summary>
    /// GPT-4 Turbo preview model featuring improved instruction following, JSON mode, reproducible outputs, parallel function calling, and more. Returns a maximum of 4,096 output tokens. This is a preview model.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelPreview231106 = new ChatModel("gpt-4-1106-preview", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelPreview231106"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel Preview231106 = ModelPreview231106;
    
    /// <summary>
    /// GPT-4 model with the ability to understand images, in addition to all other GPT-4 Turbo capabilities. This is a preview model, we recommend developers to now use gpt-4-turbo which includes vision capabilities. Currently points to gpt-4-1106-vision-preview.
    /// </summary>
    [Obsolete("Disabled by OpenAI")]
    public static readonly ChatModel ModelVisionPreview = new ChatModel("gpt-4-vision-preview", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelVisionPreview"/>
    /// </summary>
    public readonly ChatModel VisionPreview = ModelVisionPreview;
    
    /// <summary>
    /// GPT-4 model with the ability to understand images, in addition to all other GPT-4 Turbo capabilities. This is a preview model, we recommend developers to now use gpt-4-turbo which includes vision capabilities. Returns a maximum of 4,096 output tokens.
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelVisionPreview231106 = new ChatModel("gpt-4-1106-vision-preview", LLmProviders.OpenAi, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelVisionPreview231106"/>
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel VisionPreview231106 = ModelVisionPreview231106;
    
    /// <summary>
    /// Currently points to gpt-4.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelDefault = new ChatModel("gpt-4", LLmProviders.OpenAi, 8_192);

    /// <summary>
    /// <inheritdoc cref="ModelDefault"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel Default = ModelDefault;
    
    /// <summary>
    /// Snapshot of gpt-4 from June 13th 2023 with improved function calling support.
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelPreview230613 = new ChatModel("gpt-4-0613", LLmProviders.OpenAi, 8_192);

    /// <summary>
    /// <inheritdoc cref="ModelPreview230613"/>
    /// </summary>
    [Obsolete("Retired October 23, 2026 on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel Preview230613 = ModelPreview230613;
    
    /// <summary>
    /// Currently points to gpt-4-32k-0613.
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelContext32K = new ChatModel("gpt-4-32k", LLmProviders.OpenAi, 32_768);

    /// <summary>
    /// <inheritdoc cref="ModelContext32K"/>
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel Context32K = ModelContext32K;
    
    /// <summary>
    /// Snapshot of gpt-4-32k from June 13th 2023 with improved function calling support. This model was never rolled out widely in favor of GPT-4 Turbo.
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public static readonly ChatModel ModelContext32K230613 = new ChatModel("gpt-4-32k-0613", LLmProviders.OpenAi, 32_768);

    /// <summary>
    /// <inheritdoc cref="ModelContext32K230613"/>
    /// </summary>
    [Obsolete("Retired on the OpenAI API. Use ChatModel.OpenAi.Gpt56.V56Sol instead.")]
    public readonly ChatModel Context32K230613 = ModelContext32K230613;
    
    /// <summary>
    /// All known GPT 4 models from OpenAI.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [ModelOMini, ModelOMini240718, ModelO, ModelO240513, ModelO240806, ModelO241120, ModelChatGptLatest, ModelTurbo, ModelTurbo240409, ModelPreview240125, ModelPreview231106, ModelVisionPreview231106, ModelDefault, ModelPreview230613, ModelContext32K, ModelContext32K230613, ModelO1Pro, ModelO1, ModelO1240912, ModelO1Mini, ModelO1Mini240912, ModelAudioPreview, ModelAudioPreview241001, ModelO1241217, ModelAudioPreview241217, ModelOSearchPreview, ModelOMiniSearchPreview, ModelAudioPreview250603]);

    /// <summary>
    /// Models using max_completion_tokens instead of max_tokens.
    /// </summary>
    public static HashSet<IModel> ReasoningModels => LazyReasoningModels.Value;

    private static readonly Lazy<HashSet<IModel>> LazyReasoningModels = new Lazy<HashSet<IModel>>(() =>
    [
        ..ChatModelOpenAiO3.ModelsAll, ModelO1Pro,
        ModelO1,
        ModelO1240912,
        ModelO1Mini,
        ModelO1Mini240912,
        ModelO1241217
    ]);
    
    /// <summary>
    /// Models with audio capability.
    /// </summary>
    public static HashSet<IModel> AudioModels => LazyAudioModels.Value;

    private static readonly Lazy<HashSet<IModel>> LazyAudioModels = new Lazy<HashSet<IModel>>(() => [ModelAudioPreview, ModelAudioPreview241001, ModelAudioPreview241217, ModelAudioPreview250603]);

    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;
    
    internal ChatModelOpenAiGpt4()
    {
        
    }
}