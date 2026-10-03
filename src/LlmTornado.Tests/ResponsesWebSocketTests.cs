using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Demo;
using LlmTornado.Files;
using LlmTornado.Responses;
using LlmTornado.Responses.Events;
using LlmTornado.Skills;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class ResponsesWebSocketTests
{
    [SetUp]
    public async Task Setup()
    {
        await Program.SetupApi();
    }

    [Test]
    public void ShellTool_WithSkillsAndNetworkPolicy_SerializesExpectedShape()
    {
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt51.V51,
            InputString = "Run a command",
            Tools =
            [
                new ResponseShellTool
                {
                    Environment = new ResponseShellEnvironmentContainerAuto
                    {
                        Skills =
                        [
                            new ResponseShellSkillReference("skill_abc", "latest"),
                            new ResponseShellInlineSkill
                            {
                                Name = "inline-demo",
                                Description = "Inline skill bundle",
                                Source = new ResponseShellInlineSkillSource
                                {
                                    Data = Convert.ToBase64String([0x50, 0x4B, 0x03, 0x04])
                                }
                            }
                        ],
                        NetworkPolicy = new ResponseNetworkPolicy
                        {
                            AllowedDomains = ["api.example.com", "pypi.org"],
                            DomainSecrets =
                            [
                                new ResponseDomainSecret("api.example.com", "API_KEY", "secret-value")
                            ]
                        }
                    }
                }
            ]
        };

        string json = JsonConvert.SerializeObject(request);
        JObject jo = JObject.Parse(json);

        JToken shellTool = jo["tools"]![0]!;
        Assert.That(shellTool["type"]!.ToString(), Is.EqualTo("shell"));

        JToken env = shellTool["environment"]!;
        Assert.That(env["type"]!.ToString(), Is.EqualTo("container_auto"));
        Assert.That(env["skills"], Is.Not.Null);
        Assert.That(env["skills"]!.Count(), Is.EqualTo(2));
        Assert.That(env["skills"]![0]!["type"]!.ToString(), Is.EqualTo("skill_reference"));
        Assert.That(env["skills"]![0]!["skill_id"]!.ToString(), Is.EqualTo("skill_abc"));
        Assert.That(env["network_policy"]!["type"]!.ToString(), Is.EqualTo("allowlist"));
        Assert.That(env["network_policy"]!["allowed_domains"]!.Count(), Is.EqualTo(2));
        Assert.That(env["network_policy"]!["domain_secrets"]![0]!["name"]!.ToString(), Is.EqualTo("API_KEY"));
    }

    [Test]
    public void WebSocketCreatePayload_StripsTransportFields()
    {
        ResponseRequest request = new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt6.V6Astra,
            ServiceTier = ChatRequestServiceTiers.Ultrafast,
            InputString = "hello",
            Stream = true,
            Background = true,
            StreamOptions = new ResponseStreamOptions { IncludeObfuscation = true }
        };

        string payload = ResponsesWebSocketConnection.BuildCreatePayload(
            request,
            Program.Connect().GetProvider(LLmProviders.OpenAi),
            new ResponseWebSocketCreateOptions { Generate = false });

        JObject jo = JObject.Parse(payload);
        Assert.That(jo["type"]!.ToString(), Is.EqualTo("response.create"));
        Assert.That(jo["stream"], Is.Null);
        Assert.That(jo["background"], Is.Null);
        Assert.That(jo["stream_options"], Is.Null);
        Assert.That(jo["generate"]!.Value<bool>(), Is.False);
        Assert.That(jo["input"]!.ToString(), Is.EqualTo("hello"));
        Assert.That(jo["service_tier"]?.ToString(), Is.EqualTo("ultrafast"));
    }

    [Test]
    public async Task WebSocketConnectAndMultiTurn_Production()
    {
        TornadoApi api = Program.Connect();
        await using ResponsesWebSocketConnection connection = await api.Responses.ConnectWebSocketAsync();

        Assert.That(connection.State, Is.EqualTo(System.Net.WebSockets.WebSocketState.Open));

        List<ResponseEventTypes> eventTypes = [];
        ResponseStreamEventHandler handler = new ResponseStreamEventHandler
        {
            OnEvent = evt =>
            {
                eventTypes.Add(evt.EventType);
                return ValueTask.CompletedTask;
            }
        };

        ResponseResult? first = await connection.CreateResponseAsync(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41Mini,
            Store = false,
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, "Reply with exactly: websocket-ok")
            ]
        }, handler);

        Assert.That(first, Is.Not.Null);
        Assert.That(first!.OutputText, Does.Contain("websocket-ok").IgnoreCase);
        Assert.That(eventTypes, Does.Contain(ResponseEventTypes.ResponseCompleted));

        ResponseResult? second = await connection.CreateResponseAsync(new ResponseRequest
        {
            Model = ChatModel.OpenAi.Gpt41.V41Mini,
            Store = false,
            InputItems =
            [
                new ResponseInputMessage(ChatMessageRoles.User, "Now reply with exactly: websocket-turn-2")
            ]
        }, handler);

        Assert.That(second, Is.Not.Null);
        Assert.That(second!.OutputText, Does.Contain("websocket-turn-2").IgnoreCase);
        Assert.That(connection.CurrentResponseId, Is.EqualTo(second.Id));
    }

    [Test]
    public async Task OpenAiSkills_CreateListDelete_Production()
    {
        TornadoApi api = Program.Connect();
        string skillMdPath = Path.Combine(AppContext.BaseDirectory, "Static", "Files", "Skills", "pdf-processor", "SKILL.md");
        Assume.That(File.Exists(skillMdPath), Is.True, $"Missing test skill file at {skillMdPath}");

        OpenAiSkill skill = await api.OpenAiSkills.CreateSkillAsync(new OpenAiSkillUploadRequest(
            new FileUploadRequest
            {
                Bytes = await File.ReadAllBytesAsync(skillMdPath),
                Name = "pdf-processor/SKILL.md",
                MimeType = "text/markdown"
            }));

        Assert.That(skill.Id, Is.Not.Empty);

        OpenAiSkillList listed = await api.OpenAiSkills.ListSkillsAsync(limit: 20);
        Assert.That(listed.Data.Any(x => x.Id == skill.Id), Is.True);

        OpenAiSkill fetched = await api.OpenAiSkills.GetSkillAsync(skill.Id);
        Assert.That(fetched.Id, Is.EqualTo(skill.Id));

        bool deleted = await api.OpenAiSkills.DeleteSkillAsync(skill.Id);
        Assert.That(deleted, Is.True);
    }
}
