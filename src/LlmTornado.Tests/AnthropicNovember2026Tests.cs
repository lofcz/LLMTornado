using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using LlmTornado.Anthropic;
using LlmTornado.Batch;
using LlmTornado.Batch.Vendors.Anthropic;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;
using LlmTornado.RateLimits;
using LlmTornado.Tokenize;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Tests;

[TestFixture]
public class AnthropicNovember2026Tests
{
    private static AnthropicEndpointProvider Provider(string version = AnthropicApiVersions.Legacy)
    {
        TornadoApi api = new(new AnthropicEndpointProvider { ApiRevision = version, Auth = new(LLmProviders.Anthropic, "local-test-key") });
        return (AnthropicEndpointProvider)api.GetProvider(LLmProviders.Anthropic);
    }

    private static ChatRequest Request() => new()
    {
        Model = "claude-sonnet-4-6", MaxTokens = 1024,
        Messages = [new(ChatMessageRoles.User, "Hello")]
    };

    [TestCase(AnthropicApiVersions.Legacy, true)]
    [TestCase(AnthropicApiVersions.October2026, false)]
    public void ApiVersion_AppliesToHeadersAndBatchSamplingWithoutMutatingRequests(string version, bool sampling)
    {
        var provider = Provider(version);
        var request = Request();
        request.Temperature = 0.4; request.TopP = 0.6; request.TopK = 4;
        JObject body = JObject.Parse(request.Serialize(provider).Body.ToString()!);
        Assert.That(body.ContainsKey("temperature"), Is.EqualTo(sampling));
        Assert.That(body.ContainsKey("top_p"), Is.EqualTo(sampling));
        Assert.That(body.ContainsKey("top_k"), Is.EqualTo(sampling));
        var batch = new BatchRequest { Requests = [new() { CustomId = "one", Params = request }] };
        JObject batched = JObject.Parse(new VendorAnthropicBatchRequest(batch, provider).Serialize());
        Assert.That(((JObject)batched["requests"]![0]!["params"]!).ContainsKey("temperature"), Is.EqualTo(sampling));
        using var http = provider.OutboundMessage("https://api.anthropic.com/v1/messages", HttpMethod.Post, null, false, request);
        Assert.That(http.Headers.GetValues("anthropic-version"), Is.EqualTo(new[] { version }));
        Assert.That(request.Temperature, Is.EqualTo(0.4));
    }

    [Test]
    public void OctoberVersion_ValidatesContentForMessagesCountingAndBatches()
    {
        var request = Request();
        request.Messages = [new(ChatMessageRoles.Assistant, [new ChatMessagePart(new ChatDocument(new Uri("https://example.com/file.pdf")))])];
        var provider = Provider(AnthropicApiVersions.October2026);
        Assert.Throws<ArgumentException>(() => request.Serialize(provider));
        Assert.Throws<ArgumentException>(() => new TokenizeRequest(request).Serialize(provider));
        Assert.Throws<ArgumentException>(() => new VendorAnthropicBatchRequest(new() { Requests = [new() { CustomId = "x", Params = request }] }, provider));
        Assert.DoesNotThrow(() => request.Serialize(Provider()));
        request.Messages[0].Role = ChatMessageRoles.User;
        Assert.DoesNotThrow(() => request.Serialize(provider));
    }

    [Test]
    public void OctoberVersion_RejectsUserCitationsButPreservesAssistantCitations()
    {
        var request = Request();
        request.Messages = [new(ChatMessageRoles.User, [new ChatMessagePart("Quoted") { Citations = [new ChatMessagePartCitationWebSearchResultLocation { CitedText = "Quoted", Url = "https://example.com", Title = "Source" }] }])];
        var provider = Provider(AnthropicApiVersions.October2026);
        Assert.Throws<ArgumentException>(() => request.Serialize(provider));
        request.Messages[0].Role = ChatMessageRoles.Assistant;
        JObject body = JObject.Parse(request.Serialize(provider).Body.ToString()!);
        Assert.That((JArray)body["messages"]![0]!["content"]![0]!["citations"]!, Has.Count.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Threads_SerializeNativeCreateAndEmptyPauseContinuation_WithBeta(bool continuation)
    {
        var provider = Provider();
        var request = Request();
        request.VendorExtensions = new(new ChatRequestVendorAnthropicExtensions { Thread = continuation ? AnthropicMessageThread.Continue("msg_previous") : AnthropicMessageThread.Create() });
        if (continuation) request.Messages = [];
        JObject body = JObject.Parse(request.Serialize(provider).Body.ToString()!);
        Assert.That((string?)body["thread"]?["type"], Is.EqualTo(continuation ? "continue" : "create"));
        Assert.That((string?)body["thread"]?["previous_message_id"], Is.EqualTo(continuation ? "msg_previous" : null));
        Assert.That(body["system"], Is.Null);
        Assert.That(body["tools"], Is.Null);
        if (continuation) Assert.That(body["messages"], Is.Empty);
        using var http = provider.OutboundMessage("https://api.anthropic.com/v1/messages", HttpMethod.Post, null, true, request);
        Assert.That(http.Headers.GetValues("anthropic-beta"), Does.Contain(AnthropicMessageThread.BetaHeader));
        JObject copy = JObject.Parse(new ChatRequest(request).Serialize(provider).Body.ToString()!);
        Assert.That(JToken.DeepEquals(copy["thread"], body["thread"]), Is.True);
    }

    [Test]
    public void Threads_RejectConflictingHistoryAndBatchUse()
    {
        var request = Request();
        request.VendorExtensions = new(new ChatRequestVendorAnthropicExtensions { Thread = AnthropicMessageThread.Continue("msg_previous") });
        request.Messages!.Insert(0, new(ChatMessageRoles.System, "Changed system"));
        Assert.Throws<ArgumentException>(() => request.Serialize(Provider()));
        request.Messages.RemoveAt(0);
        request.VendorExtensions.Anthropic!.Thread!.PreviousMessageId = "";
        Assert.Throws<ArgumentException>(() => request.Serialize(Provider()));
        request.VendorExtensions.Anthropic.Thread = AnthropicMessageThread.Create();
        Assert.Throws<ArgumentException>(() => new VendorAnthropicBatchRequest(new() { Requests = [new() { Params = request }] }, Provider()));
        request.VendorExtensions.Anthropic.Compaction = new();
        Assert.Throws<ArgumentException>(() => request.Serialize(Provider()));
    }

    [Test]
    public void ThreadTokenCounting_RequiresFullHistoryAndErrorCodesStayAvailable()
    {
        var request = Request();
        request.VendorExtensions = new(new ChatRequestVendorAnthropicExtensions { Thread = AnthropicMessageThread.Continue("msg_old") });
        Assert.Throws<ArgumentException>(() => new TokenizeRequest(request).Serialize(Provider()));
        request.VendorExtensions.Anthropic!.Thread = AnthropicMessageThread.Create();
        JObject tokens = JObject.Parse(new TokenizeRequest(request).Serialize(Provider()).Body.ToString()!);
        Assert.That(tokens["thread"], Is.Null);
        var error = AnthropicErrorResponse.Parse("""{"type":"error","error":{"type":"not_found_error","details":{"error_code":"thread_not_found","unsupported":["context_management"],"extra":3}}}""")!;
        Assert.That(error.Error!.Details!.ErrorCode, Is.EqualTo("thread_not_found"));
        Assert.That(error.Error.Details.AdditionalData!["extra"].Value<int>(), Is.EqualTo(3));
    }

    [Test]
    public async Task ThreadContinuation_UsesResponseIdAndPreservesRefusalAndExpiryErrors()
    {
        var request = Request();
        request.VendorExtensions = new(new ChatRequestVendorAnthropicExtensions { Thread = AnthropicMessageThread.Create() });
        var created = await Exchange((api, ct) => { request.CancellationToken = ct; return api.Chat.CreateChatCompletionSafe(request); },
            """{"id":"msg_first","type":"message","role":"assistant","model":"claude-sonnet-4-6","content":[{"type":"text","text":"Hello"}],"stop_reason":"end_turn","usage":{"input_tokens":4,"output_tokens":2}}""");
        Assert.That(created.Result.Data!.Id, Is.EqualTo("msg_first"));
        request.Messages = [new(ChatMessageRoles.User, "Next question")];
        request.VendorExtensions.Anthropic!.Thread = AnthropicMessageThread.Continue(created.Result.Data.Id!);
        var refused = await Exchange((api, ct) => { request.CancellationToken = ct; return api.Chat.CreateChatCompletionSafe(request); },
            """{"id":"msg_refused","type":"message","role":"assistant","model":"claude-sonnet-4-6","content":[{"type":"text","text":"Cannot answer"}],"stop_reason":"refusal","usage":{"input_tokens":5,"output_tokens":2}}""");
        JObject body = JObject.Parse(refused.Body);
        Assert.That((string?)body["thread"]?["previous_message_id"], Is.EqualTo("msg_first"));
        Assert.That((JArray)body["messages"]!, Has.Count.EqualTo(1));
        Assert.That(refused.Result.Data!.Choices![0].FinishReason, Is.EqualTo(ChatMessageFinishReasons.Refusal));
        var expired = await Exchange((api, ct) => { request.CancellationToken = ct; return api.Chat.CreateChatCompletionSafe(request); },
            """{"type":"error","error":{"type":"not_found_error","message":"Expired","details":{"error_code":"thread_not_found"}}}""", 404);
        Assert.That(expired.Result.Code, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(AnthropicErrorResponse.Parse(expired.Result.Response!)!.Error!.Details!.ErrorCode, Is.EqualTo("thread_not_found"));
        Assert.That(request.VendorExtensions.Anthropic.Thread.PreviousMessageId, Is.EqualTo("msg_first"), "The low-level API must not silently advance or retry caller-owned history.");
    }

    [Test]
    public async Task Threads_StreamThroughThePublicChatEndpoint()
    {
        var request = Request();
        request.VendorExtensions = new(new ChatRequestVendorAnthropicExtensions { Thread = AnthropicMessageThread.Continue("msg_prior") });
        const string sse = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_stream","type":"message","role":"assistant","model":"claude-sonnet-4-6","content":[],"usage":{"input_tokens":4,"output_tokens":0}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":2}}

            event: message_stop
            data: {"type":"message_stop"}

            """;
        var exchange = await Exchange(async (api, ct) =>
        {
            request.CancellationToken = ct;
            List<ChatResult> chunks = [];
            await foreach (var chunk in api.Chat.StreamChatEnumerable(request)) chunks.Add(chunk);
            return chunks;
        }, sse, contentType: "text/event-stream");
        Assert.That(exchange.Beta, Does.Contain(AnthropicMessageThread.BetaHeader));
        Assert.That(exchange.Result.Any(x => x.Id == "msg_stream"), Is.True);
        Assert.That(exchange.Result.SelectMany(x => x.Choices ?? []).Any(x => x.FinishReason == ChatMessageFinishReasons.EndTurn), Is.True);
        Assert.That((bool?)JObject.Parse(exchange.Body)["stream"], Is.True);
        Assert.That(request.VendorExtensions.Anthropic!.Thread!.PreviousMessageId, Is.EqualTo("msg_prior"));
    }

    [Test]
    public void Profiles_AreRequestScopedHeadersForChatCountingAndBatches()
    {
        var provider = Provider();
        var request = Request();
        request.VendorExtensions = new(new ChatRequestVendorAnthropicExtensions { UserProfileId = "uprof_one" });
        var batch = new BatchRequest { VendorExtensions = new() { Anthropic = new() { UserProfileId = "uprof_one" } }, Requests = [new() { Params = request }] };
        foreach (object source in new object[] { request, new TokenizeRequest(request), batch })
        {
            using var http = provider.OutboundMessage("https://api.anthropic.com/v1/messages", HttpMethod.Post, null, false, source);
            Assert.That(http.Headers.GetValues("anthropic-user-profile-id"), Is.EqualTo(new[] { "uprof_one" }));
        }
        Assert.That(JObject.Parse(request.Serialize(provider).Body.ToString()!).ContainsKey("user_profile_id"), Is.False);
        batch.VendorExtensions.Anthropic!.UserProfileId = "uprof_two";
        Assert.Throws<ArgumentException>(() => new VendorAnthropicBatchRequest(batch, provider));
        using var next = provider.OutboundMessage("https://api.anthropic.com/v1/messages", HttpMethod.Post, null, false, Request());
        Assert.That(next.Headers.Contains("anthropic-user-profile-id"), Is.False);
    }

    [Test]
    public void GoogleCloud_SeparatesOrganizationFromWorkspaceAndRefreshesCallerToken()
    {
        var provider = Provider(AnthropicApiVersions.October2026);
        int calls = 0;
        provider.GoogleCloud = new() { ProjectId = "project-one", WorkspaceId = "wrkspc_one", AccessTokenProvider = () => "token-" + ++calls };
        Assert.That(provider.ApiUrl(CapabilityEndpoints.Chat, null), Is.EqualTo("https://claude.googleapis.com/v1/projects/project-one/locations/global/workspaces/wrkspc_one/invoke/v1/messages"));
        Assert.That(provider.ApiUrl(CapabilityEndpoints.AnthropicUserProfiles, "/uprof_one"), Does.Contain("/claudeOrganization/invoke/v1/user_profiles/uprof_one"));
        Assert.That(provider.ApiUrl(CapabilityEndpoints.RateLimits, null), Does.Contain("/claudeOrganization/invoke/v1/organizations/rate_limits"));
        for (int i = 1; i <= 2; i++)
        {
            using var http = provider.OutboundMessage(provider.ApiUrl(CapabilityEndpoints.Chat, null), HttpMethod.Post, null, false, Request());
            Assert.That(http.Headers.Authorization?.ToString(), Is.EqualTo("Bearer token-" + i));
            Assert.That(http.Headers.Contains("x-api-key"), Is.False);
            Assert.That(http.Headers.Contains("anthropic-workspace-id"), Is.False);
        }
        provider.GoogleCloud.WorkspaceId = null;
        Assert.DoesNotThrow(() => provider.ApiUrl(CapabilityEndpoints.AnthropicUserProfiles, null));
        Assert.Throws<ArgumentException>(() => provider.ApiUrl(CapabilityEndpoints.Chat, null));
    }

    [Test]
    public void Updates_DistinguishOmittedAndClearedSettingsAndProfileName()
    {
        var settings = new AnthropicClaudeTagSettingsUpdate { Enabled = false, Instructions = null };
        JObject json = JObject.Parse(JsonConvert.SerializeObject(settings, EndpointBase.NullSettings));
        Assert.That(json.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "enabled", "instructions" }));
        Assert.That(json["instructions"]!.Type, Is.EqualTo(JTokenType.Null));
        Assert.That((bool?)json["enabled"], Is.False);
        var profile = new AnthropicUserProfileUpdateRequest();
        Assert.That(JObject.Parse(JsonConvert.SerializeObject(profile, EndpointBase.NullSettings)).ContainsKey("name"), Is.False);
        profile.ClearName = true;
        Assert.That(JObject.Parse(JsonConvert.SerializeObject(profile, EndpointBase.NullSettings))["name"]!.Type, Is.EqualTo(JTokenType.Null));
        profile.ClearName = false; profile.Name = "New name";
        Assert.That((string?)JObject.Parse(JsonConvert.SerializeObject(profile, EndpointBase.NullSettings))["name"], Is.EqualTo("New name"));
    }

    [Test]
    public void RateLimits_ReadCanonicalGroupAndLegacyGroupType()
    {
        foreach (string json in new[] { "{\"group_type\":\"files\"}", "{\"group\":{\"type\":\"files\",\"id\":\"rlg_files\"}}" })
            Assert.That(JsonConvert.DeserializeObject<AnthropicRateLimitEntry>(json)!.GroupType, Is.EqualTo(AnthropicRateLimitGroupType.Files));
        var canonical = JsonConvert.DeserializeObject<AnthropicRateLimitEntry>("{\"group_type\":\"batch\",\"group\":{\"type\":\"model_group\",\"display_name\":\"Opus\"}}")!;
        Assert.That(canonical.GroupType, Is.EqualTo(AnthropicRateLimitGroupType.ModelGroup));
        Assert.That(canonical.Group!.DisplayName, Is.EqualTo("Opus"));
    }

    [Test]
    public async Task Analytics_PreservesRepeatedFiltersOpaqueCursorAndExactCentAmounts()
    {
        var exchange = await Exchange((api, ct) => api.AnthropicAnalytics.Cost(new()
        {
            StartingAt = "2026-10-01T00:00:00Z", GroupBy = ["actor", "model", "cost_type"], ApiKeyIds = ["", "apikey_a"],
            Models = ["claude-opus-5-5", "claude-sonnet-5-5"], Page = "cursor+/=?&", ExcludeDeletedUsers = true
        }, ct), "{\"data\":[{\"group\":{\"actor\":{\"type\":\"user_actor\",\"email_address\":\"user@example.com\",\"new_field\":3}},\"cost\":{\"amount\":\"41280.000001\",\"currency\":\"USD\"}}],\"next_page\":\"next+/=\",\"data_refreshed_at\":\"2026-11-04T00:00:00Z\"}");
        Assert.That(exchange.Query.GetValues("api_key_ids[]"), Is.EqualTo(new[] { "", "apikey_a" }));
        Assert.That(exchange.Query.GetValues("models[]"), Has.Length.EqualTo(2));
        Assert.That(exchange.Query["page"], Is.EqualTo("cursor+/=?&"));
        Assert.That(exchange.Query["exclude_deleted_users"], Is.EqualTo("true"));
        Assert.That(exchange.Beta, Is.EqualTo("analytics-usage-cost-2026-08-24"));
        Assert.That(exchange.Result.Data!.Data[0].Cost!.Amount, Is.EqualTo("41280.000001"));
        Assert.That(exchange.Result.Data.Data[0].Group!.Actor!.AdditionalData!["new_field"].Value<int>(), Is.EqualTo(3));
        Assert.That(exchange.Result.Data.NextPage, Is.EqualTo("next+/="));
    }

    [Test]
    public async Task Profiles_PreserveEmptyFilteredPagesAndEnforcementActions()
    {
        var empty = await Exchange((api, ct) => api.AnthropicUserProfiles.List(new() { EnforcementStatus = ["restricted", "banned"] }, ct), "{\"data\":[],\"next_page\":\"more\"}");
        Assert.That(empty.Query.GetValues("enforcement_status[]"), Is.EqualTo(new[] { "restricted", "banned" }));
        Assert.That(empty.Beta, Is.Null);
        Assert.That(empty.Result.Data!.NextPage, Is.EqualTo("more"));
        var profile = await Exchange((api, ct) => api.AnthropicUserProfiles.Get("uprof_one", ct), "{\"id\":\"uprof_one\",\"enforcement_status\":\"restricted\",\"enforcement_actions\":[{\"id\":\"enac_one\",\"type\":\"restriction\",\"action\":\"verify\",\"reason\":\"fraud_or_abuse\"}]}");
        Assert.That(profile.Result.Data!.EnforcementActions![0].Action, Is.EqualTo("verify"));
    }

    [TestCase("profile-create", "POST", "/v1/user_profiles")]
    [TestCase("profile-update", "POST", "/v1/user_profiles/uprof_one")]
    [TestCase("profile-delete", "DELETE", "/v1/user_profiles/uprof_one")]
    [TestCase("webhook-create", "POST", "/v1/organizations/webhooks")]
    [TestCase("webhook-get", "GET", "/v1/organizations/webhooks/wep_one")]
    [TestCase("webhook-update", "POST", "/v1/organizations/webhooks/wep_one")]
    [TestCase("webhook-delete", "DELETE", "/v1/organizations/webhooks/wep_one")]
    [TestCase("webhook-rotate", "POST", "/v1/organizations/webhooks/wep_one/regenerate_signing_secret")]
    [TestCase("webhook-list", "GET", "/v1/organizations/webhooks")]
    [TestCase("tag-setup", "POST", "/v1/organizations/claude_tag/slack_channels")]
    [TestCase("tag-channel-update", "POST", "/v1/organizations/claude_tag/slack_channels/slch_one")]
    [TestCase("tag-workspace-update", "POST", "/v1/organizations/claude_tag/slack_workspaces/slws_one")]
    [TestCase("tag-channel-delete", "DELETE", "/v1/organizations/claude_tag/slack_channels/slch_one")]
    [TestCase("tag-channel-get", "GET", "/v1/organizations/claude_tag/slack_channels/slch_one")]
    [TestCase("tag-workspace-get", "GET", "/v1/organizations/claude_tag/slack_workspaces/slws_one")]
    [TestCase("tag-channels", "GET", "/v1/organizations/claude_tag/slack_channels")]
    [TestCase("tag-workspaces", "GET", "/v1/organizations/claude_tag/slack_workspaces")]
    [TestCase("tag-bundle", "GET", "/v1/organizations/claude_tag/bundles/capp_one")]
    [TestCase("tag-bundles", "GET", "/v1/organizations/claude_tag/bundles")]
    [TestCase("tag-attach", "POST", "/v1/organizations/claude_tag/bundles/capp_one/targets")]
    [TestCase("tag-target", "GET", "/v1/organizations/claude_tag/bundles/capp_one/targets/btgt_one")]
    [TestCase("tag-targets", "GET", "/v1/organizations/claude_tag/bundles/capp_one/targets")]
    [TestCase("tag-detach", "DELETE", "/v1/organizations/claude_tag/bundles/capp_one/targets/btgt_one")]
    [TestCase("usage", "GET", "/v1/organizations/analytics/usage")]
    [TestCase("workspace-limits", "GET", "/v1/organizations/workspaces/wrkspc_one/rate_limits")]
    public async Task Resources_UseNativePathsMethodsAndFeatureHeaders(string operation, string method, string path)
    {
        var exchange = await Exchange(async (api, ct) =>
        {
            IHttpCallResult result = operation switch
            {
                "profile-create" => await api.AnthropicUserProfiles.Create(new() { Name = "Customer" }, ct),
                "profile-update" => await api.AnthropicUserProfiles.Update("uprof_one", new() { ClearName = true, Metadata = new() { ["remove"] = "" } }, ct),
                "profile-delete" => await api.AnthropicUserProfiles.Delete("uprof_one", ct),
                "webhook-create" => await api.AnthropicOrganizationWebhooks.Create(new() { Url = "https://example.com/hook", EnabledEvents = [AnthropicOrganizationWebhookEventTypes.EnforcementActionOpened] }, ct),
                "webhook-get" => await api.AnthropicOrganizationWebhooks.Get("wep_one", ct),
                "webhook-update" => await api.AnthropicOrganizationWebhooks.Update("wep_one", new() { State = "disabled" }, ct),
                "webhook-delete" => await api.AnthropicOrganizationWebhooks.Delete("wep_one", ct),
                "webhook-rotate" => await api.AnthropicOrganizationWebhooks.RegenerateSigningSecret("wep_one", ct),
                "webhook-list" => await api.AnthropicOrganizationWebhooks.List(new() { AfterId = "wep_old", Limit = 10 }, ct),
                "tag-setup" => await api.AnthropicClaudeTag.SetupChannel(new() { Slack = new() { WorkspaceId = "T123", ChannelId = "C123" } }, ct),
                "tag-channel-update" => await api.AnthropicClaudeTag.UpdateChannel("slch_one", new() { Instructions = null }, ct),
                "tag-workspace-update" => await api.AnthropicClaudeTag.UpdateWorkspace("slws_one", new() { Enabled = false }, ct),
                "tag-channel-delete" => await api.AnthropicClaudeTag.DeleteChannel("slch_one", ct),
                "tag-channel-get" => await api.AnthropicClaudeTag.GetChannel("slch_one", true, ct),
                "tag-workspace-get" => await api.AnthropicClaudeTag.GetWorkspace("slws_one", true, ct),
                "tag-channels" => await api.AnthropicClaudeTag.ListChannels(new() { SlackWorkspaceId = "T123", IncludeInherited = true }, ct),
                "tag-workspaces" => await api.AnthropicClaudeTag.ListWorkspaces(cancellationToken: ct),
                "tag-bundle" => await api.AnthropicClaudeTag.GetBundle("capp_one", ct),
                "tag-bundles" => await api.AnthropicClaudeTag.ListBundles(cancellationToken: ct),
                "tag-attach" => await api.AnthropicClaudeTag.AttachBundle("capp_one", new() { Type = "slack_channel", SlackChannelId = "slch_one" }, ct),
                "tag-target" => await api.AnthropicClaudeTag.GetBundleTarget("capp_one", "btgt_one", ct),
                "tag-targets" => await api.AnthropicClaudeTag.ListBundleTargets("capp_one", cancellationToken: ct),
                "tag-detach" => await api.AnthropicClaudeTag.DetachBundle("capp_one", "btgt_one", ct),
                "usage" => await api.AnthropicAnalytics.Usage(new() { StartingAt = "2026-10-01T00:00:00Z" }, ct),
                _ => await api.RateLimits.ListWorkspaceRateLimits("wrkspc_one", cancellationToken: ct)
            };
            Assert.That(result.Code, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result.Exception, Is.Null);
            return result;
        }, "{\"id\":\"resource\",\"data\":[],\"signing_secret\":\"whsec_test\"}");
        Assert.That(exchange.Path, Is.EqualTo(path));
        Assert.That(exchange.Method, Is.EqualTo(method));
        if (operation.StartsWith("tag-")) Assert.That(exchange.Beta, Is.EqualTo("experimental-claude-tag-2026-10-21"));
        if (operation.StartsWith("webhook-")) Assert.That(exchange.Beta, Is.EqualTo("webhooks-2026-10-07"));
        if (operation == "profile-update") Assert.That(JObject.Parse(exchange.Body)["name"]!.Type, Is.EqualTo(JTokenType.Null));
        if (operation == "tag-channel-update") Assert.That(JObject.Parse(exchange.Body)["settings"]!["instructions"]!.Type, Is.EqualTo(JTokenType.Null));
        if (operation == "tag-attach") Assert.That((string?)JObject.Parse(exchange.Body)["target"]?["slack_channel_id"], Is.EqualTo("slch_one"));
    }

    [Test]
    public async Task Resources_PreserveProviderErrors()
    {
        const string error = "{\"type\":\"error\",\"error\":{\"type\":\"permission_error\",\"message\":\"Missing scope\",\"details\":{\"error_code\":\"scope_required\"}}}";
        var exchange = await Exchange((api, ct) => api.AnthropicClaudeTag.ListWorkspaces(cancellationToken: ct), error, 403);
        Assert.That(exchange.Result.Code, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(exchange.Result.Response, Is.EqualTo(error));
        Assert.That(exchange.Result.Data, Is.Null);
    }

    [Test]
    public void WebhookVerification_ChecksRawBodyTimestampAndSignatureRotation()
    {
        const string payload = "{\"type\":\"event\",\"id\":\"whe_one\",\"data\":{\"type\":\"user_profile.enforcement_action_opened\",\"id\":\"uprof_one\",\"enforcement_action_id\":\"enac_one\"}}";
        byte[] key = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        using var hmac = new HMACSHA256(key);
        string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"whe_one.{timestamp}.{payload}")));
        var headers = new Dictionary<string, string> { ["Webhook-Id"] = "whe_one", ["webhook-timestamp"] = timestamp, ["webhook-signature"] = "v1,invalid v1," + signature };
        string secret = "whsec_" + Convert.ToBase64String(key);
        Assert.That(AnthropicWebhookSignatureVerifier.Verify(payload, headers, secret).Data!.EnforcementActionId, Is.EqualTo("enac_one"));
        Assert.Throws<CryptographicException>(() => AnthropicWebhookSignatureVerifier.Verify(payload + " ", headers, secret));
        headers["webhook-timestamp"] = "0";
        Assert.Throws<CryptographicException>(() => AnthropicWebhookSignatureVerifier.Verify(payload, headers, secret));
    }

    private static async Task<ExchangeResult<T>> Exchange<T>(Func<TornadoApi, CancellationToken, Task<T>> action, string response, int status = 200, string contentType = "application/json")
    {
        using TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start(); int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        string prefix = $"http://127.0.0.1:{port}/";
        using HttpListener listener = new(); listener.Prefixes.Add(prefix); listener.Start();
        var provider = new AnthropicEndpointProvider { ApiRevision = AnthropicApiVersions.October2026, Auth = new(LLmProviders.Anthropic, "local-test-key"),
            UrlResolver = (endpoint, url, context) => prefix + "v1/" + context.Endpoint + (endpoint == CapabilityEndpoints.RateLimits ? "" : url) };
        TornadoApi api = new(provider);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        Task<T> operation = action(api, timeout.Token);
        var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        Assert.That(context.Request.Headers["x-api-key"], Is.EqualTo("local-test-key"));
        Assert.That(context.Request.Headers["anthropic-version"], Is.EqualTo(AnthropicApiVersions.October2026));
        string body = await new StreamReader(context.Request.InputStream).ReadToEndAsync(timeout.Token);
        var result = new ExchangeResult<T> { Path = context.Request.Url!.AbsolutePath, Method = context.Request.HttpMethod, Query = context.Request.QueryString, Body = body, Beta = context.Request.Headers["anthropic-beta"] };
        context.Response.StatusCode = status; context.Response.ContentType = contentType;
        await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(response), timeout.Token); context.Response.Close();
        result.Result = await operation.WaitAsync(timeout.Token);
        return result;
    }

    private sealed class ExchangeResult<T>
    {
        internal T Result { get; set; } = default!;
        internal string Path { get; set; } = "";
        internal string Method { get; set; } = "";
        internal string Body { get; set; } = "";
        internal string? Beta { get; set; }
        internal System.Collections.Specialized.NameValueCollection Query { get; set; } = new();
    }
}
