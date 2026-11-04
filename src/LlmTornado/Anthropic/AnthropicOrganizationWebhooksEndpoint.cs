using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Anthropic;

/// <summary>Organization enforcement webhook registration. Requires an authorized organization credential.</summary>
public sealed class AnthropicOrganizationWebhooksEndpoint : AnthropicResourceEndpoint
{
    internal AnthropicOrganizationWebhooksEndpoint(TornadoApi api) : base(api) { }
    /// <inheritdoc />
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.AnthropicOrganizationWebhooks;
    /// <inheritdoc />
    protected override string BetaHeader => "webhooks-2026-10-07";
    /// <summary>Lists webhook endpoints using ID cursors.</summary>
    public Task<HttpCallResult<AnthropicOrganizationWebhookList>> List(AnthropicOrganizationWebhookListQuery? query = null, CancellationToken cancellationToken = default)
        => Call<AnthropicOrganizationWebhookList>("", HttpVerbs.Get, query: query, cancellationToken: cancellationToken);
    /// <summary>Creates an endpoint and returns its signing secret once.</summary>
    public Task<HttpCallResult<AnthropicOrganizationWebhook>> Create(AnthropicOrganizationWebhookCreateRequest request, CancellationToken cancellationToken = default)
        => Call<AnthropicOrganizationWebhook>("", HttpVerbs.Post, request, cancellationToken: cancellationToken);
    /// <summary>Retrieves an endpoint without its signing secret.</summary>
    public Task<HttpCallResult<AnthropicOrganizationWebhook>> Get(string endpointId, CancellationToken cancellationToken = default)
        => Call<AnthropicOrganizationWebhook>("/" + ResourceId(endpointId), HttpVerbs.Get, cancellationToken: cancellationToken);
    /// <summary>Updates supplied endpoint settings.</summary>
    public Task<HttpCallResult<AnthropicOrganizationWebhook>> Update(string endpointId, AnthropicOrganizationWebhookUpdateRequest request, CancellationToken cancellationToken = default)
        => Call<AnthropicOrganizationWebhook>("/" + ResourceId(endpointId), HttpVerbs.Post, request, cancellationToken: cancellationToken);
    /// <summary>Deletes an endpoint.</summary>
    public Task<HttpCallResult<AnthropicDeletedResource>> Delete(string endpointId, CancellationToken cancellationToken = default)
        => Call<AnthropicDeletedResource>("/" + ResourceId(endpointId), HttpVerbs.Delete, cancellationToken: cancellationToken);
    /// <summary>Replaces the signing secret immediately; the old secret stops working.</summary>
    public Task<HttpCallResult<AnthropicWebhookSigningSecret>> RegenerateSigningSecret(string endpointId, CancellationToken cancellationToken = default)
        => Call<AnthropicWebhookSigningSecret>("/" + ResourceId(endpointId) + "/regenerate_signing_secret", HttpVerbs.Post, cancellationToken: cancellationToken);
}
