using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Responses;

namespace LlmTornado.Tests;

[TestFixture]
public class ResponsesInputTokenCancellationTests
{
    private static readonly TimeSpan ServerDelay = TimeSpan.FromSeconds(10);

    [Test]
    public void Count_AbortsWhenRequestCancellationTokenIsCanceled()
    {
        using SlowServer server = new SlowServer(ServerDelay);
        TornadoApi api = CreateApi(server.Port);
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt4.O,
            InputString = "Count these tokens",
            CancellationToken = cts.Token
        };

        Stopwatch watch = Stopwatch.StartNew();

        Assert.CatchAsync<OperationCanceledException>(async () => await api.Responses.CountInputTokens(request));
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void Count_AbortsWhenMethodCancellationTokenIsCanceled()
    {
        using SlowServer server = new SlowServer(ServerDelay);
        TornadoApi api = CreateApi(server.Port);
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt4.O,
            InputString = "Count these tokens"
        };

        Stopwatch watch = Stopwatch.StartNew();

        Assert.CatchAsync<OperationCanceledException>(async () => await api.Responses.CountInputTokens(request, cts.Token));
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void Count_AbortsWhenOnlyRequestTokenIsCanceledAndMethodTokenIsLive()
    {
        using SlowServer server = new SlowServer(ServerDelay);
        TornadoApi api = CreateApi(server.Port);
        using CancellationTokenSource requestCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using CancellationTokenSource methodCts = new CancellationTokenSource();
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt4.O,
            InputString = "Count these tokens",
            CancellationToken = requestCts.Token
        };

        Stopwatch watch = Stopwatch.StartNew();

        Assert.CatchAsync<OperationCanceledException>(async () => await api.Responses.CountInputTokens(request, methodCts.Token));
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void Count_AbortsWhenOnlyMethodTokenIsCanceledAndRequestTokenIsLive()
    {
        using SlowServer server = new SlowServer(ServerDelay);
        TornadoApi api = CreateApi(server.Port);
        using CancellationTokenSource requestCts = new CancellationTokenSource();
        using CancellationTokenSource methodCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt4.O,
            InputString = "Count these tokens",
            CancellationToken = requestCts.Token
        };

        Stopwatch watch = Stopwatch.StartNew();

        Assert.CatchAsync<OperationCanceledException>(async () => await api.Responses.CountInputTokens(request, methodCts.Token));
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
    }

    private static TornadoApi CreateApi(int port)
    {
        return new TornadoApi(new OpenAiEndpointProvider(LLmProviders.OpenAi)
        {
            Auth = new ProviderAuthentication(LLmProviders.OpenAi, "test-key"),
            UrlResolver = (_, _, _) => $"http://127.0.0.1:{port}/v1/{{0}}{{1}}"
        });
    }

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
                byte[] bytes = Encoding.UTF8.GetBytes("{}");
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
