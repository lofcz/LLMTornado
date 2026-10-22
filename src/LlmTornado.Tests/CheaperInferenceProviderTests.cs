using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Models.Vendors;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

/// <summary>
/// Unit tests for the Cheaper Inference provider (OpenAI-compatible LLM gateway).
/// </summary>
[TestFixture]
public class CheaperInferenceProviderTests
{
    [Test]
    public void CheaperInference_ResolvesOpenAiCompatibleProvider()
    {
        TornadoApi api = new TornadoApi([new ProviderAuthentication(LLmProviders.CheaperInference, "ci_live_test")]);
        IEndpointProvider provider = api.GetProvider(LLmProviders.CheaperInference);
        Assert.That(provider.Provider, Is.EqualTo(LLmProviders.CheaperInference));
    }

    [Test]
    public void CheaperInference_DefaultsToHostedEndpoint()
    {
        TornadoApi api = new TornadoApi([new ProviderAuthentication(LLmProviders.CheaperInference, "ci_live_test")]);
        IEndpointProvider provider = api.GetProvider(LLmProviders.CheaperInference);

        string url = provider.ApiUrl(CapabilityEndpoints.Chat, null, new ChatModel("gpt-5.4-mini", LLmProviders.CheaperInference));

        Assert.That(url, Does.StartWith("https://api.cheaperinference.com/"));
        Assert.That(url, Does.Contain("chat/completions"));
    }

    [Test]
    public void CheaperInference_HonorsApiUrlFormatOverride()
    {
        TornadoApi api = new TornadoApi([new ProviderAuthentication(LLmProviders.CheaperInference, "ci_live_test")])
        {
            ApiUrlFormat = "https://gateway.internal/{0}/{1}"
        };
        IEndpointProvider provider = api.GetProvider(LLmProviders.CheaperInference);

        string url = provider.ApiUrl(CapabilityEndpoints.Chat, null, new ChatModel("gpt-5.4-mini", LLmProviders.CheaperInference));

        Assert.That(url, Does.StartWith("https://gateway.internal/"));
    }

    [Test]
    public void CheaperInference_SerializesOpenAiCompatibleChatRequest()
    {
        IEndpointProvider provider =
            new TornadoApi([new ProviderAuthentication(LLmProviders.CheaperInference, "ci_live_test")])
                .GetProvider(LLmProviders.CheaperInference);

        ChatRequest request = new ChatRequest
        {
            Model = new ChatModel("gpt-5.4-mini", LLmProviders.CheaperInference),
            Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
        };

        TornadoRequestContent serialized = request.Serialize(provider);
        JObject body = JObject.Parse(serialized.Body.ToString()!);

        // Cheaper Inference is OpenAI-compatible: the body carries the bare model id
        // and OpenAI-shaped messages, so any model the gateway exposes works verbatim.
        Assert.That(body["model"]?.ToString(), Is.EqualTo("gpt-5.4-mini"));
        Assert.That(body["messages"], Is.Not.Null);
        Assert.That(body["messages"]!.First?["role"]?.ToString(), Is.EqualTo("user"));
        Assert.That(body["messages"]!.First?["content"]?.ToString(), Is.EqualTo("Hello"));
    }

    [Test]
    public void CheaperInference_ModelsEndpointResolvesToModelsUrl()
    {
        TornadoApi api = new TornadoApi([new ProviderAuthentication(LLmProviders.CheaperInference, "ci_live_test")]);
        IEndpointProvider provider = api.GetProvider(LLmProviders.CheaperInference);

        string url = provider.ApiUrl(CapabilityEndpoints.Models, null);

        // Model discovery reuses the generic ModelsEndpoint (GET /v1/models).
        Assert.That(url, Does.StartWith("https://api.cheaperinference.com/"));
        Assert.That(url, Does.Contain("models"));
    }

    [Test]
    public void CheaperInference_ParsesOpenAiCompatibleModelsDiscoveryResponse()
    {
        IEndpointProvider provider =
            new TornadoApi([new ProviderAuthentication(LLmProviders.CheaperInference, "ci_live_test")])
                .GetProvider(LLmProviders.CheaperInference);

        // The gateway returns the OpenAI-compatible model list shape.
        string sampleJson =
            @"{ ""object"": ""list"", ""data"": [ { ""id"": ""gpt-5.4-mini"", ""object"": ""model"", ""owned_by"": ""cheaperinference"" }, { ""id"": ""claude-sonnet-5"", ""object"": ""model"", ""owned_by"": ""cheaperinference"" } ] }";

        RetrievedModelsResult? result = provider.InboundMessage<RetrievedModelsResult>(sampleJson, null, null);

        Assert.That(result?.Data, Is.Not.Null);
        Assert.That(result!.Data, Has.Count.EqualTo(2));
        Assert.That(result.Data[0].Id, Is.EqualTo("gpt-5.4-mini"));
        Assert.That(result.Data[1].Id, Is.EqualTo("claude-sonnet-5"));
    }
}
