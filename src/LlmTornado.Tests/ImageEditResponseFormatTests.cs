using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Images;
using LlmTornado.Images.Models;

namespace LlmTornado.Tests;

[TestFixture]
public class ImageEditResponseFormatTests
{
    private const string Reply = """{"created":1,"data":[{"b64_json":"AAAA"}]}""";

    [TestCase(TornadoImageResponseFormats.Base64, "b64_json")]
    [TestCase(TornadoImageResponseFormats.Url, "url")]
    public async Task MultipartEdit_SendsTheResponseFormatOpenAiAccepts(TornadoImageResponseFormats format, string expected)
    {
        using FakeServer server = new FakeServer(Reply);
        TornadoApi api = new TornadoApi(new OpenAiEndpointProvider(LLmProviders.OpenAi)
        {
            Auth = new ProviderAuthentication(LLmProviders.OpenAi, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{server.Port}/v1/{{0}}{{1}}"
        });

        await api.ImageEdit.EditImage(new ImageEditRequest(new TornadoInputFile("iVBORw0KGgo=", "image/png"), "Add a hat", responseFormat: format, model: new ImageModel("dall-e-2", LLmProviders.OpenAi)));

        Match match = Regex.Match(server.RequestBodies.Single(), "name=\"?response_format\"?\\r?\\n\\r?\\n(\\w+)");
        Assert.That(match.Success, Is.True);
        Assert.That(match.Groups[1].Value, Is.EqualTo(expected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Edit_OmitsResponseFormatAndInputFidelityForGptImage2(bool useJsonBody)
    {
        using FakeServer server = new FakeServer(Reply);
        TornadoApi api = CreateApi(server);

        await api.ImageEdit.EditImage(new ImageEditRequest(new TornadoInputFile("iVBORw0KGgo=", "image/png"), "Add a hat", responseFormat: TornadoImageResponseFormats.Base64, model: ImageModel.OpenAi.Gpt.V2)
        {
            InputFidelity = TornadoImageInputFidelity.High,
            UseJsonBody = useJsonBody,
            ImageReferences = useJsonBody ? [new ImageEditReference { Type = ImageEditReferenceType.ImageUrl, ImageUrl = "https://example.com/a.png" }] : null
        });

        string body = server.RequestBodies.Single();
        Assert.That(body, Does.Contain("gpt-image-2"));
        Assert.That(body, Does.Not.Contain("response_format"));
        Assert.That(body, Does.Not.Contain("input_fidelity"));
    }

    [Test]
    public async Task MultipartEdit_OmitsResponseFormatButKeepsInputFidelityForGptImage1()
    {
        using FakeServer server = new FakeServer(Reply);
        TornadoApi api = CreateApi(server);

        await api.ImageEdit.EditImage(new ImageEditRequest(new TornadoInputFile("iVBORw0KGgo=", "image/png"), "Add a hat", responseFormat: TornadoImageResponseFormats.Base64, model: ImageModel.OpenAi.Gpt.V1)
        {
            InputFidelity = TornadoImageInputFidelity.High
        });

        string body = server.RequestBodies.Single();
        Assert.That(body, Does.Not.Contain("response_format"));
        Assert.That(body, Does.Contain("input_fidelity"));
    }

    private static TornadoApi CreateApi(FakeServer server)
    {
        return new TornadoApi(new OpenAiEndpointProvider(LLmProviders.OpenAi)
        {
            Auth = new ProviderAuthentication(LLmProviders.OpenAi, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{server.Port}/v1/{{0}}{{1}}"
        });
    }

    private sealed class FakeServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly string response;

        public int Port { get; }
        public List<string> RequestBodies { get; } = [];

        public FakeServer(string response)
        {
            this.response = response;

            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            Port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            listener.Start();
            _ = Serve();
        }

        private async Task Serve()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // listener closed
                    return;
                }

                using (StreamReader reader = new StreamReader(context.Request.InputStream))
                {
                    RequestBodies.Add(await reader.ReadToEndAsync().ConfigureAwait(false));
                }

                byte[] bytes = Encoding.UTF8.GetBytes(response);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                context.Response.Close();
            }
        }

        public void Dispose()
        {
            listener.Close();
        }
    }
}
