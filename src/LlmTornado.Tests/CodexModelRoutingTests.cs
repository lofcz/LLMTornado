using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;

namespace LlmTornado.Tests;

/// <summary>
/// Regression tests for Codex model endpoint routing.
///
/// OpenAI's Codex models (e.g. gpt-5.3-codex) are served only via the Responses
/// (and Batch) endpoints, never via /v1/chat/completions. If a Codex model is
/// declared with the <see cref="ChatModelEndpointCapabilities.Chat"/> capability,
/// <see cref="ChatRequest.GetCapabilityEndpoint(ChatRequest)"/> stops auto-upcasting
/// it to the Responses endpoint and a plain chat turn (no explicit
/// <c>UseResponseEndpoint</c>, no <c>ResponseRequestParameters</c>) is sent to
/// /v1/chat/completions, which OpenAI rejects with:
/// "This is not a chat model and thus not supported in the v1/chat/completions endpoint."
/// </summary>
[TestFixture]
public class CodexModelRoutingTests
{
    private TornadoApi? _api;
    private IEndpointProvider? _provider;

    [SetUp]
    public void Setup()
    {
        // No real network calls are made; a dummy key is enough to obtain the provider and serialize.
        _api = new TornadoApi("test-key");
        _provider = _api.GetProvider(LLmProviders.OpenAi);
    }

    /// <summary>
    /// All native OpenAI Codex models, including gpt-5.3-codex. Each must route to the Responses endpoint.
    /// </summary>
    private static readonly ChatModel[] CodexModels =
    [
        ChatModel.OpenAi.Codex.Gpt53Codex,
        ChatModel.OpenAi.Gpt5.V5Codex,
        ChatModel.OpenAi.Gpt51.V51Codex,
        ChatModel.OpenAi.Gpt51.V51CodexMini,
        ChatModel.OpenAi.Gpt51.V51CodexMax,
        ChatModel.OpenAi.Gpt52.V52Codex
    ];

    [Test]
    public void Gpt53Codex_IsResponsesOnly_NotChat()
    {
        // The literal example from the bug report: gpt-5.3-codex must not advertise Chat support.
        ChatModel gpt53Codex = ChatModel.OpenAi.Codex.Gpt53Codex;
        Assert.That(gpt53Codex.Name, Is.EqualTo("gpt-5.3-codex"));
        Assert.That(gpt53Codex.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Responses));
        Assert.That(gpt53Codex.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Batch));
        Assert.That(gpt53Codex.EndpointCapabilities, Does.Not.Contain(ChatModelEndpointCapabilities.Chat),
            "gpt-5.3-codex is only served via the Responses endpoint, not /v1/chat/completions.");
    }

    [Test]
    public void Gpt53Codex_PlainChatTurn_RoutesToResponsesEndpoint()
    {
        // Mirrors the CLI/agent path: a plain chat request with no explicit endpoint preference
        // and no response parameters. This is exactly what triggered the original error.
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.OpenAi.Codex.Gpt53Codex,
            Messages = [new ChatMessage(ChatMessageRoles.User, "hi")]
        };

        Assert.That(request.UseResponseEndpoint, Is.Null, "Precondition: CLI/agent does not force the endpoint.");
        Assert.That(request.ResponseRequestParameters, Is.Null, "Precondition: a plain chat turn has no response parameters.");

        TornadoRequestContent serialized = request.Serialize(_provider!);
        Assert.That(serialized.CapabilityEndpoint, Is.EqualTo(CapabilityEndpoints.Responses),
            "gpt-5.3-codex must auto-route to the Responses endpoint for a plain chat turn.");
    }

    [Test]
    [TestCaseSource(nameof(CodexModels))]
    public void CodexModel_DoesNotAdvertiseChat(ChatModel model)
    {
        // The general invariant: no Codex model should advertise Chat support, otherwise it would
        // misroute to /v1/chat/completions exactly like gpt-5.3-codex did.
        Assert.That(model.EndpointCapabilities, Is.Not.Null, $"{model.Name} should declare endpoint capabilities.");
        Assert.That(model.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Responses), $"{model.Name} should support the Responses endpoint.");
        Assert.That(model.EndpointCapabilities, Does.Not.Contain(ChatModelEndpointCapabilities.Chat), $"{model.Name} must not advertise the Chat endpoint.");
    }

    [Test]
    [TestCaseSource(nameof(CodexModels))]
    public void CodexModel_PlainChatTurn_RoutesToResponsesEndpoint(ChatModel model)
    {
        // The general fix: every Codex model must auto-route to Responses for a plain chat turn.
        ChatRequest request = new ChatRequest
        {
            Model = model,
            Messages = [new ChatMessage(ChatMessageRoles.User, "hi")]
        };

        TornadoRequestContent serialized = request.Serialize(_provider!);
        Assert.That(serialized.CapabilityEndpoint, Is.EqualTo(CapabilityEndpoints.Responses),
            $"{model.Name} must auto-route to the Responses endpoint for a plain chat turn.");
    }

    [Test]
    public void NonCodexChatModel_PlainChatTurn_StillRoutesToChatEndpoint()
    {
        // Guard against over-correction: a regular chat-capable model must keep using the Chat endpoint.
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.OpenAi.Gpt54.V54,
            Messages = [new ChatMessage(ChatMessageRoles.User, "hi")]
        };

        TornadoRequestContent serialized = request.Serialize(_provider!);
        Assert.That(serialized.CapabilityEndpoint, Is.EqualTo(CapabilityEndpoints.Chat),
            "A standard chat-capable model should still use the Chat endpoint by default.");
    }
}
