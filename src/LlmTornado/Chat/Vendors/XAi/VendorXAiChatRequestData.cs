using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LlmTornado.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Chat.Vendors.XAi;

internal class VendorXAiChatRequest
{
    public VendorXAiChatRequestData? ExtendedRequest { get; set; }
    public ChatRequest? NativeRequest { get; set; }
    
    [JsonIgnore]
    public ChatRequest SourceRequest { get; set; }
    
    public JObject Serialize(JsonSerializerSettings settings)
    {
        JsonSerializer serializer = JsonSerializer.CreateDefault(settings);
        JObject jsonPayload = JObject.FromObject(ExtendedRequest ?? NativeRequest, serializer);
        return jsonPayload;
    }
    
    public VendorXAiChatRequest(ChatRequest request, IEndpointProvider provider)
    {
        SourceRequest = request;
        ChatRequestVendorXAiExtensions? extensions = request.VendorExtensions?.XAi;

        if (request.WebSearchOptions is not null)
        {
            ExtendedRequest ??= new VendorXAiChatRequestData(request);

            ExtendedRequest.SearchParameters = new VendorXAiChatRequestDataSearchParameters
            {
                Mode = "auto",
                Sources = [
                    new VendorXAiChatRequestDataSearchParametersSource
                    {
                        Type = "news",
                        Country = request.WebSearchOptions.UserLocation?.Country
                    }
                ]
            };

            ExtendedRequest.WebSearchOptions = null;
        }
        
        if (extensions is not null)
        {
            ExtendedRequest ??= new VendorXAiChatRequestData(request);
            
            if (extensions.SearchParameters is not null)
            {
                ExtendedRequest.SearchParameters = new VendorXAiChatRequestDataSearchParameters
                {
                    FromDate = extensions.SearchParameters.FromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ToDate = extensions.SearchParameters.ToDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Mode = extensions.SearchParameters.Mode switch
                    {
                        ChatRequestVendorXAiExtensionsSearchParametersModes.Auto => "auto",
                        ChatRequestVendorXAiExtensionsSearchParametersModes.On => "on",
                        ChatRequestVendorXAiExtensionsSearchParametersModes.Off => "off",
                        _ => null
                    },
                    MaxSearchResults = extensions.SearchParameters.MaxSearchResults,
                    ReturnCitations = extensions.SearchParameters.ReturnCitations,
                    Sources = extensions.SearchParameters.Sources?.Select(x =>
                    {
                        VendorXAiChatRequestDataSearchParametersSource source = new VendorXAiChatRequestDataSearchParametersSource();
                        
                        switch (x)
                        {
                            case ChatRequestVendorXAiExtensionsSearchParametersSourceWeb web:
                            {
                                source.Type = "web";
                                source.Country = web.Country;
                                source.ExcludedWebsites = web.ExcludedWebsites;
                                source.SafeSearch = web.SafeSearch;
                                break;
                            }
                            case ChatRequestVendorXAiExtensionsSearchParametersSourceX xSource:
                            {
                                source.Type = "x";
                                source.XHandles = xSource.XHandles;
                                break;
                            }
                            case ChatRequestVendorXAiExtensionsSearchParametersSourceRss rss:
                            {
                                source.Type = "rss";
                                source.Links = rss.Links;
                                break;
                            }
                            case ChatRequestVendorXAiExtensionsSearchParametersSourceNews news:
                            {
                                source.Type = "news";
                                source.Country = news.Country;
                                source.SafeSearch = news.SafeSearch;
                                break;
                            }
                        } 

                        return source;
                    }).ToList()
                };
            }
            
            if (extensions.UseEncryptedContent is not null)
            {
                ExtendedRequest.UseEncryptedContent = extensions.UseEncryptedContent;
            }
            
            if (extensions.PreviousResponseId is not null)
            {
                ExtendedRequest.PreviousResponseId = extensions.PreviousResponseId;
            }
        }
        else if (request.Store is not null)
        {
            // Store needs remapping even without vendor extensions
            ExtendedRequest = new VendorXAiChatRequestData(request);
        }
        else
        {
            NativeRequest = request;
        }
    }
}


/// <summary>
/// https://docs.x.ai/docs/api-reference#chat-completions
/// </summary>
internal class VendorXAiChatRequestData : ChatRequest
{
    [JsonProperty("search_parameters")]
    internal VendorXAiChatRequestDataSearchParameters? SearchParameters { get; set; }
    
    /// <summary>
    /// Whether to store request and responses. Maps from <see cref="ChatRequest.Store"/>.
    /// xAI uses "store_messages" instead of OpenAI's "store".
    /// </summary>
    [JsonProperty("store_messages")]
    internal bool? StoreMessages { get; set; }
    
    /// <summary>
    /// Whether to use encrypted thinking for thinking trace rehydration.
    /// </summary>
    [JsonProperty("use_encrypted_content")]
    internal bool? UseEncryptedContent { get; set; }
    
    /// <summary>
    /// Previous response id. The messages from this response must be chained.
    /// </summary>
    [JsonProperty("previous_response_id")]
    internal string? PreviousResponseId { get; set; }
    
    public VendorXAiChatRequestData(ChatRequest request) : base(request)
    {
        // Map Store -> store_messages (xAI uses a different field name)
        if (request.Store is not null)
        {
            StoreMessages = request.Store;
            Store = null;
        }
    }
}

internal class VendorXAiChatRequestDataSearchParameters
{
    /// <summary>
    /// Date from which to consider the results in ISO-8601 YYYY-MM-DD. See https://en.wikipedia.org/wiki/ISO_8601.
    /// </summary>
    [JsonProperty("from_date")]
    public string? FromDate { get; set; }
    
    /// <summary>
    /// Maximum number of search results to use. (default 15, min 1, max 30)
    /// </summary>
    [JsonProperty("max_search_results")]
    public int? MaxSearchResults { get; set; }
    
    /// <summary>
    /// Choose the mode to query realtime data:<br/>
    /// off (default): no search performed and no external will be considered.<br/>
    /// on: the model will search in every sources for relevant data.<br/>
    /// auto: the model choose whether to search data or not and where to search the data.
    /// </summary>
    [JsonProperty("mode")]
    public string? Mode { get; set; }
    
    /// <summary>
    /// Whether to return citations in the response or not.
    /// </summary>
    [JsonProperty("return_citations")]
    public bool? ReturnCitations { get; set; }
    
    /// <summary>
    /// List of sources to search in. If no sources specified, the model will look over the web and X by default.
    /// </summary>
    [JsonProperty("sources")]
    public List<VendorXAiChatRequestDataSearchParametersSource>? Sources { get; set; }
    
    /// <summary>
    /// Date up to which to consider the results in ISO-8601 YYYY-MM-DD. See https://en.wikipedia.org/wiki/ISO_8601.
    /// </summary>
    [JsonProperty("to_date")]
    public string? ToDate { get; set; }
}

internal class VendorXAiChatRequestDataSearchParametersSource
{
    /// <summary>
    /// x | web | news | rss
    /// </summary>
    [JsonProperty("type")]
    public string? Type { get; set; }
    
    /// <summary>
    /// X Handles of the users from whom to consider the posts. Only available if mode is auto, on or x.
    /// </summary>
    [JsonProperty("x_handles")]
    public List<string>? XHandles { get; set; }
    
    /// <summary>
    /// ISO alpha-2 code of the country. If the country is set, only data coming from this country will be considered. See https://en.wikipedia.org/wiki/ISO_3166-2.
    /// </summary>
    [JsonProperty("country")]
    public string? Country { get; set; }
    
    /// <summary>
    /// List of website to exclude from the search results without protocol specification or subdomains. A maximum of 5 websites can be excluded.
    /// </summary>
    [JsonProperty("excluded_websites")]
    public List<string>? ExcludedWebsites { get; set; }
    
    /// <summary>
    /// If set to true, mature content won't be considered during the search. Default to true.
    /// </summary>
    [JsonProperty("safe_search")]
    public bool? SafeSearch { get; set; }
    
    /// <summary>
    /// Links of the RSS feeds.
    /// </summary>
    [JsonProperty("links")]
    public List<string>? Links { get; set; }
}