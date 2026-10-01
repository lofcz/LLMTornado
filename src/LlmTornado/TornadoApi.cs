using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Assistants;
using LlmTornado.Audio;
using LlmTornado.Batch;
using LlmTornado.Caching;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Models;
using LlmTornado.Completions;
using LlmTornado.Embedding;
using LlmTornado.Files;
using LlmTornado.Images;
using LlmTornado.Videos;
using LlmTornado.Interactions;
using LlmTornado.Live;
using LlmTornado.Webhooks;
using LlmTornado.Models;
using LlmTornado.Moderation;
using LlmTornado.Ocr;
using LlmTornado.Rerank;
using LlmTornado.Decision;
using LlmTornado.Realtime;
using LlmTornado.Responses;
using LlmTornado.Threads;
using LlmTornado.VectorStores;
using LlmTornado.Uploads;
using LlmTornado.Skills;
using LlmTornado.Tokenize;
using LlmTornado.ManagedAgents;
using LlmTornado.ManagedAgents.Anthropic;
using LlmTornado.RateLimits;
using LlmTornado.Common;
using LlmTornado.Compaction;
using LlmTornado.Codex;

namespace LlmTornado;

/// <summary>
///     Entry point to the OpenAPI API, handling auth and allowing access to the various API endpoints
/// </summary>
public class TornadoApi
{
    internal readonly ConcurrentDictionary<LLmProviders, ProviderAuthentication> Authentications = [];
    internal readonly ConcurrentDictionary<LLmProviders, IEndpointProvider> EndpointProviders = [];
    
    private LLmProviders? cachedFirstProvider;
    private IEndpointProvider? cachedFirstEndpointProvider;

    private readonly Lazy<AssistantsEndpoint> assistants;
    private readonly Lazy<AudioEndpoint> audio;
    private readonly Lazy<ChatEndpoint> chat;
    private readonly Lazy<CompletionEndpoint> completion;
    private readonly Lazy<EmbeddingEndpoint> embedding;
    private readonly Lazy<ContextualEmbeddingEndpoint> contextualEmbedding;
    private readonly Lazy<MultimodalEmbeddingEndpoint> multimodalEmbedding;
    private readonly Lazy<FilesEndpoint> files;
    private readonly Lazy<ImageEditEndpoint> imageEdit;
    private readonly Lazy<ImageGenerationEndpoint> imageGeneration;
    private readonly Lazy<ModelsEndpoint> models;
    private readonly Lazy<ModerationEndpoint> moderation;
    private readonly Lazy<ThreadsEndpoint> threads;
    private readonly Lazy<VectorStoresEndpoint> vectorStores;
    private readonly Lazy<CachingEndpoint> caching;
    private readonly Lazy<ResponsesEndpoint> responses;
    private readonly Lazy<UploadsEndpoint> uploads;
    private readonly Lazy<RerankEndpoint> rerank;
    private readonly Lazy<DecisionEndpoint> decision;
    private readonly Lazy<ResponsesConversationEndpoint> responsesConversation;
    private readonly Lazy<SkillsEndpoint> skills;
    private readonly Lazy<OpenAiSkillsEndpoint> openAiSkills;
    private readonly Lazy<TokenizeEndpoint> tokenize;
    private readonly Lazy<VideoGenerationEndpoint> videos;
    private readonly Lazy<BatchEndpoint> batch;
    private readonly Lazy<WebhooksEndpoint> webhooks;
    private readonly Lazy<InteractionsEndpoint> interactions;
    private readonly Lazy<OcrEndpoint> ocr;
    private readonly Lazy<RealtimeEndpoint> realtime;
    private readonly Lazy<LiveEndpoint> live;
    private readonly Lazy<ManagedAgentsEndpoint> managedAgents;
    private readonly Lazy<AnthropicManagedAgentsEndpoint> anthropicManagedAgents;
    private readonly Lazy<AnthropicManagedAgentSessionsEndpoint> anthropicManagedAgentSessions;
    private readonly Lazy<AnthropicManagedAgentEnvironmentsEndpoint> anthropicManagedAgentEnvironments;
    private readonly Lazy<RateLimitsEndpoint> rateLimits;
    private readonly Lazy<CompactionEndpoint> compaction;
    private readonly Lazy<CodexEndpoint> codex;

    /// <summary>
    ///     If true, the API will throw exceptions for non-200 responses.
    /// </summary>
    internal bool HttpStrict { get; set; }
    
    /// <summary>
    ///     If true, enables direct browser access headers for providers that support it (e.g., Anthropic's "anthropic-dangerous-direct-browser-access" header).
    ///     This setting must be explicitly enabled as it may bypass certain security restrictions.
    /// </summary>
    public bool DirectBrowserAccess { get; set; }

    /// <summary>
    ///     Settings applied to outbound HTTP requests.
    /// </summary>
    public TornadoRequestSettings RequestSettings { get; set; } = new TornadoRequestSettings();
    
    /// <summary>
    ///     Creates a new Tornado API without any authentication. Use this with self-hosted models.
    /// </summary>
    public TornadoApi()
    {
        assistants = new Lazy<AssistantsEndpoint>(() => new AssistantsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        audio = new Lazy<AudioEndpoint>(() => new AudioEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        chat = new Lazy<ChatEndpoint>(() => new ChatEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        completion = new Lazy<CompletionEndpoint>(() => new CompletionEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        embedding = new Lazy<EmbeddingEndpoint>(() => new EmbeddingEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        contextualEmbedding = new Lazy<ContextualEmbeddingEndpoint>(() => new ContextualEmbeddingEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        multimodalEmbedding = new Lazy<MultimodalEmbeddingEndpoint>(() => new MultimodalEmbeddingEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        files = new Lazy<FilesEndpoint>(() => new FilesEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        imageEdit = new Lazy<ImageEditEndpoint>(() => new ImageEditEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        imageGeneration = new Lazy<ImageGenerationEndpoint>(() => new ImageGenerationEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        models = new Lazy<ModelsEndpoint>(() => new ModelsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        moderation = new Lazy<ModerationEndpoint>(() => new ModerationEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        threads = new Lazy<ThreadsEndpoint>(() => new ThreadsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        vectorStores = new Lazy<VectorStoresEndpoint>(() => new VectorStoresEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        caching = new Lazy<CachingEndpoint>(() => new CachingEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        responses = new Lazy<ResponsesEndpoint>(() => new ResponsesEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        uploads = new Lazy<UploadsEndpoint>(() => new UploadsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        rerank = new Lazy<RerankEndpoint>(() => new RerankEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        decision = new Lazy<DecisionEndpoint>(() => new DecisionEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        responsesConversation = new Lazy<ResponsesConversationEndpoint>(() => new ResponsesConversationEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        skills = new Lazy<SkillsEndpoint>(() => new SkillsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        openAiSkills = new Lazy<OpenAiSkillsEndpoint>(() => new OpenAiSkillsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        tokenize = new Lazy<TokenizeEndpoint>(() => new TokenizeEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        videos = new Lazy<VideoGenerationEndpoint>(() => new VideoGenerationEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        batch = new Lazy<BatchEndpoint>(() => new BatchEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        webhooks = new Lazy<WebhooksEndpoint>(() => new WebhooksEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        interactions = new Lazy<InteractionsEndpoint>(() => new InteractionsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        ocr = new Lazy<OcrEndpoint>(() => new OcrEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        managedAgents = new Lazy<ManagedAgentsEndpoint>(() => new ManagedAgentsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        anthropicManagedAgents = new Lazy<AnthropicManagedAgentsEndpoint>(() => new AnthropicManagedAgentsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        anthropicManagedAgentSessions = new Lazy<AnthropicManagedAgentSessionsEndpoint>(() => new AnthropicManagedAgentSessionsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        anthropicManagedAgentEnvironments = new Lazy<AnthropicManagedAgentEnvironmentsEndpoint>(() => new AnthropicManagedAgentEnvironmentsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        realtime = new Lazy<RealtimeEndpoint>(() => new RealtimeEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        live = new Lazy<LiveEndpoint>(() => new LiveEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        rateLimits = new Lazy<RateLimitsEndpoint>(() => new RateLimitsEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        compaction = new Lazy<CompactionEndpoint>(() => new CompactionEndpoint(this), LazyThreadSafetyMode.ExecutionAndPublication);
        codex = new Lazy<CodexEndpoint>(() => new CodexEndpoint(), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    ///     Creates a new Tornado API for self-hosted / custom providers, such as Ollama and vLLM.<br/>
    ///     For Ollama use "http://localhost:11434" (by default).
    ///     For vLLM use "http://localhost:8000" (by default).
    /// </summary>
    /// <param name="serverUri">Uri of the server. Tokens {0} and {1} are available for endpoint and action respectively. If provided values doesn't use neither, format /{0}/{1} is used automatically.</param>
    public TornadoApi(Uri serverUri) : this()
    {
        string serverUriStr = serverUri.ToString();

        if (!serverUriStr.Contains("{0}"))
        {
            serverUriStr = $"{serverUriStr}{(serverUriStr.EndsWith('/') ? string.Empty : "/")}{{0}}/{{1}}";
        }

        ApiUrlFormat = serverUriStr;
    }
    
    /// <summary>
    ///     Creates a new Tornado API for self-hosted / custom providers, such as Ollama and vLLM.<br/>
    ///     For Ollama use "http://localhost:11434" (by default).
    ///     For vLLM use "http://localhost:8000" (by default).
    /// </summary>
    /// <param name="serverUri">Uri of the server. Tokens {0} and {1} are available for endpoint and action respectively. If provided values doesn't use neither, format /{0}/{1} is used automatically.</param>
    /// <param name="apiKey">API key to use</param>
    /// <param name="provider">Provider to use</param>
    public TornadoApi(Uri serverUri, string apiKey, LLmProviders provider = LLmProviders.Custom) : this()
    {
        string serverUriStr = serverUri.ToString();

        if (!serverUriStr.Contains("{0}"))
        {
            serverUriStr = $"{serverUriStr}{(serverUriStr.EndsWith('/') ? string.Empty : "/")}{{0}}/{{1}}";
        }

        ApiUrlFormat = serverUriStr;
        Authentications.TryAdd(provider, new ProviderAuthentication(provider, apiKey));
    }

    /// <summary>
    /// Creates an instance from a custom provider.<br/>
    /// This constructor is needed only for custom deployments where setting <see cref="ApiUrlFormat"/> is not sufficient.
    /// </summary>
    /// <param name="provider">The provider to use</param>
    public TornadoApi(IEndpointProvider provider) : this()
    {
        EnlistProvider(provider);
    }
    
    /// <summary>
    /// Creates an instance from a custom provider.<br/>
    /// This constructor is needed only for custom deployments where setting <see cref="ApiUrlFormat"/> is not sufficient.
    /// </summary>
    /// <param name="providers">Providers to use</param>
    public TornadoApi(IEnumerable<IEndpointProvider> providers) : this()
    {
        foreach (IEndpointProvider provider in providers)
        {
            EnlistProvider(provider);
        }
    }

    void EnlistProvider(IEndpointProvider provider)
    {
        provider.Api = this;

        if (provider.Auth is not null)
        {
            provider.Auth.Provider = provider.Provider;
            Authentications.TryAdd(provider.Provider, provider.Auth);
        }
        
        EndpointProviders.TryAdd(provider.Provider, provider);
    }
    
    /// <summary>
    ///     Creates a new Tornado API with a specific provider authentication. Use when the API will be used only with a single provider.
    /// </summary>
    public TornadoApi(LLmProviders provider, string apiKey, string organization) : this()
    {
        Authentications.TryAdd(provider, new ProviderAuthentication(provider, apiKey, organization));
    }
    
    /// <summary>
    ///     Creates a new Tornado API with a specific provider authentication. Use when the API will be used only with a single provider.
    /// </summary>
    public TornadoApi(LLmProviders provider, string apiKey) : this()
    {
        Authentications.TryAdd(provider, new ProviderAuthentication(provider, apiKey));
    }
    
    /// <summary>
    /// Creates a new Tornado API with a specific provider authentication. Use when the API will be used only with a single provider.<br/>
    /// <b>This overload is without API key! Suitable only for self-hosted models and endpoints without authorization, such as <see cref="ModelsEndpoint"/>.</b>
    /// </summary>
    public TornadoApi(LLmProviders provider) : this()
    {
        Authentications.TryAdd(provider, new ProviderAuthentication(provider, string.Empty));
    }
    
    /// <summary>
    ///     Creates a new Tornado API with multiple providers.
    /// </summary>
    public TornadoApi(IEnumerable<ProviderAuthentication> providers) : this()
    {
        foreach (ProviderAuthentication provider in providers)
        {
            Authentications.TryAdd(provider.Provider, provider);
        }
    }
    
    /// <summary>
    ///     Creates a new Tornado API with a specific provider authentication.
    /// </summary>
    public TornadoApi(ProviderAuthentication provider) : this()
    {
        Authentications.TryAdd(provider.Provider, provider);
    }

    /// <summary>
    ///     Create a new Tornado API via API key. Use this constructor if in the lifetime of the object only one provider will be used. The API key should match this provider.
    /// </summary>
    /// <param name="apiKey">API key</param>
    /// <param name="provider">Provider</param>
    public TornadoApi(string apiKey, LLmProviders provider = LLmProviders.OpenAi) : this()
    {
        Authentications.TryAdd(provider, new ProviderAuthentication(provider, apiKey));
    }

    /// <summary>
    ///     Create a new OpenAiApi via API key and organization key, suitable for (Azure) OpenAI.
    /// </summary>
    /// <param name="apiKey">API key</param>
    /// <param name="organizationKey">Organization key</param>
    /// <param name="provider">Provider</param>
    public TornadoApi(string apiKey, string organizationKey, LLmProviders provider = LLmProviders.OpenAi) : this()
    {
        Authentications.TryAdd(provider, new ProviderAuthentication(provider, apiKey, organizationKey));
    }

    /// <summary>
    ///     Gets authentication for a given provider.
    /// </summary>
    /// <returns></returns>
    public ProviderAuthentication? GetProviderAuthentication(LLmProviders provider)
    {
        return Authentications!.GetValueOrDefault(provider, null);
    }
    
    /// <summary>
    ///     Base url for Provider. If null, default specified by the provider is used.
    ///     for OpenAI, should be "https://api.openai.com/{0}/{1}"
    ///     for Azure, should be
    ///     "https://(your-resource-name.openai.azure.com/openai/deployments/(deployment-id)/{1}?api-version={0}"
    ///     this will be formatted as {0} = <see cref="ResolveApiVersion" />, {1} = <see cref="EndpointBase.Endpoint" />
    /// </summary>
    public string? ApiUrlFormat { get; set; }

    /// <summary>
    ///     Version of the Rest Api
    /// </summary>
    public string? ApiVersion { get; set; }

    /// <summary>
    /// Resolves the API version.
    /// </summary>
    public string ResolveApiVersion(string defaultValue = "v1") => ApiVersion ?? defaultValue;
    
    internal IEndpointProvider ResolveProvider(LLmProviders? userSignalledProvider = null)
    {
        return GetProvider(userSignalledProvider ?? GetFirstAuthenticatedProvider());
    }
    
    /// <summary>
    /// Returns a concrete implementation of endpoint provider for a given known provider.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public IEndpointProvider GetProvider(LLmProviders provider)
    {
        if (EndpointProviders.TryGetValue(provider, out IEndpointProvider? p))
        {
            return p;
        }

        if (Authentications.TryGetValue(provider, out _))
        {
            IEndpointProvider newProvider = EndpointProviderConverter.CreateProvider(provider, this);
            EndpointProviders.TryAdd(provider, newProvider);
            return newProvider;   
        }
        
        IEndpointProvider? cachedEndpoint = cachedFirstEndpointProvider;
        
        if (cachedEndpoint is not null && EndpointProviders.ContainsKey(cachedEndpoint.Provider))
        {
            return cachedEndpoint;
        }

        if (!EndpointProviders.IsEmpty)
        {
            IEndpointProvider? firstEndpoint = EndpointProviders.Values.FirstOrDefault();
            
            if (firstEndpoint is not null)
            {
                cachedFirstEndpointProvider = firstEndpoint;
                return firstEndpoint;
            }
        }

        if (!Authentications.IsEmpty)
        {
            LLmProviders firstAuthProvider = GetFirstAuthenticatedProvider();
            if (Authentications.TryGetValue(firstAuthProvider, out ProviderAuthentication? auth))
            {
                IEndpointProvider newDefaultProvider = EndpointProviderConverter.CreateProvider(provider, this);
                newDefaultProvider.Auth = auth;
                EndpointProviders.TryAdd(provider, newDefaultProvider);
                cachedFirstEndpointProvider = newDefaultProvider;
                return newDefaultProvider;
            }
        }
        
        IEndpointProvider newFallbackProvider = EndpointProviderConverter.CreateProvider(provider, this);
        EndpointProviders.TryAdd(provider, newFallbackProvider);
        return newFallbackProvider;   
    }

    /// <summary>
    /// Returns first authenticated provider. This is order-unstable as the underlying storage is a dictionary - if more than one provider is authenticated, you should explicitly set provider in your requests.
    /// </summary>
    /// <returns>Returns first authenticated provider, or <see cref="LLmProviders.OpenAi"/> as fallback</returns>
    public LLmProviders GetFirstAuthenticatedProvider()
    {
        if (Authentications.IsEmpty)
        {
            return LLmProviders.OpenAi;
        }
        
        LLmProviders? cached = cachedFirstProvider;
        
        if (cached.HasValue && Authentications.ContainsKey(cached.Value))
        {
            return cached.Value;
        }
        
        LLmProviders firstProvider = Authentications.Keys.FirstOrDefault();
      
        if (firstProvider != LLmProviders.Unknown)
        {
            cachedFirstProvider = firstProvider;
            return firstProvider;
        }

        return LLmProviders.OpenAi;
    }
    
    /// <summary>
    /// Returns a concrete implementation of endpoint provider for a given known model.
    /// </summary>
    public IEndpointProvider GetProvider(IModel model)
    {
        return GetProvider(model.Provider);
    }
    
    /// <summary>
    /// Returns a concrete implementation of endpoint provider for a given known model.
    /// </summary>
    public IEndpointProvider GetProvider(ChatModel model)
    {
        if (model.Provider is LLmProviders.Unknown)
        {
            IModel? match = model.ApiName is null ? null : ChatModel.AllModelsApiMap!.GetValueOrDefault(model.ApiName, null);
            match ??= ChatModel.AllModelsMap!.GetValueOrDefault(model.Name, null);
            
            if (match is not null)
            {
                model.Provider = match.Provider;
            }
        }
        
        return GetProvider(model.Provider);
    }
    
    /// <summary>
    ///     Interceptor
    /// </summary>
    public Func<ChatRequest, ChatResult?, Task>? ChatRequestInterceptor { get; set; }

    /// <summary>
    ///     The API lets you do operations with images. Given a prompt and an input image, the model will edit a new image.
    /// </summary>
    public ImageEditEndpoint ImageEdit => imageEdit.Value;

    /// <summary>
    ///     Manages audio operations such as transcipt and translate.
    /// </summary>
    public AudioEndpoint Audio => audio.Value;

    /// <summary>
    ///     Assistants are higher-level API than <see cref="ChatEndpoint" /> featuring automatic context management, code
    ///     interpreter and file based retrieval.
    /// </summary>
    public AssistantsEndpoint Assistants => assistants.Value;

    /// <summary>
    ///     Assistants are higher-level API than <see cref="ChatEndpoint" /> featuring automatic context management, code
    ///     interpreter and file based retrieval.
    /// </summary>
    public ThreadsEndpoint Threads => threads.Value;

    /// <summary>
    ///     Text generation is the core function of the API. You give the API a prompt, and it generates a completion. The way
    ///     you "program" the API to do a task is by simply describing the task in plain english or providing a few written
    ///     examples. This simple approach works for a wide range of use cases, including summarization, translation, grammar
    ///     correction, question answering, chatbots, composing emails, and much more (see the prompt library for inspiration).
    /// </summary>
    public CompletionEndpoint Completions => completion.Value;

    /// <summary>
    ///     The API lets you transform text into a vector (list) of floating point numbers. The distance between two vectors
    ///     measures their relatedness. Small distances suggest high relatedness and large distances suggest low relatedness.
    /// </summary>
    public EmbeddingEndpoint Embeddings => embedding.Value;

    /// <summary>
    ///     Contextualized chunk embedding endpoint accepts document chunks—in addition to queries and full documents—and returns a response containing contextualized chunk vector embeddings.
    /// </summary>
    public ContextualEmbeddingEndpoint ContextualEmbeddings => contextualEmbedding.Value;

    /// <summary>
    ///     The Voyage multimodal embedding endpoint returns vector representations for a given list of multimodal inputs consisting of text, images, or an interleaving of both modalities.
    /// </summary>
    public MultimodalEmbeddingEndpoint MultimodalEmbeddings => multimodalEmbedding.Value;

    /// <summary>
    ///     Text generation in the form of chat messages. This interacts with the ChatGPT API.
    /// </summary>
    public ChatEndpoint Chat => chat.Value;
    
    /// <summary>
    ///     OpenAI's most advanced interface for generating model responses. Supports text and image inputs, and text outputs. Create stateful interactions with the model, using the output of previous responses as input. Extend the model's capabilities with built-in tools for file search, web search, computer use, and more. Allow the model access to external systems and data using function calling.
    /// </summary>
    public ResponsesEndpoint Responses => responses.Value;

    /// <summary>
    /// Create and manage conversations to store and retrieve conversation state across Response API calls.
    /// </summary>
    internal ResponsesConversationEndpoint ResponsesConversation => responsesConversation.Value;

    /// <summary>
    ///     Classify text against the OpenAI Content Policy.
    /// </summary>
    public ModerationEndpoint Moderation => moderation.Value;

    /// <summary>
    ///     The API endpoint for querying available Engines/models.
    /// </summary>
    public ModelsEndpoint Models => models.Value;

    /// <summary>
    ///     The API lets you do operations with files. You can upload, delete or retrieve files. Files can be used for
    ///     fine-tuning, search, etc.
    /// </summary>
    public FilesEndpoint Files => files.Value;

    /// <summary>
    ///     The API lets you do operations with images. Given a prompt and/or an input image, the model will generate a new
    ///     image.
    /// </summary>
    public ImageGenerationEndpoint ImageGenerations => imageGeneration.Value;
    
    /// <summary>
    ///     The API lets you do operations with vector stores on OpenAI API.
    /// </summary>
    public VectorStoresEndpoint VectorStores => vectorStores.Value;
    
    /// <summary>
    ///     The API lets you cache messages. Use only with <see cref="LLmProviders.Google"/>
    /// </summary>
    public CachingEndpoint Caching => caching.Value;

    /// <summary>
    ///     The API lets you do operations with uploads.
    /// </summary>
    public UploadsEndpoint Uploads => uploads.Value;

    /// <summary>
    ///     Voyage reranker endpoint receives as input a query, a list of documents, and other arguments such as the model name, and returns a response containing the reranking results.
    /// </summary>
    public RerankEndpoint Rerank => rerank.Value;

    /// <summary>
    ///     The Decision API evaluates a state (text or structured data) against a set of typed questions
    ///     (choice, score, noul) and returns calibrated probabilities instead of generated text.
    ///     Only available with the TypeSafe provider (Jev models).
    /// </summary>
    public DecisionEndpoint Decision => decision.Value;
    
    /// <summary>
    ///     The Skills API allows you to manage specialized prompts and configurations for Claude that can be automatically selected and used.
    ///     Only available with Anthropic provider.
    /// </summary>
    public SkillsEndpoint Skills => skills.Value;

    /// <summary>
    ///     OpenAI Skills API for uploading skills referenced by the Responses shell tool (<c>/v1/skills</c>).
    ///     Only available with OpenAI provider.
    /// </summary>
    public OpenAiSkillsEndpoint OpenAiSkills => openAiSkills.Value;
    
    /// <summary>
    ///     The API lets you count tokens in text or messages.
    /// </summary>
    public TokenizeEndpoint Tokenize => tokenize.Value;
    
    /// <summary>
    ///     The API lets you do operations with videos. Given a prompt and/or an input image, the model will generate a new
    ///     video.
    /// </summary>
    public VideoGenerationEndpoint Videos => videos.Value;
    
    /// <summary>
    ///     The Batch API allows you to create asynchronous jobs to process multiple requests at once.
    /// </summary>
    public BatchEndpoint Batch => batch.Value;

    /// <summary>
    ///     Gemini Webhooks API for project-level webhook registration and event-driven completion of async jobs.
    /// </summary>
    public WebhooksEndpoint Webhooks => webhooks.Value;

    /// <summary>
    ///     Gemini Interactions API for stateful model and managed-agent conversations (May 2026 steps schema by default).
    ///     Only available with Google provider.
    /// </summary>
    public InteractionsEndpoint Interactions => interactions.Value;
    
    /// <summary>
    ///     The OCR API allows you to extract text, layout, and other information from documents.
    ///     Only available with Mistral provider.
    /// </summary>
    public OcrEndpoint Ocr => ocr.Value;

    /// <summary>
    ///     Gemini Managed Agents API for saved Antigravity-based agent configurations.
    ///     Only available with Google provider.
    /// </summary>
    public ManagedAgentsEndpoint ManagedAgents => managedAgents.Value;

    /// <summary>
    ///     Claude Managed Agents API (<c>/v1/agents</c>) — agent definitions and multiagent coordinator configuration.
    /// </summary>
    public AnthropicManagedAgentsEndpoint AnthropicManagedAgents => anthropicManagedAgents.Value;

    /// <summary>
    ///     Claude Managed Agent sessions API — multiagent orchestration, outcomes, threads, and environments.
    /// </summary>
    public AnthropicManagedAgentSessionsEndpoint AnthropicManagedAgentSessions => anthropicManagedAgentSessions.Value;

    /// <summary>
    ///     Claude Managed Agent environments API — isolated execution environments for agent sessions.
    /// </summary>
    public AnthropicManagedAgentEnvironmentsEndpoint AnthropicManagedAgentEnvironments => anthropicManagedAgentEnvironments.Value;

    /// <summary>
    ///     Anthropic Admin API for querying organization and workspace rate limits.
    ///     Requires an Admin API key (<c>sk-ant-admin...</c>).
    /// </summary>
    public RateLimitsEndpoint RateLimits => rateLimits.Value;

    /// <summary>
    ///     OpenAI Realtime API (GA): client secrets, legacy sessions, and WebSocket voice/translation/transcription.
    /// </summary>
    public RealtimeEndpoint Realtime => realtime.Value;

    /// <summary>
    ///     Live voice APIs: Gemini Live (<see cref="Live.LiveEndpoint.ConnectAsync"/>) and
    ///     OpenAI GPT-Live (<see cref="Live.LiveEndpoint.OpenAi"/>).
    /// </summary>
    public LiveEndpoint Live => live.Value;

    /// <summary>
    ///     OpenAI Codex integration for ChatGPT subscription authentication, models, and text turns.
    /// </summary>
    public CodexEndpoint Codex => codex.Value;

    /// <summary>
    ///     Anthropic Compaction API for server-side context summarization (beta).
    ///     Only available with Anthropic provider on Claude Opus 4.6+ and Sonnet 4.6+.
    /// </summary>
    public CompactionEndpoint Compaction => compaction.Value;
}
