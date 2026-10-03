using System.Collections.Concurrent;
using LlmTornado.Codex;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class CodexTests
{
    [Test]
    public async Task AppServer_AuthenticatesListsModelsAndRunsTextTurn()
    {
        FakeCodexTransport transport = new FakeCodexTransport();
        await using CodexSession session = await CodexSession.ConnectAsync(new CodexAppServerOptions(), transport);

        Assert.That(session.Initialization.CodexHome, Is.EqualTo("C:/codex"));

        CodexAccountResult account = await session.GetAccountAsync(refreshToken: true);
        Assert.That(account.Account?.PlanType, Is.EqualTo("pro"));
        JObject accountRequest = transport.Messages.Single(message => message.Value<string>("method") == "account/read");
        Assert.That(accountRequest["params"]?["refreshToken"]?.Value<bool>(), Is.True);

        CodexBrowserLogin login = await session.StartBrowserLoginAsync();
        Assert.That(login.AuthorizationUrl.Host, Is.EqualTo("chatgpt.com"));
        CodexLoginResult loginResult = await login.WaitAsync();
        Assert.That(loginResult.Success, Is.True);

        IReadOnlyList<CodexModel> models = await session.ListModelsAsync();
        Assert.That(models.Select(x => x.Model), Is.EqualTo(new[] { "gpt-6-astra", "gpt-5.3-codex" }));
        Assert.That(
            models[0].SupportedReasoningEfforts.Select(x => x.ReasoningEffort),
            Is.EqualTo(new[] { "low", "medium", "high" }));
        Assert.That(
            models[0].ServiceTiers.Select(x => x.Id),
            Is.EqualTo(new[] { "priority", "future-tier", "ultrafast" }));
        Assert.That(models[0].DefaultServiceTier, Is.EqualTo("priority"));

        CodexThread thread = await session.StartThreadAsync(new CodexThreadOptions
        {
            Model = models[0].Model,
            WorkingDirectory = "D:/repo"
        });
        List<string> deltas = [];
        CodexTurnResult turn = await thread.RunAsync("Reply briefly.", new CodexTurnOptions
        {
            ReasoningEffort = "high",
            ServiceTier = models[0].ServiceTiers.Single(x => x.Id == "ultrafast").Id,
            OnTextDelta = delta =>
            {
                deltas.Add(delta.Delta);
                return Task.CompletedTask;
            }
        });

        Assert.That(turn.FinalResponse, Is.EqualTo("Codex reply"));
        Assert.That(turn.Status, Is.EqualTo("completed"));
        Assert.That(deltas, Is.EqualTo(new[] { "Codex ", "reply" }));

        JObject turnRequest = transport.Messages.Single(message => message.Value<string>("method") == "turn/start");
        Assert.That(turnRequest["params"]?["input"]?.Count(), Is.EqualTo(1));
        Assert.That(turnRequest["params"]?["input"]?[0]?["type"]?.Value<string>(), Is.EqualTo("text"));
        Assert.That(turnRequest["params"]?["serviceTier"]?.Value<string>(), Is.EqualTo("ultrafast"));
        Assert.That(transport.Messages.Any(message => message.Value<string>("method")?.Contains("image") == true), Is.False);
    }

    [Test]
    public async Task AppServer_UsesRequiredInitializationSequence()
    {
        FakeCodexTransport transport = new FakeCodexTransport();
        await using CodexSession session = await CodexSession.ConnectAsync(new CodexAppServerOptions
        {
            ClientName = "test-client",
            ClientTitle = "Test Client",
            ClientVersion = "1.2.3"
        }, transport);

        Assert.That(transport.Messages[0].Value<string>("method"), Is.EqualTo("initialize"));
        Assert.That(transport.Messages[0]["params"]?["clientInfo"]?["name"]?.Value<string>(), Is.EqualTo("test-client"));
        Assert.That(transport.Messages[1].Value<string>("method"), Is.EqualTo("initialized"));
        Assert.That(transport.Messages[1]["id"], Is.Null);
    }

    [Test]
    [Category("Integration")]
    public async Task InstalledAppServer_ConnectsAndReturnsModels()
    {
        if (Environment.GetEnvironmentVariable("LLMTORNADO_CODEX_LIVE_TEST") != "1")
        {
            Assert.Ignore("Set LLMTORNADO_CODEX_LIVE_TEST=1 to use the installed Codex app-server.");
        }

        TornadoApi api = new TornadoApi();
        await using CodexSession session = await api.Codex.ConnectAsync();
        CodexAccountResult account = await session.GetAccountAsync();
        IReadOnlyList<CodexModel> models = await session.ListModelsAsync();

        Assert.That(session.Initialization.UserAgent, Is.Not.Empty);
        Assert.That(account.Account?.Type, Is.EqualTo("chatgpt"));
        Assert.That(models, Is.Not.Empty);
    }

    private sealed class FakeCodexTransport : ICodexAppServerTransport
    {
        private readonly ConcurrentQueue<string> output = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim outputSignal = new SemaphoreSlim(0);
        private bool stopped;

        internal List<JObject> Messages { get; } = [];

        public IReadOnlyCollection<string> RecentStandardError => Array.Empty<string>();

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (output.TryDequeue(out string? line))
                {
                    return line;
                }

                if (stopped)
                {
                    return null;
                }

                await outputSignal.WaitAsync(cancellationToken);
            }
        }

        public Task WriteLineAsync(string line, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JObject message = JObject.Parse(line);
            Messages.Add(message);
            string? method = message.Value<string>("method");
            JToken? id = message["id"];

            if (id is null)
            {
                return Task.CompletedTask;
            }

            switch (method)
            {
                case "initialize":
                    Respond(id, new JObject
                    {
                        ["userAgent"] = "codex-test",
                        ["codexHome"] = "C:/codex",
                        ["platformFamily"] = "windows",
                        ["platformOs"] = "windows"
                    });
                    break;
                case "account/login/start":
                    Respond(id, new JObject
                    {
                        ["type"] = "chatgpt",
                        ["loginId"] = "login-1",
                        ["authUrl"] = "https://chatgpt.com/auth/codex"
                    });
                    Notify("account/login/completed", new JObject
                    {
                        ["loginId"] = "login-1",
                        ["success"] = true,
                        ["error"] = null
                    });
                    break;
                case "account/read":
                    Respond(id, new JObject
                    {
                        ["account"] = new JObject
                        {
                            ["type"] = "chatgpt",
                            ["email"] = "user@example.com",
                            ["planType"] = "pro"
                        },
                        ["requiresOpenaiAuth"] = true
                    });
                    break;
                case "model/list":
                    Respond(id, new JObject
                    {
                        ["data"] = new JArray
                        {
                            Model("gpt-6-astra", true, "low", "medium", "high"),
                            Model("gpt-5.3-codex", false, "medium", "high")
                        },
                        ["nextCursor"] = null
                    });
                    break;
                case "thread/start":
                    Respond(id, new JObject
                    {
                        ["model"] = message["params"]?["model"] ?? "gpt-5.4",
                        ["thread"] = new JObject { ["id"] = "thread-1" }
                    });
                    break;
                case "turn/start":
                    Respond(id, new JObject
                    {
                        ["turn"] = new JObject
                        {
                            ["id"] = "turn-1",
                            ["status"] = "inProgress"
                        }
                    });
                    Notify("item/agentMessage/delta", new JObject
                    {
                        ["threadId"] = "thread-1",
                        ["turnId"] = "turn-1",
                        ["itemId"] = "item-1",
                        ["delta"] = "Codex "
                    });
                    Notify("item/agentMessage/delta", new JObject
                    {
                        ["threadId"] = "thread-1",
                        ["turnId"] = "turn-1",
                        ["itemId"] = "item-1",
                        ["delta"] = "reply"
                    });
                    Notify("turn/completed", new JObject
                    {
                        ["threadId"] = "thread-1",
                        ["turn"] = new JObject
                        {
                            ["id"] = "turn-1",
                            ["status"] = "completed"
                        }
                    });
                    break;
                default:
                    Respond(id, new JObject());
                    break;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            stopped = true;
            outputSignal.Release();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            stopped = true;
            outputSignal.Dispose();
        }

        private void Respond(JToken id, JToken result)
        {
            Enqueue(new JObject
            {
                ["id"] = id.DeepClone(),
                ["result"] = result
            });
        }

        private void Notify(string method, JObject parameters)
        {
            Enqueue(new JObject
            {
                ["method"] = method,
                ["params"] = parameters
            });
        }

        private void Enqueue(JObject message)
        {
            output.Enqueue(message.ToString(Formatting.None));
            outputSignal.Release();
        }

        private static JObject Model(string model, bool isDefault, params string[] efforts)
        {
            JObject result = new JObject
            {
                ["id"] = model,
                ["model"] = model,
                ["displayName"] = model,
                ["description"] = $"{model} description",
                ["hidden"] = false,
                ["isDefault"] = isDefault,
                ["defaultReasoningEffort"] = efforts[0],
                ["defaultServiceTier"] = "priority",
                ["supportedReasoningEfforts"] = new JArray(efforts.Select(effort => new JObject
                {
                    ["reasoningEffort"] = effort,
                    ["description"] = effort
                })),
                ["serviceTiers"] = new JArray(new JObject
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
                ["inputModalities"] = new JArray("text")
            };
            if (model == "gpt-6-astra")
            {
                ((JArray)result["serviceTiers"]!).Add(new JObject { ["id"] = "ultrafast", ["name"] = "Ultrafast" });
            }
            return result;
        }
    }
}
