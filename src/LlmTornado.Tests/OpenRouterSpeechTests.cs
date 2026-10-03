using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Audio;
using LlmTornado.Audio.Models;
using LlmTornado.Audio.Models.OpenRouter;
using LlmTornado.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

/// <summary>
/// Offline checks against /api/v1/models?output_modalities=speech on October 3, 2026.
/// </summary>
[TestFixture]
public class OpenRouterSpeechTests
{
    private static readonly Dictionary<string, int> ExpectedModels = new Dictionary<string, int>
    {
        ["bytedance-seed/seed-audio-1-0"] = 0,
        ["canopylabs/orpheus-3b-0.1-ft"] = 4096,
        ["deepgram/aura-2"] = 0,
        ["deepgram/flux-tts"] = 0,
        ["fish-audio/s1"] = 0,
        ["fish-audio/s2-pro"] = 0,
        ["fish-audio/s2.1-pro"] = 0,
        ["fish-audio/s2.1-pro-free:free"] = 0,
        ["google/gemini-3.1-flash-tts-preview"] = 32768,
        ["google/gemini-3.8-flash-lite-tts"] = 32768,
        ["google/gemini-3.8-flash-tts"] = 32768,
        ["hexgrad/kokoro-82m"] = 4096,
        ["microsoft/mai-voice-2"] = 0,
        ["microsoft/mai-voice-2-flash"] = 0,
        ["microsoft/mai-voice-2.1"] = 0,
        ["microsoft/mai-voice-2.1-flash"] = 0,
        ["minimax/speech-2.8-hd"] = 0,
        ["minimax/speech-2.8-turbo"] = 0,
        ["mistralai/voxtral-mini-tts-2603"] = 4096,
        ["qwen/qwen-audio-3.0-tts-flash"] = 0,
        ["qwen/qwen-audio-3.0-tts-plus"] = 0,
        ["sesame/csm-1b"] = 4096,
        ["x-ai/grok-voice-tts-1.0"] = 15000
    };

    [Test]
    public void Catalog_ContainsAll23SpeechIdsAndContexts()
    {
        Assert.That(AudioModel.OpenRouter.Provider, Is.EqualTo(LLmProviders.OpenRouter));
        Assert.That(AudioModel.OpenRouter.Speech.AllModels.Select(x => x.Name), Is.EquivalentTo(ExpectedModels.Keys));
        Assert.That(AudioModelOpenRouterSpeech.ModelsAll, Has.Count.EqualTo(23));
        foreach (var (id, context) in ExpectedModels)
        {
            var model = AudioModel.OpenRouter.AllModels.Cast<AudioModel>().Single(x => x.Name == id);
            Assert.That(model.Provider, Is.EqualTo(LLmProviders.OpenRouter), id);
            Assert.That(model.ContextTokens, Is.EqualTo(context), id);
            Assert.That(AudioModel.AllModelsMap[id], Is.SameAs(model), id);
            Assert.That(AudioModel.OpenRouter.OwnsModel(id), Is.True, id);
            AudioModel inferred = id;
            Assert.That(inferred.Provider, Is.EqualTo(LLmProviders.OpenRouter), id);
        }
    }

    [Test]
    public void SpeechRequests_SerializeEveryOpenRouterModelId()
    {
        TornadoApi api = new TornadoApi(LLmProviders.OpenRouter, "test-key");
        foreach (var model in AudioModelOpenRouterSpeech.ModelsAll.Cast<AudioModel>())
        {
            SpeechRequest request = new SpeechRequest
            {
                Model = model,
                Input = "Hello",
                Voice = SpeechVoice.Custom("catalog-voice")
            };
            JObject body = JObject.Parse(JsonConvert.SerializeObject(request, EndpointBase.NullSettings));
            Assert.That(body["model"]?.ToString(), Is.EqualTo(model.Name));
            Assert.That(body["voice"]?.ToString(), Is.EqualTo("catalog-voice"));
            var provider = api.GetProvider(request.Model);
            Assert.That(provider.Provider, Is.EqualTo(LLmProviders.OpenRouter));
            Assert.That(provider.ApiUrl(CapabilityEndpoints.Audio, "/speech"),
                Is.EqualTo("https://openrouter.ai/api/v1/audio/speech"));
        }
    }

    [Test]
    public async Task CreateSpeech_RoutesToOpenRouterAndReturnsAudioBytes()
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        string prefix = $"http://127.0.0.1:{port}/";
        using HttpListener listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        byte[] expectedAudio = [0x49, 0x44, 0x33, 0x01, 0x02];
        string? path = null;
        string? authorization = null;
        JObject? sent = null;
        Task serving = ServeRequest();
        try
        {
            TornadoApi api = new TornadoApi(new Uri(prefix), "test-key", LLmProviders.OpenRouter);
            SpeechTtsResult? result = await api.Audio.CreateSpeech(new SpeechRequest
            {
                Model = AudioModel.OpenRouter.Speech.MaiVoice21Flash,
                Input = "Hello from OpenRouter.",
                Voice = SpeechVoice.Custom("en-US-Harper:MAI-Voice-2.1-Flash")
            }).WaitAsync(timeout.Token);
            await serving.WaitAsync(timeout.Token);
            Assert.That(result, Is.Not.Null);
            using MemoryStream audio = new MemoryStream();
            await result!.AudioStream.CopyToAsync(audio, timeout.Token);
            await result.AudioStream.DisposeAsync();
            result.Response.Dispose();
            Assert.That(audio.ToArray(), Is.EqualTo(expectedAudio));
            Assert.That(path, Is.EqualTo("/v1/audio/speech"));
            Assert.That(authorization, Is.EqualTo("Bearer test-key"));
            Assert.That(sent!["model"]?.ToString(), Is.EqualTo("microsoft/mai-voice-2.1-flash"));
            Assert.That(sent["voice"]?.ToString(), Is.EqualTo("en-US-Harper:MAI-Voice-2.1-Flash"));
            Assert.That(sent["input"]?.ToString(), Is.EqualTo("Hello from OpenRouter."));
        }
        finally
        {
            listener.Close();
            try
            {
                await serving;
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
            {
                // The listener was closed before a request arrived.
            }
        }

        async Task ServeRequest()
        {
            HttpListenerContext context = await listener.GetContextAsync().WaitAsync(timeout.Token);
            path = context.Request.Url!.AbsolutePath;
            authorization = context.Request.Headers["Authorization"];
            using StreamReader reader = new StreamReader(context.Request.InputStream);
            sent = JObject.Parse(await reader.ReadToEndAsync(timeout.Token));
            context.Response.ContentType = "audio/mpeg";
            context.Response.ContentLength64 = expectedAudio.Length;
            await context.Response.OutputStream.WriteAsync(expectedAudio, timeout.Token);
            context.Response.Close();
        }
    }
}
