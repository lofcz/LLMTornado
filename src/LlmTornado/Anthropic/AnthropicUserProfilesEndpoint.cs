using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Anthropic;

/// <summary>Registers and manages end-customer profiles. Uses ordinary API credentials, without a user-profiles beta header.</summary>
public sealed class AnthropicUserProfilesEndpoint : AnthropicResourceEndpoint
{
    internal AnthropicUserProfilesEndpoint(TornadoApi api) : base(api) { }
    /// <inheritdoc />
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.AnthropicUserProfiles;
    /// <summary>Creates a profile. Repeating this call creates another profile.</summary>
    public Task<HttpCallResult<AnthropicUserProfile>> Create(AnthropicUserProfileCreateRequest request, CancellationToken cancellationToken = default)
        => Call<AnthropicUserProfile>("", HttpVerbs.Post, request, cancellationToken: cancellationToken);
    /// <summary>Retrieves the current profile and its open enforcement actions.</summary>
    public Task<HttpCallResult<AnthropicUserProfile>> Get(string profileId, CancellationToken cancellationToken = default)
        => Call<AnthropicUserProfile>("/" + ResourceId(profileId), HttpVerbs.Get, cancellationToken: cancellationToken);
    /// <summary>Lists profiles. Request subsequent pages even when a filtered page is empty.</summary>
    public Task<HttpCallResult<AnthropicPage<AnthropicUserProfile>>> List(AnthropicUserProfileListQuery? query = null, CancellationToken cancellationToken = default)
        => Call<AnthropicPage<AnthropicUserProfile>>("", HttpVerbs.Get, query: query, cancellationToken: cancellationToken);
    /// <summary>Updates supplied fields and merges metadata.</summary>
    public Task<HttpCallResult<AnthropicUserProfile>> Update(string profileId, AnthropicUserProfileUpdateRequest request, CancellationToken cancellationToken = default)
        => Call<AnthropicUserProfile>("/" + ResourceId(profileId), HttpVerbs.Post, request, cancellationToken: cancellationToken);
    /// <summary>Deletes the profile. Open actions end without resolved webhook events.</summary>
    public Task<HttpCallResult<AnthropicDeletedResource>> Delete(string profileId, CancellationToken cancellationToken = default)
        => Call<AnthropicDeletedResource>("/" + ResourceId(profileId), HttpVerbs.Delete, cancellationToken: cancellationToken);
}
