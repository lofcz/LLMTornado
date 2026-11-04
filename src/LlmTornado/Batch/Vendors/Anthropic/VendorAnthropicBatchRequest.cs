using System.Collections.Generic;
using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Vendors.Anthropic;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Batch.Vendors.Anthropic;

/// <summary>
/// Anthropic-specific batch request item.
/// </summary>
internal class VendorAnthropicBatchRequestItem
{
    [JsonProperty("custom_id")]
    public string CustomId { get; set; } = string.Empty;
    
    [JsonProperty("params")]
    public VendorAnthropicChatRequest Params { get; set; }
    
    public VendorAnthropicBatchRequestItem(BatchRequestItem item, IEndpointProvider provider)
    {
        CustomId = item.CustomId;
        Params = new VendorAnthropicChatRequest(item.Params, provider);
    }
}

/// <summary>
/// Anthropic-specific batch request.
/// </summary>
internal class VendorAnthropicBatchRequest
{
    [JsonProperty("requests")]
    public List<VendorAnthropicBatchRequestItem> Requests { get; set; } = [];
    
    public VendorAnthropicBatchRequest(BatchRequest request, IEndpointProvider provider)
    {
        foreach (BatchRequestItem item in request.Requests)
        {
            if (item.Params.VendorExtensions?.Anthropic?.Thread is not null)
                throw new System.ArgumentException("Message threads are not supported in Anthropic batches.");
            string? itemProfile = item.Params.VendorExtensions?.Anthropic?.UserProfileId;
            if (itemProfile is not null && itemProfile != request.VendorExtensions?.Anthropic?.UserProfileId)
                throw new System.ArgumentException("Set one UserProfileId on the batch; individual items cannot use different profiles.");
            Requests.Add(new VendorAnthropicBatchRequestItem(item, provider));
        }
    }
    
    /// <summary>
    /// Serializes the request to JSON.
    /// </summary>
    public string Serialize()
    {
        return JsonConvert.SerializeObject(this, EndpointBase.NullSettings);
    }
}
