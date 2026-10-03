using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Demo;
using LlmTornado.Realtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
[Category("Integration")]
public class RealtimeTranscriptionTests
{
    [SetUp]
    public async Task Setup()
    {
        await Program.SetupApi();
    }

    [Test]
    public void TranscriptionDefaults_UseCurrentGaSchema()
    {
        RealtimeTranscriptionSessionConfig config = RealtimeTranscriptionSessionConfig.ForLiveTranscribe("de");
        config.Audio!.Input!.Transcription!.Keywords = ["LLMTornado"];
        JObject update = JObject.Parse(JsonConvert.SerializeObject(RealtimeClientEvents.SessionUpdate(config), EndpointBase.NullSettings));
        Assert.That(update["type"]?.ToString(), Is.EqualTo("session.update"));
        Assert.That(update.SelectToken("session.type")?.ToString(), Is.EqualTo("transcription"));
        Assert.That(update.SelectToken("session.audio.input.transcription.model")?.ToString(), Is.EqualTo("gpt-live-transcribe"));
        Assert.That(update.SelectToken("session.audio.input.transcription.languages")!.Values<string>(), Is.EqualTo(new[] { "de" }));
        Assert.That(update.SelectToken("session.audio.input.transcription.language"), Is.Null);
        Assert.That(update.SelectToken("session.audio.input.transcription.keywords")!.Values<string>(), Is.EqualTo(new[] { "LLMTornado" }));
        Assert.That(update.SelectToken("session.audio.input.turn_detection")?.Type, Is.EqualTo(JTokenType.Null));
        JObject unspecified = JObject.Parse(JsonConvert.SerializeObject(new RealtimeAudioInputConfig(), EndpointBase.NullSettings));
        Assert.That(unspecified["turn_detection"], Is.Null);
    }

    [Test]
    public async Task CreateClientSecretForTranscription_ReturnsEphemeralKey()
    {
        TornadoApi api = Program.Connect();

        HttpCallResult<RealtimeClientSecretResponse> result =
            await api.Realtime.CreateClientSecretForTranscription(
                RealtimeTranscriptionSessionConfig.ForRealtimeWhisper("en"));

        Assert.That(result.Ok, Is.True, result.Response);
        Assert.That(result.Data?.Value, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Data!.ExpiresAt, Is.GreaterThan(0));
    }

    [Test]
    public async Task TranscribeStreamingAsync_ProducesTranscript()
    {
        TornadoApi api = Program.Connect();
        string pcmPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Static", "Audio", "sample.pcm");

        Assert.That(File.Exists(pcmPath), Is.True, $"Missing test audio: {pcmPath}");

        byte[] pcm = await File.ReadAllBytesAsync(pcmPath);
        byte[] clip = pcm.Length > 24000 * 2 * 3 ? pcm[..(24000 * 2 * 3)] : pcm;

        RealtimeTranscriptionResult result = await api.Realtime.TranscribeStreamingAsync(
            clip,
            RealtimeTranscriptionSessionConfig.ForRealtimeWhisper("en", "low"));

        TestContext.WriteLine($"Final: {result.FinalTranscript}");
        TestContext.WriteLine($"Partial: {result.PartialTranscript}");
        TestContext.WriteLine($"Deltas: {result.Deltas.Count}");
        if (result.Errors.Count > 0)
        {
            TestContext.WriteLine($"Errors: {string.Join("; ", result.Errors)}");
        }

        string? text = result.FinalTranscript ?? result.PartialTranscript;
        Assert.That(text, Is.Not.Null.And.Not.Empty, "Expected non-empty transcript from streaming STT");
    }

    [Test]
    public void GptRealtimeWhisper_Model_IsRegistered()
    {
        ChatModel model = ChatModel.OpenAi.Realtime.RealtimeWhisper;
        Assert.That(model.Name, Is.EqualTo("gpt-realtime-whisper"));
        Assert.That(model.Provider, Is.EqualTo(LLmProviders.OpenAi));
        Assert.That(model.ContextTokens, Is.EqualTo(16_000));
    }
}
