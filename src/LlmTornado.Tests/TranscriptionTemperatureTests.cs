using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using LlmTornado.Audio;
using LlmTornado.Audio.Models;
using LlmTornado.Audio.Models.OpenAi;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;

namespace LlmTornado.Tests;

[TestFixture]
public class TranscriptionTemperatureTests
{
    private static async Task<string> PostTranscription(float? temperature)
    {
        TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        using HttpListener listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        Task<string> served = Task.Run(async () =>
        {
            HttpListenerContext context = await listener.GetContextAsync();
            using StreamReader reader = new StreamReader(context.Request.InputStream, Encoding.Latin1);
            string body = await reader.ReadToEndAsync();
            byte[] bytes = Encoding.UTF8.GetBytes("{\"text\":\"hello\"}");
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
            return body;
        });

        TornadoApi api = new TornadoApi(new OpenAiEndpointProvider(LLmProviders.OpenAi)
        {
            Auth = new ProviderAuthentication(LLmProviders.OpenAi, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{port}/v1/{{0}}{{1}}"
        });

        await api.Audio.CreateTranscription(new TranscriptionRequest
        {
            Model = AudioModel.OpenAi.Whisper.V2,
            File = new AudioFile([1, 2, 3], AudioFileTypes.Wav),
            Temperature = temperature
        });

        return await served;
    }

    private static string? Field(string multipart, string name)
    {
        Match match = Regex.Match(multipart, $"name=\"?{Regex.Escape(name)}\"?\\r\\n\\r\\n([^\\r]*)\\r\\n");
        return match.Success ? match.Groups[1].Value : null;
    }

    [Test]
    public async Task Temperature_SetByTheCaller_IsSent()
    {
        string body = await PostTranscription(0.4f);

        Assert.That(Field(body, "temperature"), Is.EqualTo("0.4"));
    }

    [Test]
    public async Task Temperature_Unset_DefaultsToZero()
    {
        string body = await PostTranscription(null);

        Assert.That(Field(body, "temperature"), Is.EqualTo("0"));
    }

    [TestCase("gpt-4o-transcribe", "json")]
    [TestCase("gpt-4o-mini-transcribe", "json")]
    [TestCase("whisper-1", "verbose_json")]
    public void ResponseFormat_Unset_DefaultsToWhatTheModelAccepts(string model, string expected)
    {
        TranscriptionRequest request = new TranscriptionRequest
        {
            Model = new AudioModel(model, LLmProviders.OpenAi)
        };

        Assert.That(request.GetResponseFormat, Is.EqualTo(expected));
    }

    [Test]
    public void ResponseFormat_SetByTheCaller_IsKept()
    {
        TranscriptionRequest request = new TranscriptionRequest
        {
            Model = AudioModelOpenAiGpt4.Model4OTranscribe,
            ResponseFormat = AudioTranscriptionResponseFormats.Text
        };

        Assert.That(request.GetResponseFormat, Is.EqualTo("text"));
    }
}
