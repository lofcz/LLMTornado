using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Compaction;

/// <summary>
/// Anthropic Compaction API endpoint for server-side context summarization (beta).
/// Uses the Messages API with <c>context_management</c> and the <c>compact-2026-01-12</c> beta header.
/// Only available with the Anthropic provider.
/// </summary>
public class CompactionEndpoint : EndpointBase
{
    /// <summary>
    /// Constructor of the compaction endpoint. Access via <see cref="TornadoApi.Compaction"/>.
    /// </summary>
    internal CompactionEndpoint(TornadoApi api) : base(api)
    {
    }

    /// <inheritdoc />
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Compaction;

    /// <summary>
    /// Sends a compaction-enabled request to the Anthropic Messages API.
    /// </summary>
    public async Task<CompactionResult?> Compact(CompactionRequest request, CancellationToken ct = default)
    {
        HttpCallResult<CompactionResult> result = await CompactSafe(request, ct).ConfigureAwait(false);
        return result.Exception is not null ? throw result.Exception : result.Data;
    }

    /// <summary>
    /// Sends a compaction-enabled request to the Anthropic Messages API.
    /// </summary>
    public async Task<HttpCallResult<CompactionResult>> CompactSafe(CompactionRequest request, CancellationToken ct = default)
    {
        IEndpointProvider provider = Api.GetProvider(request.Model ?? ChatModel.Anthropic.Claude46.Sonnet);
        TornadoRequestContent requestBody = request.Serialize(provider);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, request.CancellationToken);
        return await HttpPost<CompactionResult>(provider, Endpoint, requestBody.Url, requestBody.Body, request.Model, request, linked.Token).ConfigureAwait(false);
    }
}
