using System;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Anthropic;

/// <summary>Claude Enterprise Tag settings and Access bundle attachments. Requires a scoped organization Admin key.</summary>
public sealed class AnthropicClaudeTagEndpoint : AnthropicResourceEndpoint
{
    internal AnthropicClaudeTagEndpoint(TornadoApi api) : base(api) { }
    /// <inheritdoc />
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.AnthropicClaudeTag;
    /// <inheritdoc />
    protected override string BetaHeader => "experimental-claude-tag-2026-10-21";

    /// <summary>Sets up a channel and optionally changes its settings. Uses Slack's own IDs.</summary>
    public Task<HttpCallResult<AnthropicClaudeTagResource>> SetupChannel(AnthropicClaudeTagSetupRequest request, CancellationToken cancellationToken = default)
        => Call<AnthropicClaudeTagResource>("/slack_channels", HttpVerbs.Post, request, cancellationToken: cancellationToken);
    /// <summary>Lists channels in a Slack workspace; an empty page may still have next_page.</summary>
    public Task<HttpCallResult<AnthropicPage<AnthropicClaudeTagResource>>> ListChannels(AnthropicClaudeTagListQuery query, CancellationToken cancellationToken = default)
    {
        if (query is null || string.IsNullOrWhiteSpace(query.SlackWorkspaceId)) throw new ArgumentException("SlackWorkspaceId is required.", nameof(query));
        return Call<AnthropicPage<AnthropicClaudeTagResource>>("/slack_channels", HttpVerbs.Get, query: query, cancellationToken: cancellationToken);
    }
    /// <summary>Lists workspaces; optionally filter by Slack's workspace ID.</summary>
    public Task<HttpCallResult<AnthropicPage<AnthropicClaudeTagResource>>> ListWorkspaces(AnthropicClaudeTagListQuery? query = null, CancellationToken cancellationToken = default)
        => Call<AnthropicPage<AnthropicClaudeTagResource>>("/slack_workspaces", HttpVerbs.Get, query: query, cancellationToken: cancellationToken);

    /// <summary>Retrieves channel settings, optionally resolving inheritance.</summary>
    public Task<HttpCallResult<AnthropicClaudeTagResource>> GetChannel(string id, bool? includeInherited = null, CancellationToken cancellationToken = default)
        => Call<AnthropicClaudeTagResource>("/slack_channels/" + ResourceId(id), HttpVerbs.Get, query: new { include_inherited = includeInherited }, cancellationToken: cancellationToken);
    /// <summary>Changes only explicitly assigned settings; null restores inheritance.</summary>
    public Task<HttpCallResult<AnthropicClaudeTagResource>> UpdateChannel(string id, AnthropicClaudeTagSettingsUpdate settings, CancellationToken cancellationToken = default)
        => Call<AnthropicClaudeTagResource>("/slack_channels/" + ResourceId(id), HttpVerbs.Post, new { settings = settings ?? throw new ArgumentNullException(nameof(settings)) }, cancellationToken: cancellationToken);

    /// <summary>Retrieves workspace settings, optionally resolving inheritance.</summary>
    public Task<HttpCallResult<AnthropicClaudeTagResource>> GetWorkspace(string id, bool? includeInherited = null, CancellationToken cancellationToken = default)
        => Call<AnthropicClaudeTagResource>("/slack_workspaces/" + ResourceId(id), HttpVerbs.Get, query: new { include_inherited = includeInherited }, cancellationToken: cancellationToken);
    /// <summary>Changes only explicitly assigned settings; null restores inheritance.</summary>
    public Task<HttpCallResult<AnthropicClaudeTagResource>> UpdateWorkspace(string id, AnthropicClaudeTagSettingsUpdate settings, CancellationToken cancellationToken = default)
        => Call<AnthropicClaudeTagResource>("/slack_workspaces/" + ResourceId(id), HttpVerbs.Post, new { settings = settings ?? throw new ArgumentNullException(nameof(settings)) }, cancellationToken: cancellationToken);

    /// <summary>Removes the Claude Tag channel configuration and attachments; does not alter the Slack channel.</summary>
    public Task<HttpCallResult<AnthropicDeletedResource>> DeleteChannel(string id, CancellationToken cancellationToken = default)
        => Call<AnthropicDeletedResource>("/slack_channels/" + ResourceId(id), HttpVerbs.Delete, cancellationToken: cancellationToken);
    /// <summary>Lists public metadata of Access bundles.</summary>
    public Task<HttpCallResult<AnthropicPage<AnthropicClaudeTagBundle>>> ListBundles(AnthropicPageQuery? query = null, CancellationToken cancellationToken = default)
        => Call<AnthropicPage<AnthropicClaudeTagBundle>>("/bundles", HttpVerbs.Get, query: query, cancellationToken: cancellationToken);
    /// <summary>Retrieves one bundle's metadata.</summary>
    public Task<HttpCallResult<AnthropicClaudeTagBundle>> GetBundle(string bundleId, CancellationToken cancellationToken = default)
        => Call<AnthropicClaudeTagBundle>("/bundles/" + ResourceId(bundleId), HttpVerbs.Get, cancellationToken: cancellationToken);
    /// <summary>Lists a bundle's channel and workspace attachments.</summary>
    public Task<HttpCallResult<AnthropicPage<AnthropicClaudeTagTarget>>> ListBundleTargets(string bundleId, AnthropicPageQuery? query = null, CancellationToken cancellationToken = default)
        => Call<AnthropicPage<AnthropicClaudeTagTarget>>("/bundles/" + ResourceId(bundleId) + "/targets", HttpVerbs.Get, query: query, cancellationToken: cancellationToken);
    /// <summary>Attaches an existing bundle to a channel or workspace. Repeating the same attachment is safe.</summary>
    public Task<HttpCallResult<AnthropicClaudeTagTarget>> AttachBundle(string bundleId, AnthropicClaudeTagTargetRequest target, CancellationToken cancellationToken = default)
    {
        if (target is null || !(target.Type == "slack_channel" && !string.IsNullOrWhiteSpace(target.SlackChannelId) && target.SlackWorkspaceId is null
            || target.Type == "slack_workspace" && !string.IsNullOrWhiteSpace(target.SlackWorkspaceId) && target.SlackChannelId is null))
            throw new ArgumentException("Specify one matching channel or workspace target.", nameof(target));
        return Call<AnthropicClaudeTagTarget>("/bundles/" + ResourceId(bundleId) + "/targets", HttpVerbs.Post, new { target }, cancellationToken: cancellationToken);
    }
    /// <summary>Retrieves an attachment by its bundle and target IDs.</summary>
    public Task<HttpCallResult<AnthropicClaudeTagTarget>> GetBundleTarget(string bundleId, string targetId, CancellationToken cancellationToken = default)
        => Call<AnthropicClaudeTagTarget>("/bundles/" + ResourceId(bundleId) + "/targets/" + ResourceId(targetId), HttpVerbs.Get, cancellationToken: cancellationToken);
    /// <summary>Detaches the bundle. A repeated detach returns 404.</summary>
    public Task<HttpCallResult<AnthropicDeletedResource>> DetachBundle(string bundleId, string targetId, CancellationToken cancellationToken = default)
        => Call<AnthropicDeletedResource>("/bundles/" + ResourceId(bundleId) + "/targets/" + ResourceId(targetId), HttpVerbs.Delete, cancellationToken: cancellationToken);
}
