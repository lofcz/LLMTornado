using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Responses;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

/// <summary>
/// Registration, sampling, and routing tests for GPT-6 Astra (September 8, 2026).
/// </summary>
[TestFixture]
public class Gpt6IntegrationTests
{
    private TornadoApi _api = null!;
    private IEndpointProvider _provider = null!;

    [SetUp]
    public void Setup()
    {
        _api = new TornadoApi("test-key");
        _provider = _api.GetProvider(LLmProviders.OpenAi);
    }

    [Test]
    public void Gpt6_ModelRegistration_Works()
    {
        Assert.That(ChatModel.OpenAi.Gpt6.V6Astra.Name, Is.EqualTo("gpt-6-astra"));
        Assert.That(ChatModel.OpenAi.Gpt6.V6Astra.ContextTokens, Is.EqualTo(1_050_000));
        Assert.That(ChatModel.OpenAi.Gpt6.V6Astra.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Chat));
        Assert.That(ChatModel.OpenAi.Gpt6.V6Astra.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Responses));
        Assert.That(ChatModel.OpenAi.Gpt6.V6Astra.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Batch));
        Assert.That(ChatModelOpenAiGpt6.ModelsAll, Has.Count.EqualTo(4));
        Assert.That(ChatModelOpenAi.ReasoningModelsAll, Does.Contain(ChatModel.OpenAi.Gpt6.V6Astra));
        Assert.That(ChatModelOpenAi.WebSearchCompatibleModelsAll, Does.Contain(ChatModel.OpenAi.Gpt6.V6Astra));
        Assert.That(ChatModelOpenAi.ComputerUseModelsAllSet, Does.Contain(ChatModel.OpenAi.Gpt6.V6Astra));
        Assert.That(ChatModelOpenAi.ToolSearchModelsAllSet, Does.Contain(ChatModel.OpenAi.Gpt6.V6Astra));
        Assert.That(ChatModelOpenAi.CompactionModelsAllSet, Does.Contain(ChatModel.OpenAi.Gpt6.V6Astra));
        Assert.That(ChatModelOpenAi.SamplingParamsNeverSupported, Does.Contain(ChatModel.OpenAi.Gpt6.V6Astra));
        Assert.That(ChatModelOpenAi.ToolsRequireResponsesModelsAllSet, Does.Contain(ChatModel.OpenAi.Gpt6.V6Astra));
    }

    [Test]
    public void Gpt6_SamplingParams_AlwaysCleared()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.OpenAi.Gpt6.V6Astra,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            Temperature = 0.7,
            TopP = 0.9,
            ReasoningEffort = ChatReasoningEfforts.Low
        };

        TornadoRequestContent serialized = request.Serialize(_provider);
        JObject body = JObject.Parse(serialized.Body.ToString()!);

        Assert.That(body["temperature"], Is.Null);
        Assert.That(body["top_p"], Is.Null);
        Assert.That(body["reasoning_effort"]?.ToString(), Is.EqualTo("low"));
    }

    [Test]
    public void Gpt6_MaxReasoningEffort_Serializes()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.OpenAi.Gpt6.V6Astra,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            ReasoningEffort = ChatReasoningEfforts.Max
        };

        TornadoRequestContent serialized = request.Serialize(_provider);
        JObject body = JObject.Parse(serialized.Body.ToString()!);

        Assert.That(body["reasoning_effort"]?.ToString(), Is.EqualTo("max"));
    }

    [Test]
    public void Gpt6_ResponseRequest_MaxReasoningEffort_Serializes()
    {
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt6.V6Astra,
            InputString = "Hello",
            Reasoning = new ReasoningConfiguration(ResponseReasoningEfforts.Max)
        };

        TornadoRequestContent serialized = request.Serialize(_provider);
        JObject body = JObject.Parse(serialized.Body.ToString()!);

        Assert.That(body["reasoning"]?["effort"]?.ToString(), Is.EqualTo("max"));
    }

    [Test]
    public void Gpt6_PlainChatTurn_RoutesToChatEndpoint()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.OpenAi.Gpt6.V6Astra,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")]
        };

        TornadoRequestContent serialized = request.Serialize(_provider);
        Assert.That(serialized.CapabilityEndpoint, Is.EqualTo(CapabilityEndpoints.Chat));
    }

    [Test]
    public void Gpt6_ToolCall_RoutesToResponsesEndpoint()
    {
        ChatRequest request = new ChatRequest
        {
            Model = ChatModel.OpenAi.Gpt6.V6Astra,
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
            Tools = [new Tool(new ToolFunction("lookup", "Looks up a value."))]
        };

        TornadoRequestContent serialized = request.Serialize(_provider);
        Assert.That(serialized.CapabilityEndpoint, Is.EqualTo(CapabilityEndpoints.Responses),
            "GPT-6 Astra tool calling requires the Responses API.");
    }

    [Test]
    public void Gpt6_ServiceTiers_SerializeAndSelectEndpoints()
    {
        foreach (var (tier, wireValue, endpoint) in new[]
        {
            (ChatRequestServiceTiers.Fast, "fast", CapabilityEndpoints.Chat),
            (ChatRequestServiceTiers.Ultrafast, "ultrafast", CapabilityEndpoints.Responses)
        })
        {
            ChatRequest request = new ChatRequest
            {
                Model = ChatModel.OpenAi.Gpt6.V6Astra,
                Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
                ServiceTier = tier,
                ReasoningEffort = ChatReasoningEfforts.Max
            };

            TornadoRequestContent serialized = request.Serialize(_provider);
            JObject body = JObject.Parse(serialized.Body.ToString()!);
            Assert.That(serialized.CapabilityEndpoint, Is.EqualTo(endpoint));
            Assert.That(body["service_tier"]?.ToString(), Is.EqualTo(wireValue));
            string effortPath = endpoint is CapabilityEndpoints.Responses ? "reasoning.effort" : "reasoning_effort";
            Assert.That(body.SelectToken(effortPath)?.ToString(), Is.EqualTo("max"));
        }
    }
}
