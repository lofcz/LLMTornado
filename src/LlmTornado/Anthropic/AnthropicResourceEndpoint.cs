using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Anthropic;

/// <summary>Transport shared by native Anthropic resource APIs.</summary>
public abstract class AnthropicResourceEndpoint : EndpointBase
{
    internal AnthropicResourceEndpoint(TornadoApi api) : base(api) { }

    /// <summary>Required feature header, if this resource is in beta.</summary>
    protected virtual string? BetaHeader => null;

    internal static string ResourceId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A resource ID is required.", nameof(id));
        return Uri.EscapeDataString(id);
    }

    internal static string WithQuery(string url, object? query)
    {
        if (query is null) return url;
        JObject fields = JObject.FromObject(query, JsonSerializer.Create(NullSettings));
        List<string> parameters = [];
        foreach (JProperty field in fields.Properties())
        {
            IEnumerable<JToken> values = field.Value is JArray array ? array : new[] { field.Value };
            string name = field.Name + (field.Value is JArray ? "[]" : "");
            foreach (JToken value in values)
            {
                if (value.Type == JTokenType.Null) continue;
                string text = value.Type == JTokenType.Boolean ? (value.Value<bool>() ? "true" : "false")
                    : value is JValue scalar ? Convert.ToString(scalar.Value, CultureInfo.InvariantCulture) ?? string.Empty : value.ToString(Formatting.None);
                parameters.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(text)}");
            }
        }
        return parameters.Count == 0 ? url : url + (url.Contains("?") ? "&" : "?") + string.Join("&", parameters);
    }

    /// <summary>Calls a resource while preserving HTTP errors, response headers and cancellation.</summary>
    private protected Task<HttpCallResult<T>> Call<T>(string path, HttpVerbs verb, object? body = null, object? query = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.GetProvider(LLmProviders.Anthropic);
        string url = WithQuery(provider.ApiUrl(Endpoint, path), query);
        return HttpRequestRaw<T>(provider, Endpoint, url, verb: verb,
            postData: body is null ? null : JsonConvert.SerializeObject(body, NullSettings),
            ct: cancellationToken, headers: BetaHeader is null ? null : new Dictionary<string, object?> { ["anthropic-beta"] = BetaHeader });
    }
}

/// <summary>Native page cursor. An empty data array does not imply the end when next_page is set.</summary>
public class AnthropicPage<T>
{
    /// <summary>Resources on this page.</summary>
    [JsonProperty("data")]
    public List<T> Data { get; set; } = [];
    /// <summary>Opaque continuation cursor; null marks the end.</summary>
    [JsonProperty("next_page")]
    public string? NextPage { get; set; }
    /// <summary>Whether more rows exist, when returned by the endpoint.</summary>
    [JsonProperty("has_more")]
    public bool? HasMore { get; set; }
    /// <summary>Time when analytics data was last refreshed.</summary>
    [JsonProperty("data_refreshed_at")]
    public string? DataRefreshedAt { get; set; }
}

/// <summary>Native cursor pagination.</summary>
public class AnthropicPageQuery
{
    /// <summary>Opaque next_page from the preceding response.</summary>
    [JsonProperty("page")]
    public string? Page { get; set; }
    /// <summary>Maximum number of rows per page.</summary>
    [JsonProperty("limit")]
    public int? Limit { get; set; }
}

/// <summary>Confirmation of resource deletion.</summary>
public class AnthropicDeletedResource
{
    /// <summary>Deleted resource ID.</summary>
    [JsonProperty("id")]
    public string? Id { get; set; }
    /// <summary>Native deletion discriminator.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }
    /// <summary>Deletion flag, on endpoints which return one.</summary>
    [JsonProperty("deleted")]
    public bool? Deleted { get; set; }
}
