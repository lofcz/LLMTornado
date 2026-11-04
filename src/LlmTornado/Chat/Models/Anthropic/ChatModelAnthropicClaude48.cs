using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// Claude 4.8 class models from Anthropic (Claude Opus 4.8).
/// </summary>
public class ChatModelAnthropicClaude48 : IVendorModelClassProvider
{
    /// <summary>
    /// Claude Opus 4.8, a previous-generation Opus model.
    /// 1M context window by default, 128K max output, adaptive thinking, high-res vision (2576px long edge).
    /// API model ID: <c>claude-opus-4-8</c>.
    /// Fast mode is deprecated, with removal on November 30, 2026. Use Opus 5.5 for fast mode.
    /// </summary>
    public static readonly ChatModel ModelOpus = new ChatModel(Opus48ModelId, LLmProviders.Anthropic, Opus47PlusContextTokens)
    {
        ReasoningTokensSpecialValues = [-1]
    };

    /// <summary>
    /// <inheritdoc cref="ModelOpus"/>
    /// </summary>
    public readonly ChatModel Opus = ModelOpus;

    /// <summary>
    /// Codename alias for <see cref="ModelOpus"/> (NextOpus / Claude 4.8). Same API ID: <c>claude-opus-4-8</c>.
    /// </summary>
    public static readonly ChatModel ModelNextOpus = ModelOpus;

    /// <summary>
    /// <inheritdoc cref="ModelNextOpus"/>
    /// </summary>
    public readonly ChatModel NextOpus = ModelNextOpus;

    /// <summary>
    /// All known Claude 4.8 models from Anthropic.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelOpus
    ]);

    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;

    internal ChatModelAnthropicClaude48()
    {

    }

    private const string Opus48ModelId = ChatModelAnthropicHelper.Opus48ModelId;
    private const int Opus47PlusContextTokens = ChatModelAnthropicHelper.Opus47PlusContextTokens;
}
