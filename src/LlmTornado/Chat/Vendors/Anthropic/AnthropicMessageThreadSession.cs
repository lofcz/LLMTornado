using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Anthropic;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;
using LlmTornado.Vendor.Anthropic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Chat.Vendors.Anthropic;

/// <summary>
/// Portable native parameters and full local history. Restoring creates a new server thread.
/// Native blocks preserve tool results, citations, and signed thinking without conversion loss.
/// </summary>
public sealed class AnthropicMessageThreadSnapshot
{
    /// <summary>Messages API parameters, including full messages, without thread or stream.</summary>
    public JObject Parameters { get; set; } = new();
    /// <summary>Feature betas to keep on every turn.</summary>
    public List<string> BetaHeaders { get; set; } = [];
    /// <summary>End-customer attribution for this conversation.</summary>
    public string? UserProfileId { get; set; }
    /// <summary>Protocol revision used to serialize these parameters.</summary>
    public string ApiRevision { get; set; } = AnthropicApiVersions.Legacy;
}

internal sealed class AnthropicThreadRequestContext
{
    internal string ApiRevision { get; set; } = AnthropicApiVersions.Legacy;
    internal string? UserProfileId { get; set; }
    internal IEnumerable<string> BetaHeaders { get; set; } = [];
}

/// <summary>
/// Owns a Claude conversation locally and uses server-held history as an optimization.
/// Only one send, stream, or mutation may run at a time. Dispose a stream's enumerator before reuse.
/// Tool calls are returned for the caller to execute; append their results in the next turn.
/// </summary>
public sealed class AnthropicMessageThreadSession
{
    private readonly ChatEndpoint endpoint;
    private readonly TornadoApi api;
    private readonly AnthropicEndpointProvider provider;
    private readonly bool continueUnthreaded;
    private readonly List<string> betaHeaders;
    private readonly string apiRevision;
    private readonly string? userProfileId;
    private string? url;
    private CancellationToken defaultCancellationToken;
    private JObject parameters;
    private JArray history = new();
    private JArray pending;
    private int busy;
    private bool hasResponse;
    private bool replayedLastTurn;
    private bool paused;
    private int? interruptedHistoryCount;
    private JArray? interruptedMessages;

    /// <summary>The completed response ID to continue, or null when a full create is needed.</summary>
    public string? PreviousMessageId { get; private set; }
    /// <summary>The last accepted complete response. Refusals and partial streams do not replace it.</summary>
    public ChatResult? LastMessage { get; private set; }
    /// <summary>True after the optional unsupported-request policy has disabled server threads.</summary>
    public bool IsUnthreaded { get; private set; }
    /// <summary>Independent snapshot of all completed and staged native messages.</summary>
    public JArray Messages => WithLock(() => new JArray(history.Concat(pending).Select(x => x.DeepClone())));

    internal AnthropicMessageThreadSession(ChatEndpoint endpoint, TornadoApi api, ChatRequest request, bool continueUnthreaded)
        : this(endpoint, api, Snapshot(api, request), continueUnthreaded)
    {
        url = request.UrlOverride;
        defaultCancellationToken = request.CancellationToken;
    }

    internal AnthropicMessageThreadSession(ChatEndpoint endpoint, TornadoApi api, AnthropicMessageThreadSnapshot snapshot, bool continueUnthreaded)
    {
        this.endpoint = endpoint;
        this.api = api;
        provider = api.GetProvider(LLmProviders.Anthropic) as AnthropicEndpointProvider
            ?? throw new ArgumentException("Message threads require the native Anthropic provider.");
        this.continueUnthreaded = continueUnthreaded;
        parameters = (JObject)snapshot.Parameters.DeepClone();
        if (string.IsNullOrWhiteSpace((string?)parameters["model"])) throw new ArgumentException("A model is required.", nameof(snapshot));
        pending = parameters["messages"] as JArray ?? new JArray();
        parameters.Remove("messages");
        parameters.Remove("thread");
        parameters.Remove("stream");
        betaHeaders = snapshot.BetaHeaders.Where(x => x != AnthropicMessageThread.BetaHeader).Distinct().ToList();
        apiRevision = snapshot.ApiRevision;
        userProfileId = snapshot.UserProfileId;
    }

    private static AnthropicMessageThreadSnapshot Snapshot(TornadoApi api, ChatRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.Model is null) throw new ArgumentException("An Anthropic model is required.", nameof(request));
        if (api.GetProvider(request.Model!) is not AnthropicEndpointProvider provider)
            throw new ArgumentException("Message threads require an Anthropic model.", nameof(request));
        if (request.VendorExtensions?.Anthropic?.Thread is not null)
            throw new ArgumentException("The session manages thread IDs; omit the prototype's Thread field.", nameof(request));
        TornadoRequestContent serialized = request.Serialize(provider);
        List<string> betas = AnthropicEndpointProvider.GetBetaHeaders(request).ToList();
        if (provider.RequestResolver is not null)
        {
            using HttpRequestMessage resolved = provider.OutboundMessage(serialized.BuildFinalUrl()!, HttpMethod.Post, serialized.Body, false, request);
            betas = resolved.Headers.TryGetValues("anthropic-beta", out IEnumerable<string>? values)
                ? values.SelectMany(x => x.Split(',')).Select(x => x.Trim()).Where(x => x.Length > 0).ToList() : [];
        }
        return new AnthropicMessageThreadSnapshot
        {
            Parameters = JObject.Parse(serialized.Body.ToString()!),
            BetaHeaders = betas,
            ApiRevision = provider.ApiRevision,
            UserProfileId = request.VendorExtensions?.Anthropic?.UserProfileId
        };
    }

    /// <summary>Exports full native parameters and history, including any staged messages.</summary>
    public AnthropicMessageThreadSnapshot Export() => WithLock(ExportCore);

    private AnthropicMessageThreadSnapshot ExportCore()
    {
        JObject all = (JObject)parameters.DeepClone();
        all["messages"] = new JArray(history.Concat(pending).Select(x => x.DeepClone()));
        return new AnthropicMessageThreadSnapshot
        {
            Parameters = all, BetaHeaders = betaHeaders.ToList(), ApiRevision = apiRevision, UserProfileId = userProfileId
        };
    }

    /// <summary>Copies all history and settings. Its first send creates an independent thread.</summary>
    public AnthropicMessageThreadSession Fork() => WithLock(() => new AnthropicMessageThreadSession(endpoint, api, ExportCore(), continueUnthreaded)
    {
        history = (JArray)history.DeepClone(), pending = (JArray)pending.DeepClone(), url = url, paused = paused,
        defaultCancellationToken = defaultCancellationToken
    });

    /// <summary>Stages messages without sending them. They remain staged after a refusal.</summary>
    public void Append(IEnumerable<ChatMessage> messages) => WithLock(() => { AppendCore(SerializeMessages(messages)); return true; });

    /// <summary>Stages native user/assistant messages while retaining provider-specific content blocks.</summary>
    public void Append(JArray messages) => WithLock(() => { AppendCore(messages); return true; });

    private void AppendCore(JArray messages)
    {
        ValidateNewMessages(messages);
        foreach (JToken message in messages) pending.Add(message.DeepClone());
    }

    /// <summary>
    /// Updates native parameters for later turns. Fixed fields cannot change after a response;
    /// fork first to change them. MCP authorization tokens and container IDs may be refreshed.
    /// </summary>
    public void Update(JObject fields) => WithLock(() =>
    {
        JObject next = (JObject)parameters.DeepClone();
        foreach (JProperty field in fields.Properties())
        {
            if (field.Name is "messages" or "thread" or "stream")
                throw new ArgumentException($"Use the session methods to change {field.Name}.", nameof(fields));
            if (hasResponse && !JToken.DeepEquals(FixedValue(field.Name, parameters[field.Name]), FixedValue(field.Name, field.Value)))
                throw new InvalidOperationException($"{field.Name} is fixed after a response. Fork or create a new thread to change it.");
            if (field.Value.Type == JTokenType.Null) next.Remove(field.Name);
            else next[field.Name] = field.Value.DeepClone();
        }
        parameters = next;
        return true;
    });

    private static JToken? FixedValue(string name, JToken? value)
    {
        if (value?.Type == JTokenType.Null) value = null;
        switch (name)
        {
            case "system": case "tools": case "model": case "tool_choice": case "thinking":
            case "context_management": case "output_config": case "cache_control": return value;
            case "fallbacks": return new JValue(value is not null);
            case "container": return value is JObject container ? container["skills"] : null;
            case "mcp_servers":
                JToken? servers = value?.DeepClone();
                if (servers is JArray list)
                    foreach (JObject server in list.OfType<JObject>()) server.Remove("authorization_token");
                return servers;
            default: return null;
        }
    }

    /// <summary>
    /// Sends a turn, replaying once on explicit thread expiry/conflict errors. Empty messages resume a
    /// paused turn or retry the last interrupted stream. Other failures leave input staged for retry.
    /// </summary>
    public async Task<ChatResult> Send(IEnumerable<ChatMessage>? messages = null, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(defaultCancellationToken, cancellationToken);
        cancellationToken = linked.Token;
        Enter();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            int stagedBeforeSend = Begin(messages);
            bool replayed = false;
            while (true)
            {
                JObject body = BuildBody(false);
                HttpCallResult<JObject> response = await endpoint.HttpRequestRaw<JObject>(provider, CapabilityEndpoints.Chat,
                    url, verb: HttpVerbs.Post, postData: body.ToString(Formatting.None), model: Model(), requestObj: Context(),
                    ct: cancellationToken, captureHttpErrors: true).ConfigureAwait(false);
                if (!response.Ok || response.Data is null)
                {
                    if (TryRecover(response, ref replayed)) continue;
                    if (response.Response is null) PreviousMessageId = null;
                    throw response.Exception ?? new HttpRequestException(response.Response ?? "No message was returned.");
                }
                return Accept(response.Data, stagedBeforeSend, replayed, response.Request.HttpRequest);
            }
        }
        finally { Exit(); }
    }

    /// <summary>
    /// Streams shared chat events. Read to the end and use LastMessage for the complete response.
    /// Disposing early keeps completed blocks and partial text, discards unfinished tool inputs and
    /// thinking, and resets continuation. Streams are never automatically retried after data arrives.
    /// </summary>
    public async IAsyncEnumerable<ChatResult> Stream(IEnumerable<ChatMessage>? messages = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(defaultCancellationToken, cancellationToken);
        cancellationToken = linked.Token;
        Enter();
        AnthropicThreadStreamAccumulator? accumulator = null;
        int stagedBeforeSend = pending.Count;
        bool replayed = false;
        bool accepted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            stagedBeforeSend = Begin(messages);
            while (true)
            {
                JObject body = BuildBody(true);
                await using TornadoStreamRequest response = await endpoint.HttpStreamingRequestData(provider, CapabilityEndpoints.Chat,
                    url, verb: HttpVerbs.Post, postData: body.ToString(Formatting.None), model: Model(), requestObj: Context(),
                    token: cancellationToken, captureHttpErrors: true).ConfigureAwait(false);
                if (response.Exception is not null || response.StreamReader is null)
                {
                    if (response.CallResponse is not null && TryRecover(response.CallResponse, ref replayed)) continue;
                    if (response.CallResponse is null) PreviousMessageId = null;
                    throw response.Exception ?? new IOException("No response stream was returned.");
                }
                accumulator = new AnthropicThreadStreamAccumulator();
                ChatStreamEventHandler handler = new()
                {
                    OnSse = ev =>
                    {
                        accumulator.Add(ev);
                        if (accumulator.Complete && !accepted)
                        {
                            Accept(accumulator.Message!, stagedBeforeSend, replayed, response.CallRequest);
                            accepted = true;
                        }
                        return default;
                    }
                };
                ChatRequest streamRequest = new() { Model = Model(), Stream = true, CancellationToken = cancellationToken };
                await foreach (ChatResult? chunk in provider.InboundStream(response.StreamReader, streamRequest, handler).ConfigureAwait(false))
                {
                    if (chunk is not null) yield return chunk;
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (!accumulator.Complete) throw new IOException("The Anthropic stream ended before message_stop. The partial turn is retained locally.");
                break;
            }
        }
        finally
        {
            try
            {
                if (accumulator is not null && !accepted) KeepInterrupted(accumulator, stagedBeforeSend);
            }
            finally { Exit(); }
        }
    }

    private ChatModel Model() => new((string)parameters["model"]!, LLmProviders.Anthropic);

    private AnthropicThreadRequestContext Context() => new()
    {
        ApiRevision = apiRevision, UserProfileId = userProfileId,
        BetaHeaders = IsUnthreaded ? betaHeaders : betaHeaders.Concat(new[] { AnthropicMessageThread.BetaHeader })
    };

    private JArray SerializeMessages(IEnumerable<ChatMessage>? messages)
    {
        List<ChatMessage> list = messages?.ToList() ?? [];
        if (list.Any(m => m.Role is not (ChatMessageRoles.User or ChatMessageRoles.Assistant or ChatMessageRoles.Tool)))
            throw new ArgumentException("Send user, assistant, or tool-result messages. Set the system prompt in the prototype.", nameof(messages));
        ChatRequest request = new() { Model = Model(), Messages = list };
        return (JArray)JObject.Parse(request.Serialize(provider).Body.ToString()!)["messages"]!;
    }

    private void ValidateNewMessages(JArray messages)
    {
        if (paused && messages.Count > 0) throw new InvalidOperationException("Resume pause_turn with Send() or Stream() without new messages.");
        if (messages.Any(m => m is not JObject || (string?)m["role"] is not ("user" or "assistant")))
            throw new ArgumentException("Native thread messages must have a user or assistant role.", nameof(messages));
    }

    private int Begin(IEnumerable<ChatMessage>? messages)
    {
        JArray added = SerializeMessages(messages);
        ValidateNewMessages(added);
        if (added.Count == 0 && pending.Count == 0 && interruptedHistoryCount is int count)
        {
            while (history.Count > count) history.RemoveAt(history.Count - 1);
            pending = interruptedMessages!;
        }
        interruptedHistoryCount = null;
        interruptedMessages = null;
        int staged = pending.Count;
        AppendCore(added);
        return staged;
    }

    private JObject BuildBody(bool stream)
    {
        JObject body = (JObject)parameters.DeepClone();
        if (!IsUnthreaded && PreviousMessageId is not null)
        {
            body.Remove("system");
            body.Remove("tools");
            body["messages"] = pending.DeepClone();
            body["thread"] = JObject.FromObject(AnthropicMessageThread.Continue(PreviousMessageId));
        }
        else
        {
            JArray earlier = (JArray)history.DeepClone();
            foreach (JProperty cache in earlier.Descendants().OfType<JProperty>().Where(x => x.Name == "cache_control").ToList()) cache.Remove();
            body["messages"] = new JArray(earlier.Concat(pending).Select(x => x.DeepClone()));
            if (!IsUnthreaded) body["thread"] = JObject.FromObject(AnthropicMessageThread.Create());
        }
        body["stream"] = stream;
        return body;
    }

    private bool TryRecover(IHttpCallResult response, ref bool replayed)
    {
        if (IsUnthreaded || response.Response is null) return false;
        string? code;
        try { code = AnthropicErrorResponse.Parse(response.Response)?.Error?.Details?.ErrorCode; }
        catch (JsonException) { return false; }
        bool missing = response.Code == HttpStatusCode.NotFound && code == "thread_not_found";
        bool conflict = response.Code == HttpStatusCode.BadRequest && code is "thread_already_continued" or "thread_continue_in_flight";
        if (continueUnthreaded && (response.Code == HttpStatusCode.BadRequest && code == "thread_unsupported_request"
            || missing && PreviousMessageId is not null && replayedLastTurn))
        {
            IsUnthreaded = true;
            PreviousMessageId = null;
            return true;
        }
        if (PreviousMessageId is null || replayed || !(missing || conflict)) return false;
        PreviousMessageId = null;
        replayed = true;
        return true;
    }

    private ChatResult Accept(JObject message, int stagedBeforeSend, bool replayed, HttpCallRequest? request)
    {
        if (message["content"] is not JArray || string.IsNullOrWhiteSpace((string?)message["id"])
            || string.IsNullOrWhiteSpace((string?)message["stop_reason"]))
        {
            PreviousMessageId = null;
            throw new IOException("Anthropic returned an incomplete message envelope.");
        }
        string raw = message.ToString(Formatting.None);
        ChatResult result = provider.InboundMessage<ChatResult>(raw, null, null)
            ?? throw new IOException("Unable to decode the Anthropic message.");
        result.RawResponse = raw;
        result.Ok = true;
        result.Request = request;
        hasResponse = true;
        if ((string?)message["stop_reason"] == "refusal")
        {
            Refuse(stagedBeforeSend);
            return result;
        }
        foreach (JToken input in pending) history.Add(input.DeepClone());
        pending.Clear();
        history.Add(new JObject { ["role"] = "assistant", ["content"] = message["content"]!.DeepClone() });
        bool fallback = (message["usage"]?["iterations"] as JArray)?.Any(x => (string?)x["type"] == "fallback_message") == true;
        PreviousMessageId = IsUnthreaded || fallback ? null : (string?)message["id"];
        paused = (string?)message["stop_reason"] == "pause_turn";
        replayedLastTurn = replayed;
        LastMessage = result;
        return result;
    }

    private void Refuse(int stagedBeforeSend)
    {
        while (pending.Count > stagedBeforeSend) pending.RemoveAt(pending.Count - 1);
        PreviousMessageId = null;
        paused = false;
        replayedLastTurn = false;
    }

    private void KeepInterrupted(AnthropicThreadStreamAccumulator accumulator, int stagedBeforeSend)
    {
        PreviousMessageId = null;
        paused = false;
        if ((string?)accumulator.Message?["stop_reason"] == "refusal")
        {
            Refuse(stagedBeforeSend);
            return;
        }
        JArray partial = accumulator.PartialContent();
        if (partial.Count == 0) return;
        interruptedHistoryCount = history.Count;
        interruptedMessages = (JArray)pending.DeepClone();
        foreach (JToken input in pending) history.Add(input.DeepClone());
        pending.Clear();
        history.Add(new JObject { ["role"] = "assistant", ["content"] = partial });
    }

    private void Enter()
    {
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
            throw new InvalidOperationException("The thread is in use. Finish or dispose its current operation first.");
    }
    private void Exit() => Volatile.Write(ref busy, 0);
    private T WithLock<T>(Func<T> action)
    {
        Enter();
        try { return action(); }
        finally { Exit(); }
    }
}
