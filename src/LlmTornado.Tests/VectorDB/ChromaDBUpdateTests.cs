using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.VectorDatabases;
using LlmTornado.VectorDatabases.Intergrations;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests.VectorDB;

[TestFixture]
public class ChromaDBUpdateTests
{
    [Test]
    public async Task UpdateDocuments_SendsNewEmbeddings()
    {
        using FakeChromaServer server = new FakeChromaServer();
        TornadoChromaDB db = new TornadoChromaDB($"http://127.0.0.1:{server.Port}/api/v2/");
        await db.InitializeCollection("update_embeddings");

        await db.UpdateDocumentsAsync([new VectorDocument("a", "new text") { Embedding = [0.5f, 0.25f] }]);

        JObject body = JObject.Parse(server.UpdateBodies.Single());
        Assert.That(body["ids"]!.Values<string>(), Is.EqualTo(new[] { "a" }));
        Assert.That(body["embeddings"]![0]!.Values<float>(), Is.EqualTo(new[] { 0.5f, 0.25f }));
        Assert.That(body["documents"]![0]!.Value<string>(), Is.EqualTo("new text"));
    }

    [Test]
    public async Task UpdateDocuments_WithoutEmbedding_KeepsStoredEmbedding()
    {
        using FakeChromaServer server = new FakeChromaServer();
        TornadoChromaDB db = new TornadoChromaDB($"http://127.0.0.1:{server.Port}/api/v2/");
        await db.InitializeCollection("update_text_only");

        await db.UpdateDocumentsAsync([new VectorDocument("a", "new text")]);

        JObject body = JObject.Parse(server.UpdateBodies.Single());
        Assert.That(body["embeddings"]?.Type ?? JTokenType.Null, Is.EqualTo(JTokenType.Null));
        Assert.That(body["documents"]![0]!.Value<string>(), Is.EqualTo("new text"));
    }

    private sealed class FakeChromaServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();

        public int Port { get; }
        public List<string> UpdateBodies { get; } = [];

        public FakeChromaServer()
        {
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

                string body;
                using (StreamReader reader = new StreamReader(context.Request.InputStream))
                {
                    body = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                string path = context.Request.Url!.AbsolutePath;
                string response = "{}";

                if (path.EndsWith("/version"))
                {
                    response = "\"1.0.0\"";
                }
                else if (path.EndsWith("/update"))
                {
                    UpdateBodies.Add(body);
                }
                else if (path.EndsWith("/collections") && context.Request.HttpMethod == "POST")
                {
                    string name = JObject.Parse(body)["name"]?.Value<string>() ?? "c";
                    response = $"{{\"id\":\"00000000-0000-0000-0000-000000000001\",\"name\":\"{name}\",\"tenant\":\"default_tenant\",\"database\":\"default_database\",\"dimension\":null,\"metadata\":null}}";
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
