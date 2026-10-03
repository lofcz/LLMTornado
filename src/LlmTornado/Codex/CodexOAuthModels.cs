using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.ChatFunctions;
using LlmTornado.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Codex;

/// <summary>
/// Settings for direct ChatGPT subscription authentication and Codex requests.
/// </summary>
public sealed class CodexOAuthOptions
{
    /// <summary>
    /// Codex protocol version used when the caller does not provide an override.
    /// </summary>
    public const string DefaultCodexProtocolVersion = "0.160.0";

    /// <summary>
    /// OpenAI OAuth issuer.
    /// </summary>
    public Uri Issuer { get; set; } = new Uri("https://auth.openai.com");

    /// <summary>
    /// ChatGPT Codex backend base URI.
    /// </summary>
    public Uri ApiBaseUri { get; set; } = new Uri("https://chatgpt.com/backend-api/codex/");

    /// <summary>
    /// Public OAuth client identifier used by the official Codex applications.
    /// </summary>
    public string ClientId { get; set; } = "app_EMoamEEZ73f0CkXaXp7hrann";

    /// <summary>
    /// Preferred localhost callback port. The official Codex redirect allow-list uses 1455.
    /// Set to zero only for controlled test environments.
    /// </summary>
    public int CallbackPort { get; set; } = 1455;

    /// <summary>
    /// Fallback localhost callback port. The official Codex redirect allow-list uses 1457.
    /// </summary>
    public int FallbackCallbackPort { get; set; } = 1457;

    /// <summary>
    /// Client identifier sent in Codex request headers and the OAuth authorization request.
    /// </summary>
    public string Originator { get; set; } = "llmtornado";

    /// <summary>
    /// Client version sent in Codex backend headers and the user agent.
    /// </summary>
    public string? ClientVersion { get; set; }

    /// <summary>
    /// Codex protocol version sent as <c>client_version</c> to the subscription model catalog.
    /// This is independent of the LLMTornado package version.
    /// </summary>
    public string CodexProtocolVersion { get; set; } = DefaultCodexProtocolVersion;

    /// <summary>
    /// How early an access token is refreshed before its expiry.
    /// </summary>
    public TimeSpan RefreshBeforeExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Persistent credential store. Defaults to a file in the current user's local application data.
    /// </summary>
    public ICodexOAuthCredentialStore CredentialStore { get; set; } = new CodexOAuthFileCredentialStore();

    /// <summary>
    /// Optional HTTP client used for OAuth and Codex backend requests. The caller retains ownership.
    /// </summary>
    public HttpClient? HttpClient { get; set; }
}

/// <summary>
/// Persists direct Codex OAuth credentials, including rotating refresh tokens.
/// </summary>
public interface ICodexOAuthCredentialStore
{
    /// <summary>
    /// Loads the current credentials, or null when signed out.
    /// </summary>
    Task<CodexOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the stored credentials after login or token refresh.
    /// </summary>
    Task SaveAsync(CodexOAuthCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes stored credentials.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// File-backed direct Codex OAuth credential store.
/// </summary>
public sealed class CodexOAuthFileCredentialStore : ICodexOAuthCredentialStore
{
    private readonly SemaphoreSlim fileLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Creates a credential store at the supplied path or the default per-user path.
    /// </summary>
    public CodexOAuthFileCredentialStore(string? filePath = null)
    {
        FilePath = filePath ?? GetDefaultPath();
    }

    /// <summary>
    /// File containing the OAuth credentials. Treat this file as a secret.
    /// </summary>
    public string FilePath { get; }

    /// <inheritdoc />
    public async Task<CodexOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            string json = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<CodexOAuthCredentials>(json);
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(CodexOAuthCredentials credentials, CancellationToken cancellationToken = default)
    {
        if (credentials is null)
        {
            throw new ArgumentNullException(nameof(credentials));
        }

        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonConvert.SerializeObject(credentials, Formatting.Indented));
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        finally
        {
            fileLock.Release();
        }
    }

    private static string GetDefaultPath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "LlmTornado", "codex-oauth.json");
    }
}

/// <summary>
/// In-memory credential store for applications that provide their own persistence lifecycle.
/// </summary>
public sealed class CodexOAuthMemoryCredentialStore : ICodexOAuthCredentialStore
{
    private CodexOAuthCredentials? credentials;

    /// <summary>
    /// Creates an empty store or seeds it with existing credentials.
    /// </summary>
    public CodexOAuthMemoryCredentialStore(CodexOAuthCredentials? credentials = null)
    {
        this.credentials = credentials?.Clone();
    }

    /// <inheritdoc />
    public Task<CodexOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(credentials?.Clone());
    }

    /// <inheritdoc />
    public Task SaveAsync(CodexOAuthCredentials credentials, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.credentials = credentials.Clone();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        credentials = null;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Direct ChatGPT OAuth credentials. Access and refresh token values are secrets.
/// </summary>
public sealed class CodexOAuthCredentials
{
    /// <summary>
    /// OpenID identity token.
    /// </summary>
    [JsonProperty("id_token")]
    public string IdToken { get; set; } = string.Empty;

    /// <summary>
    /// Bearer token used with the ChatGPT Codex backend.
    /// </summary>
    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Rotating OAuth refresh token.
    /// </summary>
    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// ChatGPT workspace/account identifier.
    /// </summary>
    [JsonProperty("account_id")]
    public string? AccountId { get; set; }

    /// <summary>
    /// Account email extracted from token claims.
    /// </summary>
    [JsonProperty("email")]
    public string? Email { get; set; }

    /// <summary>
    /// ChatGPT subscription plan extracted from token claims.
    /// </summary>
    [JsonProperty("plan_type")]
    public string? PlanType { get; set; }

    /// <summary>
    /// Whether the account requires FedRAMP routing.
    /// </summary>
    [JsonProperty("is_fedramp")]
    public bool IsFedRamp { get; set; }

    /// <summary>
    /// Access-token expiration when present in the JWT.
    /// </summary>
    [JsonProperty("expires_at_utc")]
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Time at which these credentials were issued or refreshed.
    /// </summary>
    [JsonProperty("last_refresh_utc")]
    public DateTimeOffset LastRefreshUtc { get; set; }

    internal CodexOAuthCredentials Clone()
        => (CodexOAuthCredentials)MemberwiseClone();
}

/// <summary>
/// Result of a direct browser OAuth login.
/// </summary>
public sealed class CodexOAuthLoginResult
{
    internal CodexOAuthLoginResult(bool success, CodexAccount? account, string? error)
    {
        Success = success;
        Account = account;
        Error = error;
    }

    /// <summary>
    /// Whether login and credential persistence completed successfully.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Authenticated ChatGPT account.
    /// </summary>
    public CodexAccount? Account { get; }

    /// <summary>
    /// Error reported by the callback or token exchange.
    /// </summary>
    public string? Error { get; }
}

/// <summary>
/// A running direct browser OAuth login.
/// </summary>
public sealed class CodexOAuthBrowserLogin
{
    private readonly CodexOAuthLoginOperation operation;

    internal CodexOAuthBrowserLogin(CodexOAuthLoginOperation operation)
    {
        this.operation = operation;
    }

    /// <summary>
    /// URL the host application should open in the user's browser.
    /// </summary>
    public Uri AuthorizationUrl => operation.AuthorizationUrl;

    /// <summary>
    /// Local callback port selected for this login.
    /// </summary>
    public int CallbackPort => operation.CallbackPort;

    /// <summary>
    /// Waits for the browser callback and token exchange.
    /// </summary>
    public Task<CodexOAuthLoginResult> WaitAsync(CancellationToken cancellationToken = default)
        => operation.WaitAsync(cancellationToken);

    /// <summary>
    /// Cancels the local callback listener.
    /// </summary>
    public Task CancelAsync()
        => operation.CancelAsync();
}

/// <summary>
/// Options for a direct OAuth-backed Codex text thread.
/// </summary>
public sealed class CodexOAuthThreadOptions
{
    /// <summary>
    /// Model identifier returned by <see cref="CodexOAuthSession.ListModelsAsync"/>.
    /// The default catalog model is selected when omitted.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Optional developer instructions added to the client-managed thread history.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Optional prior conversation items copied into the client-managed thread history.
    /// </summary>
    public IReadOnlyList<CodexOAuthHistoryItem>? InitialHistory { get; set; }
}

/// <summary>
/// A text or function item used to restore a direct OAuth thread's client-managed history.
/// </summary>
public sealed class CodexOAuthHistoryItem
{
    private readonly JObject value;

    private CodexOAuthHistoryItem(JObject value)
    {
        this.value = value;
    }

    /// <summary>
    /// Creates a prior user message.
    /// </summary>
    public static CodexOAuthHistoryItem UserMessage(string text)
        => Message("user", text, "input_text");

    /// <summary>
    /// Creates a prior assistant message.
    /// </summary>
    public static CodexOAuthHistoryItem AssistantMessage(string text)
        => Message("assistant", text, "output_text");

    /// <summary>
    /// Creates a prior function call requested by the assistant.
    /// </summary>
    public static CodexOAuthHistoryItem FunctionCall(
        string callId,
        string name,
        string arguments)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            throw new ArgumentException("Call id cannot be empty.", nameof(callId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Function name cannot be empty.", nameof(name));
        }

        if (arguments is null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        return new CodexOAuthHistoryItem(new JObject
        {
            ["type"] = "function_call",
            ["call_id"] = callId,
            ["name"] = name,
            ["arguments"] = arguments
        });
    }

    /// <summary>
    /// Creates a prior function result linked to its call identifier.
    /// </summary>
    public static CodexOAuthHistoryItem FunctionOutput(string callId, string output)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            throw new ArgumentException("Call id cannot be empty.", nameof(callId));
        }

        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        return new CodexOAuthHistoryItem(new JObject
        {
            ["type"] = "function_call_output",
            ["call_id"] = callId,
            ["output"] = output
        });
    }

    internal JObject ToJson()
        => (JObject)value.DeepClone();

    private static CodexOAuthHistoryItem Message(
        string role,
        string text,
        string contentType)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        return new CodexOAuthHistoryItem(new JObject
        {
            ["type"] = "message",
            ["role"] = role,
            ["content"] = new JArray
            {
                new JObject
                {
                    ["type"] = contentType,
                    ["text"] = text
                }
            }
        });
    }
}

/// <summary>
/// Options for one direct OAuth-backed Codex text turn.
/// </summary>
public sealed class CodexOAuthTurnOptions
{
    /// <summary>
    /// Function tools available for this turn.
    /// </summary>
    public IReadOnlyList<Tool>? Tools { get; set; }

    /// <summary>
    /// Controls whether and which configured tool the model may call.
    /// </summary>
    public OutboundToolChoice? ToolChoice { get; set; }

    /// <summary>
    /// Whether the model may request multiple tool calls in parallel.
    /// </summary>
    public bool? ParallelToolCalls { get; set; }

    /// <summary>
    /// Optional reasoning effort advertised by the selected model.
    /// </summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>
    /// Optional service tier advertised by the selected model.
    /// </summary>
    public string? ServiceTier { get; set; }

    /// <summary>
    /// Receives streamed assistant text deltas.
    /// </summary>
    public Func<CodexOAuthTextDelta, Task>? OnTextDelta { get; set; }
}

/// <summary>
/// A streamed assistant text delta from the direct Codex backend.
/// </summary>
public sealed class CodexOAuthTextDelta
{
    internal CodexOAuthTextDelta(string threadId, string responseId, string itemId, string delta)
    {
        ThreadId = threadId;
        ResponseId = responseId;
        ItemId = itemId;
        Delta = delta;
    }

    /// <summary>
    /// Client-side thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Backend response identifier when available.
    /// </summary>
    public string ResponseId { get; }

    /// <summary>
    /// Output item identifier when available.
    /// </summary>
    public string ItemId { get; }

    /// <summary>
    /// Text appended by this event.
    /// </summary>
    public string Delta { get; }
}

/// <summary>
/// Final result of a direct OAuth-backed Codex text turn.
/// </summary>
public sealed class CodexOAuthTurnResult
{
    internal CodexOAuthTurnResult(
        string threadId,
        string responseId,
        string finalResponse,
        string? status,
        JObject response,
        IReadOnlyList<JObject> outputItems,
        IReadOnlyList<ToolCall> toolCalls)
    {
        ThreadId = threadId;
        ResponseId = responseId;
        FinalResponse = finalResponse;
        Status = status;
        Response = response;
        OutputItems = outputItems;
        ToolCalls = toolCalls;
    }

    /// <summary>
    /// Client-side thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Backend response identifier.
    /// </summary>
    public string ResponseId { get; }

    /// <summary>
    /// Concatenated assistant text.
    /// </summary>
    public string FinalResponse { get; }

    /// <summary>
    /// Final response status.
    /// </summary>
    public string? Status { get; }

    /// <summary>
    /// Raw completed response object.
    /// </summary>
    public JObject Response { get; }

    /// <summary>
    /// Function calls requested by the model in output order.
    /// </summary>
    public IReadOnlyList<ToolCall> ToolCalls { get; }

    internal IReadOnlyList<JObject> OutputItems { get; }
}

/// <summary>
/// Error returned by direct OAuth or ChatGPT Codex backend operations.
/// </summary>
public sealed class CodexOAuthException : Exception
{
    internal CodexOAuthException(string message, int? statusCode = null, string? responseBody = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>
    /// HTTP status code when the error originated from an HTTP response.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Response body when available.
    /// </summary>
    public string? ResponseBody { get; }
}

internal sealed class CodexOAuthTokenResponse
{
    [JsonProperty("id_token")]
    public string? IdToken { get; set; }

    [JsonProperty("access_token")]
    public string? AccessToken { get; set; }

    [JsonProperty("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonProperty("expires_in")]
    public long? ExpiresIn { get; set; }
}

internal sealed class CodexBackendModelsResponse
{
    [JsonProperty("models")]
    public List<JObject> Models { get; set; } = [];
}
