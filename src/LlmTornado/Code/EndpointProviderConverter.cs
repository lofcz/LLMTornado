using LlmTornado.Code.Vendor;

namespace LlmTornado.Code;

internal static class EndpointProviderConverter
{
    public static IEndpointProvider CreateProvider(LLmProviders provider, TornadoApi api)
    {
        IEndpointProvider createdProvider = provider switch
        {
            LLmProviders.OpenAi => new OpenAiEndpointProvider(),
            LLmProviders.Anthropic => new AnthropicEndpointProvider(),
            LLmProviders.Cohere => new CohereEndpointProvider(),
            LLmProviders.Google => new GoogleEndpointProvider(),
            LLmProviders.DeepSeek => new OpenAiEndpointProvider(LLmProviders.DeepSeek)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.deepseek.com/{0}/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.DeepSeek))}{url}"
            },
            LLmProviders.Mistral => new OpenAiEndpointProvider(LLmProviders.Mistral)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.mistral.ai/{0}/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.Mistral))}{url}"
            },
            LLmProviders.Groq => new OpenAiEndpointProvider(LLmProviders.Groq)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.groq.com/openai/{0}/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.Groq))}{url}"
            },
            LLmProviders.XAi => new OpenAiEndpointProvider(LLmProviders.XAi)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.x.ai/{0}/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.XAi))}{url}"
            },
            LLmProviders.Perplexity => new OpenAiEndpointProvider(LLmProviders.Perplexity)
            {
                UrlResolver = (endpoint, url, ctx) =>
                {
                    string fragment = OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.Perplexity);
                    string format = endpoint switch
                    {
                        CapabilityEndpoints.Responses or CapabilityEndpoints.Embeddings
                            or CapabilityEndpoints.ContextualEmbeddings or CapabilityEndpoints.Models
                            => "https://api.perplexity.ai/v1/{0}",
                        _ => "https://api.perplexity.ai/{0}"
                    };

                    return $"{string.Format(api.ApiUrlFormat ?? format, fragment)}{url}";
                }
            },
            LLmProviders.Zai => new OpenAiEndpointProvider(LLmProviders.Zai)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.z.ai/api/paas/v4/{0}", OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.Zai))}{url}"
            },
            LLmProviders.Voyage => new OpenAiEndpointProvider(LLmProviders.Voyage)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.voyageai.com/{0}/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.Voyage))}{url}"
            },
            LLmProviders.DeepInfra => new OpenAiEndpointProvider(LLmProviders.DeepInfra)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.deepinfra.com/{0}/openai/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.DeepInfra))}{url}"
            },
            LLmProviders.OpenRouter => new OpenAiEndpointProvider(LLmProviders.OpenRouter)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://openrouter.ai/api/{0}/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.OpenRouter))}{url}"
            },
            LLmProviders.MoonshotAi => new OpenAiEndpointProvider(LLmProviders.MoonshotAi)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.moonshot.ai/{0}/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.MoonshotAi))}{url}"
            },
            LLmProviders.Blablador => new OpenAiEndpointProvider(LLmProviders.Blablador)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.helmholtz-blablador.fz-juelich.de/{0}/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.Blablador))}{url}"
            },
            LLmProviders.Alibaba => new OpenAiEndpointProvider(LLmProviders.Alibaba)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://dashscope-intl.aliyuncs.com/compatible-mode/{0}/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.Alibaba))}{url}"
            },
            LLmProviders.Requesty => new OpenAiEndpointProvider(LLmProviders.Requesty)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://router.requesty.ai/{0}/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.Requesty))}{url}"
            },
            LLmProviders.Opper => new OpenAiEndpointProvider(LLmProviders.Opper)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.opper.ai/v3/compat/{0}", OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.Opper))}{url}"
            },
            LLmProviders.Upstage => new OpenAiEndpointProvider(LLmProviders.Upstage)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.upstage.ai/v1/{0}", OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.Upstage))}{url}"
            },
            LLmProviders.MiniMax => new OpenAiEndpointProvider(LLmProviders.MiniMax)
            {
                UrlResolver = (endpoint, url, ctx) => $"{string.Format(api.ApiUrlFormat ?? "https://api.minimax.io/{0}/{1}", api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.MiniMax))}{url}"
            },
            LLmProviders.LiteLlm => new OpenAiEndpointProvider(LLmProviders.LiteLlm)
            {
                UrlResolver = (endpoint, url, ctx) =>
                {
                    // LiteLLM proxies are self-hosted, so honor a per-auth BaseUrl (like Custom),
                    // defaulting to the standard local LiteLLM proxy endpoint.
                    string baseFormat = api.ApiUrlFormat
                        ?? (api.Authentications.TryGetValue(LLmProviders.LiteLlm, out ProviderAuthentication? a)
                                && !string.IsNullOrWhiteSpace(a.BaseUrl)
                            ? $"{a.BaseUrl!.TrimEnd('/')}/{{0}}/{{1}}"
                            : "http://localhost:4000/{0}/{1}");
                    return $"{string.Format(baseFormat, api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.LiteLlm))}{url}";
                }
            },
            LLmProviders.Custom => new OpenAiEndpointProvider(LLmProviders.Custom)
            {
                UrlResolver = (endpoint, url, ctx) =>
                {
                    string baseFormat = api.ApiUrlFormat
                        ?? (api.Authentications.TryGetValue(LLmProviders.Custom, out ProviderAuthentication? a)
                                && !string.IsNullOrWhiteSpace(a.BaseUrl)
                            ? $"{a.BaseUrl!.TrimEnd('/')}/{{0}}/{{1}}"
                            : "http://localhost:11434/{0}/{1}");
                    return $"{string.Format(baseFormat, api.ResolveApiVersion(), OpenAiEndpointProvider.GetEndpointUrlFragment(endpoint, LLmProviders.Custom))}{url}";
                }
            },
            _ => new OpenAiEndpointProvider()
        };

        createdProvider.Api = api;
        
        if (api.Authentications.TryGetValue(provider, out ProviderAuthentication? auth))
        {
            createdProvider.Auth = auth;
        }

        return createdProvider;
    }
}