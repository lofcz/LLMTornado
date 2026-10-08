using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Decision.Models.OpenRouter;

/// <summary>OpenRouter models for the alpha Decisions endpoint, checked October 7, 2026.</summary>
public class DecisionModelOpenRouterAll : IVendorModelClassProvider
{
    /// <summary>~typesafe/jev-latest</summary>
    public static readonly DecisionModel ModelJevLatest = DecisionModel.GetRegisteredModel("~typesafe/jev-latest");

    /// <inheritdoc cref="ModelJevLatest"/>
    public readonly DecisionModel JevLatest = DecisionModel.GetRegisteredModel("~typesafe/jev-latest");

    /// <summary>cloudflare/clef</summary>
    public static readonly DecisionModel ModelClef = DecisionModel.GetRegisteredModel("cloudflare/clef");

    /// <inheritdoc cref="ModelClef"/>
    public readonly DecisionModel Clef = DecisionModel.GetRegisteredModel("cloudflare/clef");

    /// <summary>cloudflare/clef-flash</summary>
    public static readonly DecisionModel ModelClefFlash = DecisionModel.GetRegisteredModel("cloudflare/clef-flash");

    /// <inheritdoc cref="ModelClefFlash"/>
    public readonly DecisionModel ClefFlash = DecisionModel.GetRegisteredModel("cloudflare/clef-flash");

    /// <summary>inception/mercury-decide:free</summary>
    public static readonly DecisionModel ModelMercuryDecideFree = DecisionModel.GetRegisteredModel("inception/mercury-decide:free");

    /// <inheritdoc cref="ModelMercuryDecideFree"/>
    public readonly DecisionModel MercuryDecideFree = DecisionModel.GetRegisteredModel("inception/mercury-decide:free");

    /// <summary>jaredpalmer/kev-4b</summary>
    public static readonly DecisionModel ModelKev4b = DecisionModel.GetRegisteredModel("jaredpalmer/kev-4b");

    /// <inheritdoc cref="ModelKev4b"/>
    public readonly DecisionModel Kev4b = DecisionModel.GetRegisteredModel("jaredpalmer/kev-4b");

    /// <summary>liquid/d1</summary>
    public static readonly DecisionModel ModelD1 = DecisionModel.GetRegisteredModel("liquid/d1");

    /// <inheritdoc cref="ModelD1"/>
    public readonly DecisionModel D1 = DecisionModel.GetRegisteredModel("liquid/d1");

    /// <summary>openai/gpt-6-luna-decisions</summary>
    public static readonly DecisionModel ModelGpt6LunaDecisions = DecisionModel.GetRegisteredModel("openai/gpt-6-luna-decisions");

    /// <inheritdoc cref="ModelGpt6LunaDecisions"/>
    public readonly DecisionModel Gpt6LunaDecisions = DecisionModel.GetRegisteredModel("openai/gpt-6-luna-decisions");

    /// <summary>perplexity/pplx-decider-v1-27b</summary>
    public static readonly DecisionModel ModelPplxDeciderV127b = DecisionModel.GetRegisteredModel("perplexity/pplx-decider-v1-27b");

    /// <inheritdoc cref="ModelPplxDeciderV127b"/>
    public readonly DecisionModel PplxDeciderV127b = DecisionModel.GetRegisteredModel("perplexity/pplx-decider-v1-27b");

    /// <summary>respan/span-01</summary>
    public static readonly DecisionModel ModelSpan01 = DecisionModel.GetRegisteredModel("respan/span-01");

    /// <inheritdoc cref="ModelSpan01"/>
    public readonly DecisionModel Span01 = DecisionModel.GetRegisteredModel("respan/span-01");

    /// <summary>respan/span-01-lite</summary>
    public static readonly DecisionModel ModelSpan01Lite = DecisionModel.GetRegisteredModel("respan/span-01-lite");

    /// <inheritdoc cref="ModelSpan01Lite"/>
    public readonly DecisionModel Span01Lite = DecisionModel.GetRegisteredModel("respan/span-01-lite");

    /// <summary>respan/span-01-lite:free</summary>
    public static readonly DecisionModel ModelSpan01LiteFree = DecisionModel.GetRegisteredModel("respan/span-01-lite:free");

    /// <inheritdoc cref="ModelSpan01LiteFree"/>
    public readonly DecisionModel Span01LiteFree = DecisionModel.GetRegisteredModel("respan/span-01-lite:free");

    /// <summary>typesafe/jev-1.13</summary>
    public static readonly DecisionModel ModelJev113 = DecisionModel.GetRegisteredModel("typesafe/jev-1.13");

    /// <inheritdoc cref="ModelJev113"/>
    public readonly DecisionModel Jev113 = DecisionModel.GetRegisteredModel("typesafe/jev-1.13");

    /// <summary>upstage/solar-decide</summary>
    public static readonly DecisionModel ModelSolarDecide = DecisionModel.GetRegisteredModel("upstage/solar-decide");

    /// <inheritdoc cref="ModelSolarDecide"/>
    public readonly DecisionModel SolarDecide = DecisionModel.GetRegisteredModel("upstage/solar-decide");

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
