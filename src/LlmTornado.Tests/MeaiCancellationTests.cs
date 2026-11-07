using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Microsoft.Extensions.AI;
using Microsoft.Extensions.AI;

namespace LlmTornado.Tests;

[TestFixture]
public class MeaiCancellationTests
{
    private static readonly TimeSpan ServerDelay = TimeSpan.FromSeconds(10);

    [Test]
    public void GetResponseAsync_AbortsTheRequestWhenTheTokenIsCanceled()
    {
        using SlowServer server = new SlowServer(ServerDelay);
        IChatClient client = CreateClient(server.Port);
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Stopwatch watch = Stopwatch.StartNew();

        Assert.CatchAsync<OperationCanceledException>(async () => await client.GetResponseAsync(Prompt(), cancellationToken: cts.Token));
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void GetStreamingResponseAsync_AbortsTheRequestWhenTheTokenIsCanceled()
    {
        using SlowServer server = new SlowServer(ServerDelay);
        IChatClient client = CreateClient(server.Port);
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Stopwatch watch = Stopwatch.StartNew();

        Assert.CatchAsync<OperationCanceledException>(async () =>
        {
            await foreach (ChatResponseUpdate _ in client.GetStreamingResponseAsync(Prompt(), cancellationToken: cts.Token))
            {
            }
        });
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void GenerateEmbeddingAsync_AbortsTheRequestWhenTheTokenIsCanceled()
    {
        using SlowServer server = new SlowServer(ServerDelay);
        IEmbeddingGenerator<string, Embedding<float>> generator = CreateEmbeddingGenerator(server.Port);
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Stopwatch watch = Stopwatch.StartNew();

        Assert.CatchAsync<OperationCanceledException>(async () => await generator.GenerateAsync(["hello"], cancellationToken: cts.Token));
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void GenerateImageAsync_AbortsTheRequestWhenTheTokenIsCanceled()
    {
        using SlowServer server = new SlowServer(ServerDelay);
        IImageGenerator generator = CreateImageGenerator(server.Port);
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Stopwatch watch = Stopwatch.StartNew();

        Assert.CatchAsync<OperationCanceledException>(async () => await generator.GenerateAsync(new ImageGenerationRequest("a red fox"), cancellationToken: cts.Token));
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }

    private static IChatClient CreateClient(int port)
    {
        TornadoApi api = new TornadoApi(new Uri($"http://127.0.0.1:{port}/v1"), "test-key", LLmProviders.OpenAi);
        return api.AsChatClient(new ChatModel("gpt-test", LLmProviders.OpenAi));
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(int port)
    {
        TornadoApi api = new TornadoApi(new Uri($"http://127.0.0.1:{port}/v1"), "test-key", LLmProviders.OpenAi);
        return api.AsEmbeddingGenerator("text-embedding-3-small");
    }

    private static IImageGenerator CreateImageGenerator(int port)
    {
        TornadoApi api = new TornadoApi(new Uri($"http://127.0.0.1:{port}/v1"), "test-key", LLmProviders.OpenAi);
        return api.AsImageGenerator("dall-e-3");
    }

    private static List<global::Microsoft.Extensions.AI.ChatMessage> Prompt() =>
    [
        new global::Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "hello")
    ];

    /// <summary>Accepts a request and answers only after the delay, or when the client goes away.</summary>
    private sealed class SlowServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly TimeSpan delay;
        private readonly CancellationTokenSource stop = new CancellationTokenSource();

        public int Port { get; }

        public SlowServer(TimeSpan delay)
        {
            this.delay = delay;

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
                    return;
                }

                _ = Respond(context);
            }
        }

        private async Task Respond(HttpListenerContext context)
        {
            try
            {
                using (StreamReader reader = new StreamReader(context.Request.InputStream))
                {
                    await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                await Task.Delay(delay, stop.Token).ConfigureAwait(false);
                byte[] bytes = Encoding.UTF8.GetBytes("""{"id":"x","choices":[{"index":0,"message":{"role":"assistant","content":"late"},"finish_reason":"stop"}]}""");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                context.Response.Close();
            }
            catch (Exception)
            {
                // the client went away or the test ended
            }
        }

        public void Dispose()
        {
            stop.Cancel();
            listener.Close();
        }
    }
}
