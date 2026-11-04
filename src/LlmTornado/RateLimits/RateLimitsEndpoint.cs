using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.RateLimits;

/// <summary>
/// Anthropic Admin API for querying organization and workspace rate limits.
/// Requires an Admin API key (<c>sk-ant-admin...</c>).
/// </summary>
public class RateLimitsEndpoint : EndpointBase
{
    internal RateLimitsEndpoint(TornadoApi api) : base(api)
    {
    }

    /// <inheritdoc />
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.RateLimits;

    /// <summary>
    /// Lists Messages API rate limits configured for the organization.
    /// </summary>
    /// <param name="query">Optional filters (<see cref="AnthropicRateLimitsListQuery.GroupType"/>, <see cref="AnthropicRateLimitsListQuery.Model"/>, pagination).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HttpCallResult<AnthropicRateLimitsListResponse>> ListOrganizationRateLimits(
        AnthropicRateLimitsListQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.GetProvider(LLmProviders.Anthropic);
        return HttpGet<AnthropicRateLimitsListResponse>(
            provider,
            Endpoint,
            queryParams: query?.ToQueryParams(includeModelFilter: true),
            ct: cancellationToken);
    }

    /// <summary>
    /// Lists workspace-level rate limit overrides for a workspace.
    /// Only groups with overrides are returned; use <see cref="ListOrganizationRateLimits"/> for inherited limits.
    /// </summary>
    /// <param name="workspaceId">Workspace id (for example, <c>wrkspc_01JwQvzr7rXLA5AGx3HKfFUJ</c>).</param>
    /// <param name="query">Optional filters (<see cref="AnthropicRateLimitsListQuery.GroupType"/>, pagination). Model filter is not supported on this endpoint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HttpCallResult<AnthropicRateLimitsListResponse>> ListWorkspaceRateLimits(
        string workspaceId,
        AnthropicRateLimitsListQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new System.ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        IEndpointProvider provider = Api.GetProvider(LLmProviders.Anthropic);
        return HttpGet<AnthropicRateLimitsListResponse>(
            provider,
            Endpoint,
            url: provider.ApiUrl(Endpoint, System.Uri.EscapeDataString(workspaceId)),
            queryParams: query?.ToQueryParams(includeModelFilter: false),
            ct: cancellationToken);
    }
}
