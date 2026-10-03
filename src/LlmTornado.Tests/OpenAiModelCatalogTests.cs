using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Responses;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

/// <summary>
/// Offline catalog and serialization regressions against OpenAI's October 3, 2026 model documentation.
/// </summary>
[TestFixture]
public class OpenAiModelCatalogTests
{
    private static IEndpointProvider Provider => new TornadoApi("test-key").GetProvider(LLmProviders.OpenAi);

    private static IEnumerable<(ChatModel Model, string Name, bool SupportsNone)> Gpt6Models()
    {
        yield return (ChatModel.OpenAi.Gpt6.V6Astra, "gpt-6-astra", false);
        yield return (ChatModel.OpenAi.Gpt6.V61Sol, "gpt-6.1-sol", false);
        yield return (ChatModel.OpenAi.Gpt6.V6Sol, "gpt-6-sol", true);
        yield return (ChatModel.OpenAi.Gpt6.V6Luna, "gpt-6-luna", true);
    }

    [Test]
    public void Gpt6_IsRegisteredWithCapabilities()
    {
        foreach (var (model, name, supportsNone) in Gpt6Models())
        {
            Assert.That(model.Name, Is.EqualTo(name));
            Assert.That(model.ContextTokens, Is.EqualTo(1_050_000));
            Assert.That(model.EndpointCapabilities, Is.EquivalentTo(new[]
            {
                ChatModelEndpointCapabilities.Chat, ChatModelEndpointCapabilities.Responses, ChatModelEndpointCapabilities.Batch
            }));
            Assert.That(ChatModelOpenAiGpt6.ModelsAll, Does.Contain(model));
            Assert.That(ChatModelOpenAi.ModelsAll.Count(x => x.Name == name), Is.EqualTo(1));
            Assert.That(ChatModel.OpenAi.OwnsModel(name), Is.True);
            ChatModel? resolved = ChatModel.ResolveModel(LLmProviders.OpenAi, name);
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Name, Is.EqualTo(name));
            Assert.That(resolved.Provider, Is.EqualTo(LLmProviders.OpenAi));
            Assert.That(resolved.EndpointCapabilities, Is.EquivalentTo(model.EndpointCapabilities!));
            Assert.That(ChatModelOpenAi.ReasoningModelsAll, Does.Contain(model));
            Assert.That(ChatModelOpenAi.WebSearchCompatibleModelsAll, Does.Contain(model));
            Assert.That(ChatModelOpenAi.ComputerUseModelsAllSet, Does.Contain(model));
            Assert.That(ChatModelOpenAi.ToolSearchModelsAllSet, Does.Contain(model));
            Assert.That(ChatModelOpenAi.CompactionModelsAllSet, Does.Contain(model));
            Assert.That(ChatModelOpenAi.SamplingParamsConditionallySupported.Contains(model), Is.EqualTo(supportsNone));
        }
    }

    private static IEnumerable<(ChatModel Model, ChatReasoningEfforts? Effort, bool KeepSampling)> Gpt6Requests()
    {
        foreach (var (model, _, supportsNone) in Gpt6Models())
        {
            foreach (ChatReasoningEfforts? effort in new ChatReasoningEfforts?[] { null, ChatReasoningEfforts.Low, ChatReasoningEfforts.Max })
            {
                yield return (model, effort, false);
            }

            if (supportsNone)
            {
                yield return (model, ChatReasoningEfforts.None, true);
            }
        }
    }

    [Test]
    public void Chat_SamplingAndModelId_FollowReasoningSettings()
    {
        foreach (var (model, effort, keepSampling) in Gpt6Requests())
        {
            ChatRequest request = new ChatRequest
            {
                Model = model,
                Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
                ReasoningEffort = effort,
                Temperature = 0.7,
                TopP = 0.9,
                Logprobs = true,
                TopLogprobs = 2
            };

            JObject body = JObject.Parse(request.Serialize(Provider).Body.ToString()!);

            Assert.That(body["model"]?.ToString(), Is.EqualTo(model.Name));
            Assert.That(body["temperature"]?.Value<double>(), Is.EqualTo(keepSampling ? (double?)0.7 : null));
            Assert.That(body["top_p"]?.Value<double>(), Is.EqualTo(keepSampling ? (double?)0.9 : null));
            Assert.That(body["logprobs"]?.Value<bool>(), Is.EqualTo(keepSampling ? (bool?)true : null));
            Assert.That(body["top_logprobs"]?.Value<int>(), Is.EqualTo(keepSampling ? (int?)2 : null));
            Assert.That(body["reasoning_effort"]?.ToString(), Is.EqualTo(effort?.ToString().ToLowerInvariant()));
        }

        foreach (var (model, effort, keepSampling) in Gpt6Requests())
        {
            foreach (bool samplingOnResponses in new[] { false, true })
            {
                ResponseReasoningEfforts? responseEffort = effort is null ? null : Enum.Parse<ResponseReasoningEfforts>(effort.ToString()!);
                ResponseRequest responseOptions = new ResponseRequest
                {
                    Model = model,
                    Reasoning = new ReasoningConfiguration(responseEffort, ResponseReasoningSummaries.Concise),
                    Temperature = samplingOnResponses ? 0.7 : null,
                    TopP = samplingOnResponses ? 0.9 : null,
                    TopLogprobs = samplingOnResponses ? 2 : null,
                    Include = [ResponseIncludeFields.MessageOutputTextLogprobs, ResponseIncludeFields.ReasoningEncryptedContent]
                };
                ChatRequest request = new ChatRequest
                {
                    Model = ChatModel.OpenAi.Gpt6.V6Astra,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
                    ReasoningEffort = effort is null ? ChatReasoningEfforts.Low : ChatReasoningEfforts.Max,
                    Temperature = samplingOnResponses ? null : 0.7,
                    TopP = samplingOnResponses ? null : 0.9,
                    TopLogprobs = samplingOnResponses ? null : 2,
                    ResponseRequestParameters = responseOptions
                };

                TornadoRequestContent serialized = request.Serialize(Provider);
                JObject body = JObject.Parse(serialized.Body.ToString()!);

                Assert.That(serialized.CapabilityEndpoint, Is.EqualTo(CapabilityEndpoints.Responses));
                Assert.That(body["model"]?.ToString(), Is.EqualTo(model.Name));
                Assert.That(body["reasoning"]?["effort"]?.ToString(), Is.EqualTo(effort?.ToString().ToLowerInvariant() ?? "low"));
                Assert.That(body["reasoning"]?["summary"]?.ToString(), Is.EqualTo("concise"));
                Assert.That(body["temperature"]?.Value<double>(), Is.EqualTo(keepSampling ? (double?)0.7 : null));
                Assert.That(body["top_p"]?.Value<double>(), Is.EqualTo(keepSampling ? (double?)0.9 : null));
                Assert.That(body["top_logprobs"]?.Value<int>(), Is.EqualTo(keepSampling ? (int?)2 : null));
                Assert.That(body["include"]!.Values<string>(), Does.Contain("reasoning.encrypted_content"));
                Assert.That(body["include"]!.Values<string>().Contains("message.output_text.logprobs"), Is.EqualTo(keepSampling));
                Assert.That(responseOptions.Reasoning.Effort, Is.EqualTo(responseEffort));
                Assert.That(responseOptions.Include, Does.Contain(ResponseIncludeFields.MessageOutputTextLogprobs));
            }
        }
    }

    [Test]
    public void Responses_SamplingAndModelId_FollowReasoningSettings()
    {
        foreach (var (model, effort, keepSampling) in Gpt6Requests())
        {
            ResponseRequest request = new ResponseRequest
            {
                Model = model,
                InputString = "Hello",
                Reasoning = effort is null ? null : new ReasoningConfiguration(Enum.Parse<ResponseReasoningEfforts>(effort.ToString()!)),
                Temperature = 0.7,
                TopP = 0.9,
                TopLogprobs = 2,
                Include = [ResponseIncludeFields.MessageOutputTextLogprobs, ResponseIncludeFields.ReasoningEncryptedContent]
            };

            JObject body = JObject.Parse(request.Serialize(Provider).Body.ToString()!);

            Assert.That(body["model"]?.ToString(), Is.EqualTo(model.Name));
            Assert.That(body["temperature"]?.Value<double>(), Is.EqualTo(keepSampling ? (double?)0.7 : null));
            Assert.That(body["top_p"]?.Value<double>(), Is.EqualTo(keepSampling ? (double?)0.9 : null));
            Assert.That(body["top_logprobs"]?.Value<int>(), Is.EqualTo(keepSampling ? (int?)2 : null));
            Assert.That(body["reasoning"]?["effort"]?.ToString(), Is.EqualTo(effort?.ToString().ToLowerInvariant()));
            Assert.That(body["include"]!.Values<string>(), Does.Contain("reasoning.encrypted_content"));
            Assert.That(body["include"]!.Values<string>().Contains("message.output_text.logprobs"), Is.EqualTo(keepSampling));
        }
    }

    [Test]
    public void ToolCalls_RouteToSupportedEndpoint()
    {
        foreach (var (model, effort, supportsChatTools) in Gpt6Requests())
        {
            ChatRequest request = new ChatRequest
            {
                Model = model,
                Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")],
                ReasoningEffort = effort,
                Tools = [new Tool(new ToolFunction("lookup", "Looks up a value."))]
            };

            TornadoRequestContent serialized = request.Serialize(Provider);

            Assert.That(serialized.CapabilityEndpoint,
                Is.EqualTo(supportsChatTools ? CapabilityEndpoints.Chat : CapabilityEndpoints.Responses));
            JObject body = JObject.Parse(serialized.Body.ToString()!);
            Assert.That(body["model"]?.ToString(), Is.EqualTo(model.Name));
            Assert.That(body["tools"], Is.Not.Null.And.Not.Empty);
            string effortProperty = supportsChatTools ? "reasoning_effort" : "reasoning.effort";
            Assert.That(body.SelectToken(effortProperty)?.ToString(), Is.EqualTo(effort?.ToString().ToLowerInvariant()));
        }
    }

    [Test]
    public void PlainChat_UsesChatEndpoint()
    {
        foreach (var (model, _, _) in Gpt6Models())
        {
            ChatRequest request = new ChatRequest
            {
                Model = model,
                Messages = [new ChatMessage(ChatMessageRoles.User, "Hello")]
            };

            Assert.That(request.Serialize(Provider).CapabilityEndpoint, Is.EqualTo(CapabilityEndpoints.Chat));
        }
    }

    [Test]
    public void RetiredCodexModels_AreAbsentFromCatalog()
    {
        foreach (string name in new[] { "codex-mini-latest", "computer-use-preview" })
        {
            Assert.That(ChatModelOpenAiCodex.ModelsAll.Select(x => x.Name), Does.Not.Contain(name));
            Assert.That(ChatModelOpenAi.ModelsAll.Select(x => x.Name), Does.Not.Contain(name));
            Assert.That(ChatModel.OpenAi.OwnsModel(name), Is.False);
            Assert.That(ChatModel.ResolveModel(LLmProviders.OpenAi, name), Is.Null);
        }
    }

    [Test]
    public void Gpt53Codex_RemainsAvailableUntilApiShutdown()
    {
        Assert.That(ChatModelOpenAiCodex.ModelsAll, Is.EqualTo(new[] { ChatModel.OpenAi.Codex.Gpt53Codex }));
        Assert.That(ChatModel.OpenAi.OwnsModel("gpt-5.3-codex"), Is.True);
    }
}
