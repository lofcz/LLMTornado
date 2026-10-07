using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Decision.Models.OpenRouter;

/// <summary>OpenRouter models for the alpha Decisions endpoint, checked October 7, 2026.</summary>
public class DecisionModelOpenRouterAll : IVendorModelClassProvider
{
    /// <summary>~typesafe/jev-latest</summary>
    public static readonly DecisionModel ModelJevLatest = new DecisionModel("~typesafe/jev-latest", LLmProviders.OpenRouter)
    {
        ContextTokens = 32000
    };

    /// <inheritdoc cref="ModelJevLatest"/>
    public readonly DecisionModel JevLatest = ModelJevLatest;

    /// <summary>cloudflare/clef</summary>
    public static readonly DecisionModel ModelClef = new DecisionModel("cloudflare/clef", LLmProviders.OpenRouter)
    {
        ContextTokens = 65536
    };

    /// <inheritdoc cref="ModelClef"/>
    public readonly DecisionModel Clef = ModelClef;

    /// <summary>cloudflare/clef-flash</summary>
    public static readonly DecisionModel ModelClefFlash = new DecisionModel("cloudflare/clef-flash", LLmProviders.OpenRouter)
    {
        ContextTokens = 65536
    };

    /// <inheritdoc cref="ModelClefFlash"/>
    public readonly DecisionModel ClefFlash = ModelClefFlash;

    /// <summary>inception/mercury-decide:free</summary>
    public static readonly DecisionModel ModelMercuryDecideFree = new DecisionModel("inception/mercury-decide:free", LLmProviders.OpenRouter)
    {
        ContextTokens = 32768
    };

    /// <inheritdoc cref="ModelMercuryDecideFree"/>
    public readonly DecisionModel MercuryDecideFree = ModelMercuryDecideFree;

    /// <summary>jaredpalmer/kev-4b</summary>
    public static readonly DecisionModel ModelKev4b = new DecisionModel("jaredpalmer/kev-4b", LLmProviders.OpenRouter)
    {
        ContextTokens = 8192
    };

    /// <inheritdoc cref="ModelKev4b"/>
    public readonly DecisionModel Kev4b = ModelKev4b;

    /// <summary>liquid/d1</summary>
    public static readonly DecisionModel ModelD1 = new DecisionModel("liquid/d1", LLmProviders.OpenRouter)
    {
        ContextTokens = 65536
    };

    /// <inheritdoc cref="ModelD1"/>
    public readonly DecisionModel D1 = ModelD1;

    /// <summary>openai/gpt-6-luna-decisions</summary>
    public static readonly DecisionModel ModelGpt6LunaDecisions = new DecisionModel("openai/gpt-6-luna-decisions", LLmProviders.OpenRouter)
    {
        ContextTokens = 1050000
    };

    /// <inheritdoc cref="ModelGpt6LunaDecisions"/>
    public readonly DecisionModel Gpt6LunaDecisions = ModelGpt6LunaDecisions;

    /// <summary>perplexity/pplx-decider-v1-27b</summary>
    public static readonly DecisionModel ModelPplxDeciderV127b = new DecisionModel("perplexity/pplx-decider-v1-27b", LLmProviders.OpenRouter)
    {
        ContextTokens = 262144
    };

    /// <inheritdoc cref="ModelPplxDeciderV127b"/>
    public readonly DecisionModel PplxDeciderV127b = ModelPplxDeciderV127b;

    /// <summary>respan/span-01</summary>
    public static readonly DecisionModel ModelSpan01 = new DecisionModel("respan/span-01", LLmProviders.OpenRouter)
    {
        ContextTokens = 0
    };

    /// <inheritdoc cref="ModelSpan01"/>
    public readonly DecisionModel Span01 = ModelSpan01;

    /// <summary>respan/span-01-lite</summary>
    public static readonly DecisionModel ModelSpan01Lite = new DecisionModel("respan/span-01-lite", LLmProviders.OpenRouter)
    {
        ContextTokens = 0
    };

    /// <inheritdoc cref="ModelSpan01Lite"/>
    public readonly DecisionModel Span01Lite = ModelSpan01Lite;

    /// <summary>respan/span-01-lite:free</summary>
    public static readonly DecisionModel ModelSpan01LiteFree = new DecisionModel("respan/span-01-lite:free", LLmProviders.OpenRouter)
    {
        ContextTokens = 0
    };

    /// <inheritdoc cref="ModelSpan01LiteFree"/>
    public readonly DecisionModel Span01LiteFree = ModelSpan01LiteFree;

    /// <summary>typesafe/jev-1.13</summary>
    public static readonly DecisionModel ModelJev113 = new DecisionModel("typesafe/jev-1.13", LLmProviders.OpenRouter)
    {
        ContextTokens = 32000
    };

    /// <inheritdoc cref="ModelJev113"/>
    public readonly DecisionModel Jev113 = ModelJev113;

    /// <summary>upstage/solar-decide</summary>
    public static readonly DecisionModel ModelSolarDecide = new DecisionModel("upstage/solar-decide", LLmProviders.OpenRouter)
    {
        ContextTokens = 524288
    };

    /// <inheritdoc cref="ModelSolarDecide"/>
    public readonly DecisionModel SolarDecide = ModelSolarDecide;

    /// <summary>All known OpenRouter models for the alpha Decisions endpoint.</summary>
    public static readonly List<IModel> ModelsAll =
    [
        ModelJevLatest,
        ModelClef,
        ModelClefFlash,
        ModelMercuryDecideFree,
        ModelKev4b,
        ModelD1,
        ModelGpt6LunaDecisions,
        ModelPplxDeciderV127b,
        ModelSpan01,
        ModelSpan01Lite,
        ModelSpan01LiteFree,
        ModelJev113,
        ModelSolarDecide
    ];

    /// <inheritdoc cref="ModelsAll"/>
    public List<IModel> AllModels => ModelsAll;

    internal DecisionModelOpenRouterAll()
    {
    }
}
