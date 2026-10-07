using System.Net;
using System.Net.Sockets;
using System.Text;
using LlmTornado.Code;
using LlmTornado.Decision;
using LlmTornado.Decision.Models;
using LlmTornado.Decision.Vendors.OpenRouter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace LlmTornado.Tests;

[TestFixture]
public class OpenRouterDecisionTests
{
    private static readonly Dictionary<string, int> ExpectedModels = new Dictionary<string, int>
    {
        ["~typesafe/jev-latest"] = 32000,
        ["cloudflare/clef"] = 65536,
        ["cloudflare/clef-flash"] = 65536,
        ["inception/mercury-decide:free"] = 32768,
        ["jaredpalmer/kev-4b"] = 8192,
        ["liquid/d1"] = 65536,
        ["openai/gpt-6-luna-decisions"] = 1050000,
        ["perplexity/pplx-decider-v1-27b"] = 262144,
        ["respan/span-01"] = 0,
        ["respan/span-01-lite"] = 0,
        ["respan/span-01-lite:free"] = 0,
        ["typesafe/jev-1.13"] = 32000,
        ["upstage/solar-decide"] = 524288
    };

    [Test]
    public void ModelsAndUrls_ResolveOpenRouterAndPreserveTypeSafe()
    {
        TornadoApi api = new TornadoApi(LLmProviders.OpenRouter, "or-test");
        IEndpointProvider provider = api.GetProvider(LLmProviders.OpenRouter);
        DecisionRequest request = new DecisionRequest(DecisionModel.OpenRouter.All.Gpt6LunaDecisions, "Hello")
            .AddNoul("greeting", "Is this a greeting?");
        Assert.That(request.Serialize(provider).Url, Is.EqualTo("https://openrouter.ai/api/alpha/decisions"));
        Assert.That(provider.ApiUrl(CapabilityEndpoints.Chat, null), Is.EqualTo("https://openrouter.ai/api/v1/chat/completions"));
        Assert.That(provider.ApiUrl(CapabilityEndpoints.Models, null), Is.EqualTo("https://openrouter.ai/api/v1/models"));

        api.ApiUrlFormat = "https://gateway.test/{0}/{1}";
        Assert.That(request.Serialize(provider).Url, Is.EqualTo("https://gateway.test/alpha/decisions"));
        Assert.That(provider.ApiUrl(CapabilityEndpoints.Audio, "/speech"), Is.EqualTo("https://gateway.test/v1/audio/speech"));
        Assert.That(DecisionModel.OpenRouter.AllModels.Select(x => x.Name), Is.EquivalentTo(ExpectedModels.Keys));
        foreach (var (slug, context) in ExpectedModels)
        {
            DecisionModel inferred = slug;
            DecisionModel known = (DecisionModel)DecisionModel.AllModelsMap[slug];
            Assert.That(inferred.Provider, Is.EqualTo(LLmProviders.OpenRouter), slug);
            Assert.That(known.Provider, Is.EqualTo(LLmProviders.OpenRouter), slug);
            Assert.That(known.ContextTokens, Is.EqualTo(context), slug);
            Assert.That(DecisionModel.OpenRouter.OwnsModel(slug), Is.True, slug);
        }
        Assert.That(DecisionModel.OpenRouter.OwnsModel("togethercomputer/tev1-4b-experimental"), Is.False);
        DecisionModel native = "jev-1.13.0";
        Assert.That(native.Provider, Is.EqualTo(LLmProviders.TypeSafe));
        Assert.That(DecisionModel.TypeSafe.AllModels, Has.Count.EqualTo(3));
    }

    [Test]
    public void Request_PreservesQuestionNullsMultimodalStateAndRoutingRestrictions()
    {
        JArray state = new JArray(
            new JObject { ["type"] = "text", ["text"] = "Evaluate this image." },
            new JObject { ["type"] = "image_url", ["image_url"] = new JObject { ["url"] = "https://example.test/image.png" } });
        DecisionRequest request = new DecisionRequest(DecisionModel.OpenRouter.All.Gpt6LunaDecisions, state)
            .AddNoul("contains_text", "Does the image contain text?")
            .AddChoice("kind", "Which type is it?", new Dictionary<string, object?> { ["document"] = "A document", ["other"] = null })
            .AddScore("readable", "How readable is it?", "Unreadable", "Readable");
        request.VendorExtensions = new DecisionRequestVendorExtensions(new DecisionRequestVendorOpenRouterExtensions
        {
            Provider = new DecisionOpenRouterProviderPreferences
            {
                Zdr = true,
                AllowFallbacks = false,
                DataCollection = "deny",
                Only = ["openai"]
            },
            SessionId = "session-1",
            User = "user-1",
            Trace = new Dictionary<string, object?> { ["trace_id"] = "trace-1", ["custom"] = new { stage = "classify" } }
        });
        JObject body = Parse(request, LLmProviders.OpenRouter);
        Assert.That(JToken.DeepEquals(body["state"], state), Is.True);
        Assert.That(body["model"]?.ToString(), Is.EqualTo("openai/gpt-6-luna-decisions"));
        Assert.That(body.SelectToken("provider.zdr")?.Value<bool>(), Is.True);
        Assert.That(body.SelectToken("provider.allow_fallbacks")?.Value<bool>(), Is.False);
        Assert.That(body.SelectToken("provider.data_collection")?.ToString(), Is.EqualTo("deny"));
        Assert.That(body.SelectToken("provider.only")!.Values<string>(), Is.EqualTo(new[] { "openai" }));
        Assert.That(body["session_id"]?.ToString(), Is.EqualTo("session-1"));
        Assert.That(body["user"]?.ToString(), Is.EqualTo("user-1"));
        Assert.That(body.SelectToken("trace.custom.stage")?.ToString(), Is.EqualTo("classify"));
        Assert.That(body.SelectToken("questions.kind.criteria.other")?.Type, Is.EqualTo(JTokenType.Null));
        Assert.That(body["questions"]!.Children<JProperty>().Select(x => x.Value["type"]?.ToString()),
            Is.EquivalentTo(new[] { "noul", "choice", "score" }));

        request.Model = DecisionModel.TypeSafe.Jev.Latest;
        JObject direct = Parse(request, LLmProviders.TypeSafe);
        Assert.That(direct.Properties().Select(x => x.Name), Is.EquivalentTo(new[] { "model", "state", "questions" }));
        request.Model = DecisionModel.OpenRouter.All.Jev113;
        request.VendorExtensions = new DecisionRequestVendorExtensions(new DecisionRequestVendorOpenRouterExtensions());
        Assert.That(Parse(request, LLmProviders.OpenRouter).Properties().Select(x => x.Name),
            Is.EquivalentTo(new[] { "model", "state", "questions" }));

        foreach (var (yes, no) in new (object?, object?)[] { ("Yes", null), (null, "No"), (null, null) })
        {
            request.Questions["incomplete"] = new DecisionNoul("Is this urgent?", yes, no);
            ArgumentException? error = Assert.Throws<ArgumentException>(() => Parse(request, LLmProviders.OpenRouter));
            Assert.That(error!.Message, Does.Contain("incomplete").And.Contain("true").And.Contain("false"));
            Assert.That(error.ParamName, Is.EqualTo(nameof(DecisionRequest.Questions)));
            Assert.That(Parse(request, LLmProviders.TypeSafe)["questions"]!["incomplete"]!["criteria"], Is.Not.Null);
        }
        request.Questions["incomplete"] = new DecisionNoul("Is this urgent?", "Yes", "No");
        Assert.That(Parse(request, LLmProviders.OpenRouter).SelectToken("questions.incomplete.criteria.false")!.Value<string>(), Is.EqualTo("No"));
        request.Questions["incomplete"] = new DecisionNoul("Is this urgent?");
        Assert.That(((JObject)Parse(request, LLmProviders.OpenRouter)["questions"]!["incomplete"]!).ContainsKey("criteria"), Is.False);
    }

    [Test]
    [NonParallelizable]
    public void Request_PreservesGlobalSerializerSettingsForBothProviders()
    {
        Func<JsonSerializerSettings>? original = JsonConvert.DefaultSettings;
        try
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                StringEscapeHandling = StringEscapeHandling.EscapeHtml,
                Converters = { new StringEnumConverter() }
            };
            DecisionRequest request = new DecisionRequest(DecisionModel.TypeSafe.Jev.Latest, new { TicketStatus = TicketStatus.Open, HtmlContent = "<tag>" })
                .AddNoul("matches", new { CaseStatus = TicketStatus.Closed });
            foreach (LLmProviders provider in new[] { LLmProviders.TypeSafe, LLmProviders.OpenRouter })
            {
                request.Model = provider is LLmProviders.TypeSafe ? DecisionModel.TypeSafe.Jev.Latest : DecisionModel.OpenRouter.All.Jev113;
                JObject body = Parse(request, provider);
                Assert.That(body.SelectToken("state.ticketStatus")?.Value<string>(), Is.EqualTo("Open"));
                Assert.That(body.SelectToken("questions.matches.instructions.caseStatus")?.Value<string>(), Is.EqualTo("Closed"));
                Assert.That(request.Serialize(new TornadoApi(provider, "test-key").GetProvider(provider)).Body.ToString(), Does.Contain("\\u003ctag\\u003e"));
            }
        }
        finally
        {
            JsonConvert.DefaultSettings = original;
        }
    }

    private enum TicketStatus { Open, Closed }

    [Test]
    public void Result_MapsAnswersAndOpenRouterGenerationMetadata()
    {
        DecisionResult result = JsonConvert.DeserializeObject<DecisionResult>(SuccessResponse)!;
        Assert.That(result.Id, Is.EqualTo("gen-decision-1"));
        Assert.That(result.Model, Is.EqualTo("openai/gpt-6-luna-decisions-20261006"));
        Assert.That(result.UpstreamProvider, Is.EqualTo("OpenAI"));
        Assert.That(result.Usage?.InputTokens, Is.EqualTo(476));
        Assert.That(result.Usage?.OutputTokens, Is.EqualTo(70));
        Assert.That(result.Usage?.Cost, Is.EqualTo(0.000019992m));
        Assert.That(result.GetNoul("is_bug")?.Noul, Is.EqualTo(0.96));
        Assert.That(result.GetChoice("team")?.Choice, Is.EqualTo("payments"));
        Assert.That(result.GetScore("urgency")?.Score, Is.EqualTo(1.99));
        Assert.That(result.GetScore("urgency")?.Legend["2"], Is.EqualTo("Blocking revenue"));
        Assert.That(JsonConvert.DeserializeObject<DecisionResult>("{\"answers\":{},\"usage\":{\"input_tokens\":1,\"output_tokens\":0}}")!.Usage!.Cost, Is.Null);

        JObject structured = JObject.Parse(SuccessResponse);
        JObject legend = new JObject { ["0"] = "Calm", ["1"] = new JObject { ["label"] = "Frustrated" }, ["2"] = new JArray("Very", "angry") };
        structured["answers"]!["urgency"]!["legend"] = legend;
        DecisionResult typed = JsonConvert.DeserializeObject<DecisionResult>(structured.ToString())!;
        Assert.That(JToken.DeepEquals(JToken.FromObject(typed.GetScore("urgency")!.Legend), legend), Is.True);
        Assert.That(JToken.DeepEquals(JObject.Parse(JsonConvert.SerializeObject(typed)).SelectToken("answers.urgency.legend"), legend), Is.True);
    }

    [Test]
    public async Task CreateDecision_RoutesAndParsesTheActualHttpResponse()
    {
        var captured = await CallLocalServer(HttpStatusCode.OK, SuccessResponse);
        Assert.That(captured.Result.Ok, Is.True);
        Assert.That(captured.Path, Is.EqualTo("/alpha/decisions"));
        Assert.That(captured.Authorization, Is.EqualTo("Bearer or-test"));
        Assert.That(captured.Body["model"]?.ToString(), Is.EqualTo("openai/gpt-6-luna-decisions"));
        Assert.That(captured.Result.Data?.UpstreamProvider, Is.EqualTo("OpenAI"));
        Assert.That(captured.Result.Data?.Provider?.Provider, Is.EqualTo(LLmProviders.OpenRouter));
        Assert.That(captured.Result.Data?.GetNoul("is_bug")?.Noul, Is.EqualTo(0.96));
        Assert.That(captured.Result.Data?.Usage?.Cost, Is.EqualTo(0.000019992m));

        JObject structured = JObject.Parse(SuccessResponse);
        JObject legend = new JObject { ["0"] = new JObject { ["label"] = "Can wait" }, ["1"] = new JArray("This", "week"), ["2"] = "Blocking revenue" };
        structured["answers"]!["urgency"]!["legend"] = legend;
        var structuredCall = await CallLocalServer(HttpStatusCode.OK, structured.ToString());
        Assert.That(structuredCall.Result.Ok, Is.True);
        Assert.That(JToken.DeepEquals(JToken.FromObject(structuredCall.Result.Data!.GetScore("urgency")!.Legend), legend), Is.True);

        const string minimal = """
            { "model": "openai/gpt-6-luna-decisions", "answers": {
              "is_urgent": { "type": "noul", "noul": 0.9 },
              "department": { "type": "choice", "choice": "billing" },
              "frustration": { "type": "score", "score": 1 }
            }, "usage": { "input_tokens": 1, "output_tokens": 0 } }
            """;
        var minimalCall = await CallLocalServer(HttpStatusCode.OK, minimal);
        Assert.That(minimalCall.Result.Ok, Is.True);
        OpenRouterDecisionLiveTests.AssertAnswers(minimalCall.Result.Data!);
        Assert.That(minimalCall.Result.Data!.GetChoice("department")!.Probabilities, Is.Empty);
        Assert.That(minimalCall.Result.Data.GetScore("frustration")!.Legend, Is.Empty);
        JObject complete = JObject.Parse(minimal);
        complete["answers"]!["department"]!["probabilities"] = new JObject { ["billing"] = 1.0, ["technical"] = 0.0, ["sales"] = 0.0 };
        complete["answers"]!["frustration"]!["probabilities"] = new JObject { ["0"] = 0.0, ["1"] = 1.0, ["2"] = 0.0 };
        complete["answers"]!["frustration"]!["legend"] = new JObject { ["0"] = "Calm", ["1"] = "Frustrated", ["2"] = "Very angry" };
        OpenRouterDecisionLiveTests.AssertAnswers(JsonConvert.DeserializeObject<DecisionResult>(complete.ToString())!);
        complete["answers"]!["department"]!["probabilities"]!["billing"] = 2.0;
        Assert.Throws<AssertionException>(() => OpenRouterDecisionLiveTests.AssertAnswers(JsonConvert.DeserializeObject<DecisionResult>(complete.ToString())!));
    }

    [Test]
    public async Task ZdrRejection_IsReturnedWithoutRelaxingPolicyOrRetrying()
    {
        var captured = await CallLocalServer(HttpStatusCode.BadRequest, "{\"error\":{\"code\":400,\"message\":\"No ZDR-compatible endpoint available\"}}");
        Assert.That(captured.Result.Ok, Is.False);
        Assert.That(captured.Result.Code, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(captured.Result.Exception, Is.Not.Null);
        Assert.That(captured.Result.Response, Does.Contain("No ZDR-compatible endpoint"));
        Assert.That(captured.Body.SelectToken("provider.zdr")?.Value<bool>(), Is.True);
        Assert.That(captured.Body.SelectToken("provider.allow_fallbacks")?.Value<bool>(), Is.False);
        Assert.That(captured.Requests, Is.EqualTo(1));

        DecisionRequest invalid = new DecisionRequest(DecisionModel.OpenRouter.All.Gpt6LunaDecisions, "Hello")
            .AddNoul("greeting", "Is this a greeting?", @true: "Yes");
        var rejected = await CallLocalServer(HttpStatusCode.OK, SuccessResponse, invalid);
        Assert.That(rejected.Result.Ok, Is.False);
        Assert.That(rejected.Result.Code, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(rejected.Result.Exception, Is.TypeOf<ArgumentException>());
        Assert.That(rejected.Result.Exception!.Message, Does.Contain("greeting"));
        Assert.That(rejected.Requests, Is.Zero);
    }

    private static JObject Parse(DecisionRequest request, LLmProviders provider)
        => JObject.Parse(request.Serialize(new TornadoApi(provider, "test-key").GetProvider(provider)).Body.ToString()!);

    private const string SuccessResponse = """
        {
          "id": "gen-decision-1",
          "model": "openai/gpt-6-luna-decisions-20261006",
          "provider": "OpenAI",
          "answers": {
            "is_bug": { "type": "noul", "noul": 0.96 },
            "team": { "type": "choice", "choice": "payments", "confidence": 0.75, "probabilities": { "payments": 0.84, "other": 0.16 } },
            "urgency": { "type": "score", "score": 1.99, "confidence": 0.99, "legend": { "0": "Can wait", "1": "This week", "2": "Blocking revenue" }, "probabilities": { "0": 0, "1": 0.01, "2": 0.99 } }
          },
          "usage": { "input_tokens": 476, "output_tokens": 70, "cost": 0.000019992 }
        }
        """;

    private static async Task<(LlmTornado.Common.HttpCallResult<DecisionResult> Result, JObject Body, string Path, string? Authorization, int Requests)>
        CallLocalServer(HttpStatusCode status, string response, DecisionRequest? requestOverride = null)
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
        JObject? body = null;
        string path = string.Empty;
        string? authorization = null;
        int requests = 0;
        Task serving = Serve();
        try
        {
            TornadoApi api = new TornadoApi(new Uri(prefix), "or-test", LLmProviders.OpenRouter);
            DecisionRequest request = new DecisionRequest(DecisionModel.OpenRouter.All.Gpt6LunaDecisions, new { ticket = "Checkout failed" })
                .AddNoul("is_bug", "Is this a bug?");
            request.VendorExtensions = new DecisionRequestVendorExtensions(new DecisionRequestVendorOpenRouterExtensions
            {
                Provider = new DecisionOpenRouterProviderPreferences { Zdr = true, AllowFallbacks = false }
            });
            var result = await api.Decision.CreateDecisionSafe(requestOverride ?? request, timeout.Token);
            listener.Stop();
            try { await serving; }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException) { }
            return (result, body ?? new JObject(), path, authorization, requests);
        }
        finally
        {
            listener.Close();
            try { await serving; }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException) { }
        }

        async Task Serve()
        {
            while (!timeout.IsCancellationRequested)
            {
                HttpListenerContext context = await listener.GetContextAsync().WaitAsync(timeout.Token);
                requests++;
                path = context.Request.Url!.AbsolutePath;
                authorization = context.Request.Headers["Authorization"];
                using StreamReader reader = new StreamReader(context.Request.InputStream);
                body = JObject.Parse(await reader.ReadToEndAsync(timeout.Token));
                byte[] bytes = Encoding.UTF8.GetBytes(response);
                context.Response.StatusCode = (int)status;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, timeout.Token);
                context.Response.Close();
            }
        }
    }
}
