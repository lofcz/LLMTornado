using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LlmTornado.ChatFunctions;
using LlmTornado.Codex;
using LlmTornado.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class CodexOAuthTests
{
    [Test]
    public async Task BrowserLogin_UsesPkceCallbackAndPersistsCredentials()
    {
        string idToken = Jwt(new JObject
        {
            ["email"] = "user@example.com",
            ["https://api.openai.com/auth"] = new JObject
            {
                ["chatgpt_account_id"] = "account-1",
                ["chatgpt_plan_type"] = "pro"
            }
        });
        string accessToken = Jwt(new JObject
        {
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        });
        string? tokenRequestBody = null;
        RecordingHandler handler = new RecordingHandler(async request =>
        {
            tokenRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync();
            return JsonResponse(new JObject
            {
                ["id_token"] = idToken,
                ["access_token"] = accessToken,
                ["refresh_token"] = "refresh-1"
            });
        });
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore();

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CallbackPort = 0,
                FallbackCallbackPort = 0,
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthBrowserLogin login = await session.StartBrowserLoginAsync();
        Dictionary<string, string> authorizeQuery = CodexOAuthProtocol.ParseQuery(login.AuthorizationUrl.Query);
        Uri callback = new Uri(
            $"http://localhost:{login.CallbackPort}/auth/callback" +
            $"?code=test-code&state={Uri.EscapeDataString(authorizeQuery["state"])}");

        using HttpClient callbackClient = new HttpClient();
        using HttpResponseMessage callbackResponse = await callbackClient.GetAsync(callback);
        CodexOAuthLoginResult result = await login.WaitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(callbackResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result.Success, Is.True);
            Assert.That(result.Account?.Email, Is.EqualTo("user@example.com"));
            Assert.That(result.Account?.PlanType, Is.EqualTo("pro"));
            Assert.That(authorizeQuery["client_id"], Is.EqualTo("app_EMoamEEZ73f0CkXaXp7hrann"));
            Assert.That(authorizeQuery["code_challenge_method"], Is.EqualTo("S256"));
        });

        Dictionary<string, string> tokenForm = CodexOAuthProtocol.ParseQuery(tokenRequestBody ?? string.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(tokenForm["grant_type"], Is.EqualTo("authorization_code"));
            Assert.That(tokenForm["code"], Is.EqualTo("test-code"));
            Assert.That(
                CodexOAuthProtocol.CreateCodeChallenge(tokenForm["code_verifier"]),
                Is.EqualTo(authorizeQuery["code_challenge"]));
        });

        CodexOAuthCredentials? saved = await store.LoadAsync();
        Assert.Multiple(() =>
        {
            Assert.That(saved?.AccessToken, Is.EqualTo(accessToken));
            Assert.That(saved?.RefreshToken, Is.EqualTo("refresh-1"));
            Assert.That(saved?.AccountId, Is.EqualTo("account-1"));
        });
    }

    [Test]
    public async Task ListModels_RefreshesRotatingTokenAndPreservesCatalogOrder()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials("expired-access", "refresh-old", DateTimeOffset.UtcNow.AddMinutes(-1)));
        int refreshRequests = 0;
        string? modelAuthorization = null;
        string? modelAccount = null;
        string? catalogClientVersion = null;
        RecordingHandler handler = new RecordingHandler(async request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal) == true)
            {
                refreshRequests++;
                JObject refresh = JObject.Parse(await request.Content!.ReadAsStringAsync());
                Assert.That(refresh.Value<string>("refresh_token"), Is.EqualTo("refresh-old"));
                return JsonResponse(new JObject
                {
                    ["access_token"] = Jwt(new JObject
                    {
                        ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
                    }),
                    ["refresh_token"] = "refresh-new"
                });
            }

            modelAuthorization = request.Headers.Authorization?.ToString();
            modelAccount = request.Headers.TryGetValues("ChatGPT-Account-ID", out IEnumerable<string>? values)
                ? values.Single()
                : null;
            Dictionary<string, string> catalogQuery =
                CodexOAuthProtocol.ParseQuery(request.RequestUri?.Query ?? string.Empty);
            catalogQuery.TryGetValue("client_version", out catalogClientVersion);
            return JsonResponse(new JObject
            {
                ["models"] = new JArray
                {
                    BackendModel("gpt-6-astra", true, "low", "medium", "high"),
                    BackendModel("gpt-5.3-codex", false, new[] { "medium", "high" }, "list"),
                    BackendModel("hidden-model", false, "medium", visibility: "hide")
                }
            });
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler),
                ClientVersion = "1.2.3"
            });
        IReadOnlyList<CodexModel> models = await session.ListModelsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(refreshRequests, Is.EqualTo(1));
            Assert.That(modelAuthorization, Does.StartWith("Bearer "));
            Assert.That(modelAccount, Is.EqualTo("account-1"));
            Assert.That(catalogClientVersion, Is.EqualTo(CodexOAuthOptions.DefaultCodexProtocolVersion));
            Assert.That(catalogClientVersion, Is.Not.EqualTo("1.2.3"));
            Assert.That(models.Select(model => model.Model), Is.EqualTo(new[] { "gpt-6-astra", "gpt-5.3-codex" }));
            Assert.That(
                models[0].SupportedReasoningEfforts.Select(effort => effort.ReasoningEffort),
                Is.EqualTo(new[] { "low", "medium", "high" }));
            Assert.That(
                models[0].ServiceTiers.Select(serviceTier => serviceTier.Id),
                Is.EqualTo(new[] { "priority", "future-tier", "ultrafast" }));
            Assert.That(models[0].DefaultServiceTier, Is.EqualTo("priority"));
        });

        CodexOAuthCredentials? saved = await store.LoadAsync();
        Assert.That(saved?.RefreshToken, Is.EqualTo("refresh-new"));
    }

    [Test]
    public async Task ListModels_UsesConfiguredCodexProtocolVersion()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        string? catalogClientVersion = null;
        RecordingHandler handler = new RecordingHandler(request =>
        {
            Dictionary<string, string> query =
                CodexOAuthProtocol.ParseQuery(request.RequestUri?.Query ?? string.Empty);
            query.TryGetValue("client_version", out catalogClientVersion);
            return Task.FromResult(JsonResponse(new JObject { ["models"] = new JArray() }));
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler),
                ClientVersion = "1.2.3",
                CodexProtocolVersion = "0.147.1-test"
            });
        await session.ListModelsAsync();

        Assert.That(catalogClientVersion, Is.EqualTo("0.147.1-test"));
    }

    [Test]
    public async Task TextThread_SendsOnlyTextAndReplaysClientManagedHistory()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        List<JObject> payloads = [];
        int responseNumber = 0;
        RecordingHandler handler = new RecordingHandler(async request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true)
            {
                return JsonResponse(new JObject
                {
                    ["models"] = new JArray
                    {
                        BackendModel("gpt-6-astra", true, new[] { "medium", "high" }, "list")
                    }
                });
            }

            payloads.Add(JObject.Parse(await request.Content!.ReadAsStringAsync()));
            responseNumber++;
            string responseId = $"response-{responseNumber}";
            string sse =
                $"event: response.output_text.delta\n" +
                $"data: {{\"type\":\"response.output_text.delta\",\"response_id\":\"{responseId}\",\"item_id\":\"item-1\",\"delta\":\"Codex \"}}\n\n" +
                $"event: response.output_text.delta\n" +
                $"data: {{\"type\":\"response.output_text.delta\",\"response_id\":\"{responseId}\",\"item_id\":\"item-1\",\"delta\":\"reply\"}}\n\n" +
                $"event: response.output_item.done\n" +
                $"data: {{\"type\":\"response.output_item.done\",\"item\":{{\"id\":\"reasoning-{responseNumber}\",\"type\":\"reasoning\",\"encrypted_content\":\"encrypted-{responseNumber}\",\"summary\":[]}}}}\n\n" +
                $"event: response.output_item.done\n" +
                $"data: {{\"type\":\"response.output_item.done\",\"item\":{{\"id\":\"message-{responseNumber}\",\"type\":\"message\",\"role\":\"assistant\",\"content\":[{{\"type\":\"output_text\",\"text\":\"Codex reply\"}}]}}}}\n\n" +
                $"event: response.completed\n" +
                $"data: {{\"type\":\"response.completed\",\"response\":{{\"id\":\"{responseId}\",\"status\":\"completed\"}}}}\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
            };
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions
        {
            Model = "gpt-6-astra",
            Instructions = "Reply briefly."
        });
        List<string> deltas = [];
        CodexOAuthTurnResult first = await thread.RunAsync("First", new CodexOAuthTurnOptions
        {
            ReasoningEffort = "high",
            ServiceTier = "ultrafast",
            OnTextDelta = delta =>
            {
                deltas.Add(delta.Delta);
                return Task.CompletedTask;
            }
        });
        CodexOAuthTurnResult second = await thread.RunAsync("Second");

        Assert.Multiple(() =>
        {
            Assert.That(first.FinalResponse, Is.EqualTo("Codex reply"));
            Assert.That(second.ResponseId, Is.EqualTo("response-2"));
            Assert.That(deltas, Is.EqualTo(new[] { "Codex ", "reply" }));
            Assert.That(payloads[0].Value<string>("instructions"), Is.EqualTo("gpt-6-astra base instructions"));
            Assert.That(payloads[0]["input"]?[0]?["role"]?.Value<string>(), Is.EqualTo("developer"));
            Assert.That(payloads[0]["input"]?[1]?["content"]?[0]?["type"]?.Value<string>(), Is.EqualTo("input_text"));
            Assert.That(payloads[0].ToString(Formatting.None), Does.Not.Contain("image"));
            Assert.That(payloads[0]["include"]?[0]?.Value<string>(), Is.EqualTo("reasoning.encrypted_content"));
            Assert.That(payloads[0].Value<string>("service_tier"), Is.EqualTo("ultrafast"));
            Assert.That(payloads[0]["tools"], Is.Empty);
            Assert.That(payloads[0].Value<string>("tool_choice"), Is.EqualTo("auto"));
            Assert.That(payloads[0].Value<bool>("parallel_tool_calls"), Is.False);
            Assert.That(payloads[1].Property("previous_response_id"), Is.Null);
            Assert.That(payloads[1]["input"]?.Count(), Is.EqualTo(5));
            Assert.That(payloads[1]["input"]?[2]?["encrypted_content"]?.Value<string>(), Is.EqualTo("encrypted-1"));
            Assert.That(payloads[1]["input"]?[3]?["role"]?.Value<string>(), Is.EqualTo("assistant"));
            Assert.That(payloads[1]["input"]?[4]?["content"]?[0]?["text"]?.Value<string>(), Is.EqualTo("Second"));
            Assert.That(
                typeof(CodexOAuthSession).GetMethods().Any(method => method.Name.Contains("Image", StringComparison.Ordinal)),
                Is.False);
        });
    }

    [Test]
    public async Task TextThread_IncludesConfiguredInitialHistory()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        JObject? payload = null;
        string? payloadJson = null;
        RecordingHandler handler = new RecordingHandler(async request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true)
            {
                return JsonResponse(new JObject
                {
                    ["models"] = new JArray { BackendModel("gpt-5.4", true, "medium") }
                });
            }

            payloadJson = await request.Content!.ReadAsStringAsync();
            payload = JObject.Parse(payloadJson);
            return EventStreamResponse(CompletedSse("response-1"));
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions
        {
            Model = "gpt-5.4",
            InitialHistory = new[]
            {
                CodexOAuthHistoryItem.UserMessage("What time is it?"),
                CodexOAuthHistoryItem.FunctionCall("call-1", "datetime", "{}"),
                CodexOAuthHistoryItem.FunctionOutput("call-1", "2026-09-19T16:58:17Z"),
                CodexOAuthHistoryItem.AssistantMessage("It is 18:58 local time.")
            }
        });

        await thread.RunAsync("What was the date?");

        Assert.Multiple(() =>
        {
            Assert.That(payload?["input"]?.Count(), Is.EqualTo(5));
            Assert.That(payload?["input"]?[0]?["role"]?.Value<string>(), Is.EqualTo("user"));
            Assert.That(payload?["input"]?[0]?["content"]?[0]?["text"]?.Value<string>(), Is.EqualTo("What time is it?"));
            Assert.That(payload?["input"]?[1]?["type"]?.Value<string>(), Is.EqualTo("function_call"));
            Assert.That(payload?["input"]?[1]?["call_id"]?.Value<string>(), Is.EqualTo("call-1"));
            Assert.That(payload?["input"]?[1]?["name"]?.Value<string>(), Is.EqualTo("datetime"));
            Assert.That(payload?["input"]?[1]?["arguments"]?.Value<string>(), Is.EqualTo("{}"));
            Assert.That(payload?["input"]?[2]?["type"]?.Value<string>(), Is.EqualTo("function_call_output"));
            Assert.That(payload?["input"]?[2]?["call_id"]?.Value<string>(), Is.EqualTo("call-1"));
            Assert.That(payloadJson, Does.Contain("\"output\":\"2026-09-19T16:58:17Z\""));
            Assert.That(payload?["input"]?[3]?["role"]?.Value<string>(), Is.EqualTo("assistant"));
            Assert.That(payload?["input"]?[3]?["content"]?[0]?["type"]?.Value<string>(), Is.EqualTo("output_text"));
            Assert.That(payload?["input"]?[4]?["content"]?[0]?["text"]?.Value<string>(), Is.EqualTo("What was the date?"));
        });
    }

    [Test]
    public async Task TextThread_SerializesConfiguredFunctionTools()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        JObject? payload = null;
        RecordingHandler handler = new RecordingHandler(async request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true)
            {
                return JsonResponse(new JObject
                {
                    ["models"] = new JArray { BackendModel("gpt-5.4", true, "medium") }
                });
            }

            payload = JObject.Parse(await request.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "event: response.completed\n" +
                    "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"response-1\",\"status\":\"completed\",\"output\":[]}}\n\n",
                    Encoding.UTF8,
                    "text/event-stream")
            };
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions
        {
            Model = "gpt-5.4"
        });
        Tool tool = new Tool(
            (string location) => location,
            "get_weather",
            "Gets the weather.",
            strict: true);

        await thread.RunAsync("Weather?", new CodexOAuthTurnOptions
        {
            Tools = new[] { tool },
            ToolChoice = OutboundToolChoice.Tool("get_weather"),
            ParallelToolCalls = true
        });

        Assert.Multiple(() =>
        {
            Assert.That(payload?["tools"]?[0]?["type"]?.Value<string>(), Is.EqualTo("function"));
            Assert.That(payload?["tools"]?[0]?["name"]?.Value<string>(), Is.EqualTo("get_weather"));
            Assert.That(payload?["tools"]?[0]?["description"]?.Value<string>(), Is.EqualTo("Gets the weather."));
            Assert.That(payload?["tools"]?[0]?["parameters"]?["properties"]?["location"]?["type"]?.Value<string>(), Is.EqualTo("string"));
            Assert.That(payload?["tools"]?[0]?["strict"]?.Value<bool>(), Is.True);
            Assert.That(payload?["tool_choice"]?["type"]?.Value<string>(), Is.EqualTo("function"));
            Assert.That(payload?["tool_choice"]?["name"]?.Value<string>(), Is.EqualTo("get_weather"));
            Assert.That(payload?.Value<bool>("parallel_tool_calls"), Is.True);
        });
    }

    [Test]
    public async Task TextThread_CombinesFragmentedFunctionCallArguments()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        RecordingHandler handler = new RecordingHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(JsonResponse(new JObject
                {
                    ["models"] = new JArray { BackendModel("gpt-5.4", true, "medium") }
                }));
            }

            const string sse =
                "event: response.output_item.added\n" +
                "data: {\"type\":\"response.output_item.added\",\"item\":{\"id\":\"item-1\",\"type\":\"function_call\",\"call_id\":\"call-1\",\"name\":\"get_weather\",\"arguments\":\"\"}}\n\n" +
                "event: response.function_call_arguments.delta\n" +
                "data: {\"type\":\"response.function_call_arguments.delta\",\"item_id\":\"item-1\",\"delta\":\"{\\\"location\\\":\"}\n\n" +
                "event: response.function_call_arguments.delta\n" +
                "data: {\"type\":\"response.function_call_arguments.delta\",\"item_id\":\"item-1\",\"delta\":\"\\\"Prague\\\"}\"}\n\n" +
                "event: response.function_call_arguments.done\n" +
                "data: {\"type\":\"response.function_call_arguments.done\",\"item_id\":\"item-1\",\"arguments\":\"{\\\"location\\\":\\\"Prague\\\"}\"}\n\n" +
                "event: response.output_item.done\n" +
                "data: {\"type\":\"response.output_item.done\",\"item\":{\"id\":\"item-1\",\"type\":\"function_call\",\"call_id\":\"call-1\",\"name\":\"get_weather\"}}\n\n" +
                "event: response.completed\n" +
                "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"response-1\",\"status\":\"completed\"}}\n\n";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
            });
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions
        {
            Model = "gpt-5.4"
        });
        Tool tool = new Tool(new ToolFunction("get_weather", "Gets the weather."));

        CodexOAuthTurnResult result = await thread.RunAsync("Weather?", new CodexOAuthTurnOptions
        {
            Tools = new[] { tool }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.ToolCalls, Has.Count.EqualTo(1));
            Assert.That(result.ToolCalls[0].Id, Is.EqualTo("call-1"));
            Assert.That(result.ToolCalls[0].FunctionCall?.Name, Is.EqualTo("get_weather"));
            Assert.That(result.ToolCalls[0].FunctionCall?.Arguments, Is.EqualTo("{\"location\":\"Prague\"}"));
            Assert.That(result.ToolCalls[0].FunctionCall?.Tool, Is.SameAs(tool));
        });
    }

    [Test]
    public async Task TextThread_SubmitsToolResultAndContinuesToFinalText()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        List<JObject> payloads = [];
        RecordingHandler handler = new RecordingHandler(async request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true)
            {
                return JsonResponse(new JObject
                {
                    ["models"] = new JArray { BackendModel("gpt-5.4", true, "medium") }
                });
            }

            payloads.Add(JObject.Parse(await request.Content!.ReadAsStringAsync()));
            string sse = payloads.Count == 1
                ? "event: response.output_item.done\n" +
                  "data: {\"type\":\"response.output_item.done\",\"item\":{\"id\":\"item-1\",\"type\":\"function_call\",\"call_id\":\"call-1\",\"name\":\"get_weather\",\"arguments\":\"{\\\"location\\\":\\\"Prague\\\"}\"}}\n\n" +
                  "event: response.completed\n" +
                  "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"response-1\",\"status\":\"completed\"}}\n\n"
                : "event: response.output_text.delta\n" +
                  "data: {\"type\":\"response.output_text.delta\",\"response_id\":\"response-2\",\"item_id\":\"message-1\",\"delta\":\"Sunny\"}\n\n" +
                  "event: response.completed\n" +
                  "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"response-2\",\"status\":\"completed\"}}\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
            };
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions
        {
            Model = "gpt-5.4"
        });

        CodexOAuthTurnResult result = await thread.RunAsync(
            "Weather?",
            calls =>
            {
                calls[0].Result = new FunctionResult(calls[0], new { temperature = 21 });
                return ValueTask.CompletedTask;
            },
            new CodexOAuthTurnOptions
            {
                Tools = new[] { new Tool(new ToolFunction("get_weather", "Gets the weather.")) }
            });

        Assert.Multiple(() =>
        {
            Assert.That(payloads, Has.Count.EqualTo(2));
            Assert.That(payloads[1]["input"]?[1]?["call_id"]?.Value<string>(), Is.EqualTo("call-1"));
            Assert.That(payloads[1]["input"]?[2]?["type"]?.Value<string>(), Is.EqualTo("function_call_output"));
            Assert.That(payloads[1]["input"]?[2]?["call_id"]?.Value<string>(), Is.EqualTo("call-1"));
            Assert.That(payloads[1]["input"]?[2]?["output"]?.Value<string>(), Does.Contain("21"));
            Assert.That(result.FinalResponse, Is.EqualTo("Sunny"));
            Assert.That(result.ToolCalls, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task TextThread_PreservesMultipleToolCallAndResultOrder()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        List<JObject> payloads = [];
        RecordingHandler handler = new RecordingHandler(async request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true)
            {
                return JsonResponse(new JObject
                {
                    ["models"] = new JArray { BackendModel("gpt-5.4", true, "medium") }
                });
            }

            payloads.Add(JObject.Parse(await request.Content!.ReadAsStringAsync()));
            string sse = payloads.Count == 1
                ? "event: response.output_item.done\n" +
                  "data: {\"type\":\"response.output_item.done\",\"item\":{\"id\":\"item-1\",\"type\":\"function_call\",\"call_id\":\"call-1\",\"name\":\"lookup\",\"arguments\":\"{\\\"value\\\":1}\"}}\n\n" +
                  "event: response.output_item.done\n" +
                  "data: {\"type\":\"response.output_item.done\",\"item\":{\"id\":\"item-2\",\"type\":\"function_call\",\"call_id\":\"call-2\",\"name\":\"lookup\",\"arguments\":\"{\\\"value\\\":2}\"}}\n\n" +
                  "event: response.completed\n" +
                  "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"response-1\",\"status\":\"completed\"}}\n\n"
                : "event: response.completed\n" +
                  "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"response-2\",\"status\":\"completed\"}}\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
            };
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions
        {
            Model = "gpt-5.4"
        });
        List<string?> handledIds = [];

        CodexOAuthTurnResult result = await thread.RunAsync(
            "Look up both values.",
            calls =>
            {
                handledIds.AddRange(calls.Select(call => call.ToolCall?.Id));
                calls[0].Resolve(new { result = "first" });
                calls[1].Resolve(new { result = "second" });
                return ValueTask.CompletedTask;
            },
            new CodexOAuthTurnOptions
            {
                Tools = new[] { new Tool(new ToolFunction("lookup", "Looks up a value.")) },
                ParallelToolCalls = true
            });

        Assert.Multiple(() =>
        {
            Assert.That(handledIds, Is.EqualTo(new[] { "call-1", "call-2" }));
            Assert.That(result.ToolCalls.Select(call => call.Id), Is.EqualTo(new[] { "call-1", "call-2" }));
            Assert.That(result.ToolCalls[0].FunctionCall?.Arguments, Is.EqualTo("{\"value\":1}"));
            Assert.That(result.ToolCalls[1].FunctionCall?.Arguments, Is.EqualTo("{\"value\":2}"));
            Assert.That(payloads[1]["input"]?[3]?["call_id"]?.Value<string>(), Is.EqualTo("call-1"));
            Assert.That(payloads[1]["input"]?[4]?["call_id"]?.Value<string>(), Is.EqualTo("call-2"));
            Assert.That(payloads[1]["input"]?[3]?["output"]?.Value<string>(), Does.Contain("first"));
            Assert.That(payloads[1]["input"]?[4]?["output"]?.Value<string>(), Does.Contain("second"));
        });
    }

    [Test]
    public async Task TextThread_DeniedCallDoesNotInvokeAttachedDelegate()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        int invocationCount = 0;
        List<JObject> payloads = [];
        RecordingHandler handler = new RecordingHandler(async request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true)
            {
                return JsonResponse(new JObject
                {
                    ["models"] = new JArray { BackendModel("gpt-5.4", true, "medium") }
                });
            }

            payloads.Add(JObject.Parse(await request.Content!.ReadAsStringAsync()));
            string sse = payloads.Count == 1
                ? ToolCallSse("delete_file", "{\"path\":\"important.txt\"}")
                : CompletedSse("response-2");
            return EventStreamResponse(sse);
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions
        {
            Model = "gpt-5.4"
        });
        Tool tool = new Tool(
            (string path) =>
            {
                invocationCount++;
                return path;
            },
            "delete_file");

        await thread.RunAsync(
            "Delete the file.",
            calls =>
            {
                calls[0].Resolve(new { error = "Permission denied." }, invocationSucceeded: false);
                return ValueTask.CompletedTask;
            },
            new CodexOAuthTurnOptions { Tools = new[] { tool } });

        Assert.Multiple(() =>
        {
            Assert.That(invocationCount, Is.Zero);
            Assert.That(payloads[1]["input"]?[2]?["output"]?.Value<string>(), Does.Contain("Permission denied."));
        });
    }

    [Test]
    public async Task TextThread_UnknownAndMalformedCallsProduceControlledFailures()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        int invocationCount = 0;
        List<JObject> payloads = [];
        RecordingHandler handler = new RecordingHandler(async request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true)
            {
                return JsonResponse(new JObject
                {
                    ["models"] = new JArray { BackendModel("gpt-5.4", true, "medium") }
                });
            }

            payloads.Add(JObject.Parse(await request.Content!.ReadAsStringAsync()));
            string sse = payloads.Count == 1
                ? "event: response.output_item.done\n" +
                  "data: {\"type\":\"response.output_item.done\",\"item\":{\"id\":\"item-1\",\"type\":\"function_call\",\"call_id\":\"call-1\",\"name\":\"unknown\",\"arguments\":\"{}\"}}\n\n" +
                  "event: response.output_item.done\n" +
                  "data: {\"type\":\"response.output_item.done\",\"item\":{\"id\":\"item-2\",\"type\":\"function_call\",\"call_id\":\"call-2\",\"name\":\"known\",\"arguments\":\"not-json\"}}\n\n" +
                  "event: response.completed\n" +
                  "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"response-1\",\"status\":\"completed\"}}\n\n"
                : CompletedSse("response-2");
            return EventStreamResponse(sse);
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions
        {
            Model = "gpt-5.4"
        });
        Tool tool = new Tool(
            (int value) =>
            {
                invocationCount++;
                return value;
            },
            "known");

        await thread.RunAsync(
            "Call both tools.",
            async calls =>
            {
                Assert.That(calls[0].Tool, Is.Null);
                Assert.That((await calls[0].Invoke(calls[0].Arguments ?? "{}")).InvocationException, Is.Not.Null);
                Assert.That((await calls[1].Invoke(calls[1].Arguments ?? "{}")).InvocationException, Is.Not.Null);
            },
            new CodexOAuthTurnOptions { Tools = new[] { tool } });

        Assert.Multiple(() =>
        {
            Assert.That(invocationCount, Is.Zero);
            Assert.That(payloads[1]["input"]?[3]?["output"]?.Value<string>(), Does.Contain("not handled"));
            Assert.That(payloads[1]["input"]?[4]?["output"]?.Value<string>(), Does.Contain("not handled"));
        });
    }

    [Test]
    public async Task TextThread_CancellationInterruptsStreaming()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        int responseRequests = 0;
        RecordingHandler handler = new RecordingHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(JsonResponse(new JObject
                {
                    ["models"] = new JArray { BackendModel("gpt-5.4", true, "medium") }
                }));
            }

            responseRequests++;
            return Task.FromResult(EventStreamResponse(
                "event: response.output_text.delta\n" +
                "data: {\"type\":\"response.output_text.delta\",\"response_id\":\"response-1\",\"item_id\":\"message-1\",\"delta\":\"Partial\"}\n\n" +
                CompletedSse("response-1")));
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions
        {
            Model = "gpt-5.4"
        });
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        bool receivedDelta = false;

        Assert.CatchAsync<OperationCanceledException>(async () => await thread.RunAsync(
            "Stream a response.",
            new CodexOAuthTurnOptions
            {
                OnTextDelta = _ =>
                {
                    receivedDelta = true;
                    cancellation.Cancel();
                    return Task.CompletedTask;
                }
            },
            cancellation.Token));

        Assert.Multiple(() =>
        {
            Assert.That(receivedDelta, Is.True);
            Assert.That(responseRequests, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TextThread_CancellationInterruptsHostHandler()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        int responseRequests = 0;
        RecordingHandler handler = new RecordingHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(JsonResponse(new JObject
                {
                    ["models"] = new JArray { BackendModel("gpt-5.4", true, "medium") }
                }));
            }

            responseRequests++;
            return Task.FromResult(EventStreamResponse(ToolCallSse("lookup", "{}")));
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions
        {
            Model = "gpt-5.4"
        });
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        CancellationToken observedToken = default;

        Assert.CatchAsync<OperationCanceledException>(async () => await thread.RunAsync(
            "Look up a value.",
            async (_, cancellationToken) =>
            {
                observedToken = cancellationToken;
                cancellation.Cancel();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            },
            new CodexOAuthTurnOptions
            {
                Tools = new[] { new Tool(new ToolFunction("lookup", "Looks up a value.")) }
            },
            cancellation.Token));

        Assert.Multiple(() =>
        {
            Assert.That(observedToken, Is.EqualTo(cancellation.Token));
            Assert.That(responseRequests, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TextThread_CancellationPreventsFollowUpRequest()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-1",
                DateTimeOffset.UtcNow.AddHours(1)));
        int responseRequests = 0;
        RecordingHandler handler = new RecordingHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/models", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(JsonResponse(new JObject
                {
                    ["models"] = new JArray { BackendModel("gpt-5.4", true, "medium") }
                }));
            }

            responseRequests++;
            return Task.FromResult(EventStreamResponse(ToolCallSse("lookup", "{}")));
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        CodexOAuthThread thread = await session.StartThreadAsync(new CodexOAuthThreadOptions
        {
            Model = "gpt-5.4"
        });
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        Assert.CatchAsync<OperationCanceledException>(async () => await thread.RunAsync(
            "Look up a value.",
            (calls, _) =>
            {
                calls[0].Resolve(new { result = "done" });
                cancellation.Cancel();
                return ValueTask.CompletedTask;
            },
            new CodexOAuthTurnOptions
            {
                Tools = new[] { new Tool(new ToolFunction("lookup", "Looks up a value.")) }
            },
            cancellation.Token));

        Assert.That(responseRequests, Is.EqualTo(1));
    }

    [Test]
    public async Task UnauthorizedModelRequest_RefreshesAndRetriesOnce()
    {
        CodexOAuthMemoryCredentialStore store = new CodexOAuthMemoryCredentialStore(
            Credentials(
                Jwt(new JObject { ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }),
                "refresh-old",
                DateTimeOffset.UtcNow.AddHours(1)));
        int modelRequests = 0;
        int refreshRequests = 0;
        RecordingHandler handler = new RecordingHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal) == true)
            {
                refreshRequests++;
                return Task.FromResult(JsonResponse(new JObject
                {
                    ["access_token"] = Jwt(new JObject
                    {
                        ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
                    }),
                    ["refresh_token"] = "refresh-new"
                }));
            }

            modelRequests++;
            return Task.FromResult(modelRequests == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : JsonResponse(new JObject
                {
                    ["models"] = new JArray { BackendModel("gpt-5.4", true, "medium") }
                }));
        });

        await using CodexOAuthSession session = await new TornadoApi().Codex.ConnectOAuthAsync(
            new CodexOAuthOptions
            {
                CredentialStore = store,
                HttpClient = new HttpClient(handler)
            });
        IReadOnlyList<CodexModel> models = await session.ListModelsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(models, Has.Count.EqualTo(1));
            Assert.That(modelRequests, Is.EqualTo(2));
            Assert.That(refreshRequests, Is.EqualTo(1));
        });
    }

    private static CodexOAuthCredentials Credentials(
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt)
        => new CodexOAuthCredentials
        {
            IdToken = Jwt(new JObject
            {
                ["email"] = "user@example.com",
                ["https://api.openai.com/auth"] = new JObject
                {
                    ["chatgpt_account_id"] = "account-1",
                    ["chatgpt_plan_type"] = "pro"
                }
            }),
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccountId = "account-1",
            Email = "user@example.com",
            PlanType = "pro",
            ExpiresAtUtc = expiresAt,
            LastRefreshUtc = DateTimeOffset.UtcNow
        };

    private static JObject BackendModel(
        string slug,
        bool isDefault,
        params string[] efforts)
        => BackendModel(slug, isDefault, efforts, "list");

    private static JObject BackendModel(
        string slug,
        bool isDefault,
        string effort,
        string visibility)
        => BackendModel(slug, isDefault, new[] { effort }, visibility);

    private static JObject BackendModel(
        string slug,
        bool isDefault,
        IReadOnlyList<string> efforts,
        string visibility)
    {
        JObject result = new JObject
        {
            ["slug"] = slug,
            ["display_name"] = slug,
            ["description"] = $"{slug} description",
            ["base_instructions"] = $"{slug} base instructions",
            ["visibility"] = visibility,
            ["show_in_picker"] = true,
            ["supported_in_api"] = true,
            ["is_default"] = isDefault,
            ["default_reasoning_level"] = efforts.First(),
            ["default_service_tier"] = "priority",
            ["supported_reasoning_levels"] = new JArray(efforts.Select(effort => new JObject
            {
                ["effort"] = effort,
                ["description"] = effort
            })),
            ["service_tiers"] = new JArray(new JObject
            {
                ["id"] = "priority",
                ["name"] = "Fast",
                ["description"] = "1.5x speed"
            }, new JObject
            {
                ["id"] = "future-tier",
                ["name"] = "Future",
                ["description"] = "Future catalog value"
            }),
            ["input_modalities"] = new JArray("text")
        };
        if (slug == "gpt-6-astra")
        {
            ((JArray)result["service_tiers"]!).Add(new JObject { ["id"] = "ultrafast", ["name"] = "Ultrafast" });
        }
        return result;
    }

    private static string Jwt(JObject claims)
    {
        string header = Base64Url(Encoding.UTF8.GetBytes("{}"));
        string payload = Base64Url(Encoding.UTF8.GetBytes(claims.ToString(Formatting.None)));
        return $"{header}.{payload}.signature";
    }

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static HttpResponseMessage JsonResponse(JObject body)
        => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage EventStreamResponse(string sse)
        => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
        };

    private static string ToolCallSse(string name, string arguments)
        => "event: response.output_item.done\n" +
           $"data: {{\"type\":\"response.output_item.done\",\"item\":{{\"id\":\"item-1\",\"type\":\"function_call\",\"call_id\":\"call-1\",\"name\":\"{name}\",\"arguments\":{JsonConvert.ToString(arguments)}}}}}\n\n" +
           "event: response.completed\n" +
           "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"response-1\",\"status\":\"completed\"}}\n\n";

    private static string CompletedSse(string responseId)
        => "event: response.completed\n" +
           $"data: {{\"type\":\"response.completed\",\"response\":{{\"id\":\"{responseId}\",\"status\":\"completed\"}}}}\n\n";

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> handler;

        internal RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return handler(request);
        }
    }
}
