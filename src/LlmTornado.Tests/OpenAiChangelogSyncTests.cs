using LlmTornado.Audio.Models;
using LlmTornado.Audio.Models.OpenAi;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Images;
using LlmTornado.Images.Models;
using LlmTornado.Images.Models.OpenAi;
using LlmTornado.Models;
using LlmTornado.Responses;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

/// <summary>
/// Catalog registration for OpenAI models and capabilities added after GPT-5.6 (July 9, 2026).
/// </summary>
[TestFixture]
public class OpenAiChangelogSyncTests
{
    [Test]
    public void Daybreak_Models_AreRegistered()
    {
        Assert.That(ChatModel.OpenAi.Daybreak.Gpt56Cyber.Name, Is.EqualTo("gpt-5.6-cyber"));
        Assert.That(ChatModel.OpenAi.Daybreak.Gpt56Cyber.ContextTokens, Is.EqualTo(400_000));
        Assert.That(ChatModel.OpenAi.Daybreak.Gpt56Cyber.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Responses));
        Assert.That(ChatModel.OpenAi.Daybreak.Gpt56Cyber.EndpointCapabilities, Does.Not.Contain(ChatModelEndpointCapabilities.Chat));
        Assert.That(ChatModel.OpenAi.Daybreak.RedLatest.Name, Is.EqualTo("gpt-daybreak-red-latest"));
        Assert.That(ChatModel.OpenAi.Daybreak.BlueLatest.Name, Is.EqualTo("gpt-daybreak-blue-latest"));
        Assert.That(ChatModelOpenAi.ReasoningModelsAll, Does.Contain(ChatModel.OpenAi.Daybreak.Gpt56Cyber));
        Assert.That(ChatModelOpenAi.ComputerUseModelsAllSet, Does.Contain(ChatModel.OpenAi.Daybreak.Gpt56Cyber));
        Assert.That(ChatModelOpenAiDaybreak.ModelsAll, Has.Count.EqualTo(3));
    }

    [Test]
    public void Rosalind_Model_IsRegistered()
    {
        Assert.That(ChatModel.OpenAi.Rosalind.Research.Name, Is.EqualTo("gpt-rosalind-research"));
        Assert.That(ChatModel.OpenAi.Rosalind.Research.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Chat));
        Assert.That(ChatModel.OpenAi.Rosalind.Research.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Responses));
        Assert.That(ChatModelOpenAi.ReasoningModelsAll, Does.Contain(ChatModel.OpenAi.Rosalind.Research));
        Assert.That(ChatModelOpenAi.WebSearchCompatibleModelsAll, Does.Contain(ChatModel.OpenAi.Rosalind.Research));
    }

    [Test]
    public void Realtime21_And_Live_Models_AreRegistered()
    {
        Assert.That(ChatModel.OpenAi.Realtime.Realtime21.Name, Is.EqualTo("gpt-realtime-2.1"));
        Assert.That(ChatModel.OpenAi.Realtime.Realtime21Mini.Name, Is.EqualTo("gpt-realtime-2.1-mini"));
        Assert.That(ChatModel.OpenAi.Realtime.Live1.Name, Is.EqualTo("gpt-live-1"));
        Assert.That(ChatModel.OpenAi.Realtime.LiveTranscribe.Name, Is.EqualTo("gpt-live-transcribe"));
        Assert.That(ChatModel.OpenAi.Realtime.Realtime21.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Realtime));
        Assert.That(ChatModel.OpenAi.Realtime.Live1.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Live));
        Assert.That(ChatModel.OpenAi.Realtime.LiveTranscribe.EndpointCapabilities, Does.Contain(ChatModelEndpointCapabilities.Realtime));
    }

    [Test]
    public void Image25_Models_AreRegistered()
    {
        Assert.That(ImageModel.OpenAi.Gpt.V25Sunburst.Name, Is.EqualTo("gpt-image-2.5-sunburst"));
        Assert.That(ImageModel.OpenAi.Gpt.V25Flare.Name, Is.EqualTo("gpt-image-2.5-flare"));
        Assert.That(ImageModel.AllModelsMap.ContainsKey("gpt-image-2.5-sunburst"), Is.True);
        Assert.That(ImageModel.AllModelsMap.ContainsKey("gpt-image-2.5-flare"), Is.True);
        Assert.That(ImageModelOpenAiGpt.ModelsAll, Does.Contain(ImageModel.OpenAi.Gpt.V25Sunburst));
        Assert.That(ImageModelOpenAiGpt.ModelsAll, Does.Contain(ImageModel.OpenAi.Gpt.V25Flare));
    }

    [Test]
    public void Transcribe_Models_AreRegistered()
    {
        Assert.That(AudioModel.OpenAi.Gpt.Transcribe.Name, Is.EqualTo("gpt-transcribe"));
        Assert.That(AudioModel.OpenAi.Gpt.LiveTranscribe.Name, Is.EqualTo("gpt-live-transcribe"));
        Assert.That(AudioModel.AllModelsMap.ContainsKey("gpt-transcribe"), Is.True);
        Assert.That(AudioModel.AllModelsMap.ContainsKey("gpt-live-transcribe"), Is.True);
        Assert.That(AudioModelOpenAi.StreamingCompatibleModels, Does.Contain(AudioModel.OpenAi.Gpt.Transcribe));
    }

    [Test]
    public void Gpt55_HasComputerUseAndCompaction()
    {
        Assert.That(ChatModelOpenAi.ComputerUseModelsAllSet, Does.Contain(ChatModel.OpenAi.Gpt55.V55));
        Assert.That(ChatModelOpenAi.ToolSearchModelsAllSet, Does.Contain(ChatModel.OpenAi.Gpt55.V55));
        Assert.That(ChatModelOpenAi.CompactionModelsAllSet, Does.Contain(ChatModel.OpenAi.Gpt55.V55Pro));
    }

    [Test]
    public void ImageQuality_IncludesXHighAndMax()
    {
        Assert.That(TornadoImageQualities.XHigh.ToString(), Is.EqualTo("XHigh"));
        Assert.That(TornadoImageQualities.Max.ToString(), Is.EqualTo("Max"));
    }

    [Test]
    public void ServiceTiers_IncludeFastAndUltrafast()
    {
        foreach (var (tier, value) in new[]
        {
            (ChatRequestServiceTiers.Fast, "fast"),
            (ChatRequestServiceTiers.Ultrafast, "ultrafast")
        })
        {
            ResponseRequest request = new ResponseRequest
            {
                Model = ChatModel.OpenAi.Gpt6.V6Astra,
                InputString = "Hello",
                ServiceTier = tier
            };
            JObject body = JObject.Parse(request.Serialize(new TornadoApi("test-key").GetProvider(LLmProviders.OpenAi)).Body.ToString()!);
            Assert.That(body["service_tier"]?.ToString(), Is.EqualTo(value));
        }
    }

    [Test]
    public void OmniModerationLatest_IsRegistered()
    {
        Assert.That(Model.OmniModerationLatest.Name, Is.EqualTo("omni-moderation-latest"));
    }
}
