using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Batch.Vendors.Anthropic;

/// <summary>
/// Handles all batch operations for Anthropic.
/// </summary>
internal static class VendorAnthropicBatchHandler
{
    public static async Task<HttpCallResult<BatchItem>> Create(BatchRequest request, IEndpointProvider provider, EndpointBase endpoint, CancellationToken cancellationToken)
    {
        VendorAnthropicBatchRequest anthropicRequest = new VendorAnthropicBatchRequest(request, provider);
        string body = anthropicRequest.Serialize();
        
        return await endpoint.HttpPost<BatchItem>(provider, CapabilityEndpoints.Batch, postData: body, requestObj: request, ct: cancellationToken).ConfigureAwait(false);
    }
    
    public static async IAsyncEnumerable<BatchResult> StreamResults(BatchItem batch, IEndpointProvider provider, EndpointBase endpoint, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(batch.ResultsUrl))
        {
            yield break;
        }
        
        StreamResponse? response = await endpoint.HttpGetStream(provider, CapabilityEndpoints.Batch, batch.ResultsUrl, ct: cancellationToken).ConfigureAwait(false);
        
        if (response?.Stream is null)
        {
            yield break;
        }

        try
        {
            using StreamReader reader = new StreamReader(response.Stream);
            
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }
                
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                
                BatchResult? result = VendorAnthropicBatchResult.Deserialize(line);
                if (result is not null)
                {
                    yield return result;
                }
            }
        }
        finally
        {
            response.Response?.Dispose();
        }
    }
    
    public static async Task<HttpCallResult<bool>> Delete(string batchId, IEndpointProvider provider, EndpointBase endpoint, CancellationToken cancellationToken)
    {
        string url = endpoint.GetUrl(provider, $"/{batchId}");
        HttpCallResult<DeletedBatch> result = await endpoint.HttpDelete<DeletedBatch>(provider, CapabilityEndpoints.Batch, url, ct: cancellationToken).ConfigureAwait(false);
        return new HttpCallResult<bool>(result.Code, result.Response, result.Data?.Id is not null, result.Ok, result.Request);
    }
}
