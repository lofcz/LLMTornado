using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace LlmTornado.Models.Vendors.TypeSafe;

internal class VendorTypeSafeRetrievedModelsResult
{
    internal class VendorTypeSafeRetrievedModelsResultModel
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        [JsonProperty("release_date")]
        public string? ReleaseDate { get; set; }
    }
    
    [JsonProperty("models")]
    public List<VendorTypeSafeRetrievedModelsResultModel> Models { get; set; }
    
    public RetrievedModelsResult ToResult(string? postData)
    {
        return new RetrievedModelsResult
        {
            Data = Models.Select(x => new RetrievedModel
            {
                Id = x.Name ?? string.Empty,
                InternalName = x.Name,
                InternalDescription = x.Description
            }).ToList(),
            Obj = "model"
        };
    }
}
