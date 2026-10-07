using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Models;

namespace LlmTornado.Tests;

/// <summary>
/// Offline tests of the models endpoint error handling. Requests are served by a local stub, no API key is needed.
/// </summary>
[TestFixture]
public class ModelsEndpointTests
{
    private const string ModelsJson = """
        {
          "object": "list",
          "data": [
            { "id": "model-a", "object": "model", "created": 1700000000, "owned_by": "stub" },
            { "id": "model-b", "object": "model", "created": 1700000001, "owned_by": "stub" }
          ]
        }
        """;

    private const string ErrorJson = """{"error":{"message":"Incorrect API key provided","type":"invalid_request_error"}}""";

    private HttpListener? _listener;
    private int _port;
    private volatile int _status = 200;
    private volatile string _body = ModelsJson;
    private string? _query;

    [SetUp]
    public void SetUp()
    {
        _port = GetFreePort();
        _status = 200;
        _body = ModelsJson;
        _query = null;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        _listener.Start();
        _ = Serve(_listener);
    }

    [TearDown]
    public void TearDown()
    {
        _listener?.Close();
        _listener = null;
    }

    [Test]
    public async Task GetModels_Success_ReturnsModels()
    {
        List<RetrievedModel>? models = await Api(_port).Models.GetModels();

        Assert.That(models, Is.Not.Null);
        Assert.That(models!.Select(x => x.Id), Is.EqualTo(new[] { "model-a", "model-b" }));
    }

    [Test]
    public async Task GetModelsSafe_Success_ReturnsOkResult()
    {
        HttpCallResult<List<RetrievedModel>> result = await Api(_port).Models.GetModelsSafe();

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Exception, Is.Null);
        Assert.That(result.Code, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result.Data!.Select(x => x.Id), Is.EqualTo(new[] { "model-a", "model-b" }));
    }

    [Test]
    public async Task GetModels_OpenRouter_IncludesAllModalitiesAndPreservesMultipleValues()
    {
        Respond(200, """
            { "data": [
              { "id": "hybrid", "architecture": { "input_modalities": ["text", "image"], "output_modalities": ["text", "decisions"] }, "supported_parameters": ["state", "questions"] },
              { "id": "decision-only", "architecture": { "output_modalities": ["decisions"] } }
            ] }
            """);
        List<RetrievedModel>? models = await Api(_port, LLmProviders.OpenRouter).Models.GetModels(LLmProviders.OpenRouter);

        Assert.That(_query, Is.EqualTo("?output_modalities=all"));
        Assert.That(models, Has.Count.EqualTo(2));
        Assert.That(models![0].Architecture!.InputModalities, Is.EqualTo(new[] { "text", "image" }));
        Assert.That(models[0].Architecture!.OutputModalities, Is.EqualTo(new[] { "text", "decisions" }));
        Assert.That(models[0].SupportedParameters, Is.EqualTo(new[] { "state", "questions" }));
        Assert.That(models.Where(x => x.Architecture?.OutputModalities?.Contains("decisions") == true)
            .Select(x => x.Id), Is.EqualTo(new[] { "hybrid", "decision-only" }));
    }

    [Test]
    public void GetModels_HttpError_Throws()
    {
        Respond(401, ErrorJson);

        HttpRequestException? exception = Assert.ThrowsAsync<HttpRequestException>(async () => await Api(_port).Models.GetModels());

        Assert.That(exception!.Message, Does.Contain("Incorrect API key provided"));
    }

    [Test]
    public async Task GetModelsSafe_HttpError_ReturnsError()
    {
        Respond(401, ErrorJson);

        HttpCallResult<List<RetrievedModel>> result = await Api(_port).Models.GetModelsSafe();

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Data, Is.Null);
        Assert.That(result.Code, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(result.Response, Does.Contain("Incorrect API key provided"));
        Assert.That(result.Exception, Is.InstanceOf<HttpRequestException>());
    }

    [Test]
    public void GetModels_InvalidResponse_Throws()
    {
        Respond(200, "this is not json");

        Assert.That(async () => await Api(_port).Models.GetModels(), Throws.Exception);
    }

    [Test]
    public async Task GetModelsSafe_InvalidResponse_ReturnsError()
    {
        Respond(200, "this is not json");

        HttpCallResult<List<RetrievedModel>> result = await Api(_port).Models.GetModelsSafe();

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Data, Is.Null);
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public void GetModels_ConnectionRefused_Throws()
    {
        int closedPort = GetFreePort();

        Assert.That(async () => await Api(closedPort).Models.GetModels(), Throws.Exception);
    }

    [Test]
    public async Task GetModelsSafe_ConnectionRefused_ReturnsError()
    {
        int closedPort = GetFreePort();

        HttpCallResult<List<RetrievedModel>> result = await Api(closedPort).Models.GetModelsSafe();

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Data, Is.Null);
        Assert.That(result.Exception, Is.Not.Null);
    }

    private static TornadoApi Api(int port, LLmProviders provider = LLmProviders.OpenAi)
    {
        return new TornadoApi(provider, "test-key")
        {
            ApiUrlFormat = $"http://127.0.0.1:{port}/{{0}}/{{1}}"
        };
    }

    private void Respond(int status, string body)
    {
        _status = status;
        _body = body;
    }

    private async Task Serve(HttpListener listener)
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

            _query = context.Request.Url!.Query;
            byte[] body = Encoding.UTF8.GetBytes(_body);
            context.Response.StatusCode = _status;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
            context.Response.Close();
        }
    }

    private static int GetFreePort()
    {
        TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
