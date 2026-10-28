using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Files;
using Newtonsoft.Json;

namespace LlmTornado.Common;

/// <summary>
/// Shared type for OpenAI Assistants, used in various sub-APIs.
/// </summary>
public sealed class ListQuery
{
    /// <summary>
    ///     List Query.
    /// </summary>
    /// <param name="limit">
    ///     A limit on the number of objects to be returned.
    ///     Limit can range between 1 and 100, and the default is 20.
    /// </param>
    /// <param name="order">
    ///     Sort order by the 'created_at' timestamp of the objects.
    /// </param>
    /// <param name="after">
    ///     A cursor for use in pagination.
    ///     after is an object ID that defines your place in the list.
    ///     For instance, if you make a list request and receive 100 objects, ending with obj_foo,
    ///     your subsequent call can include after=obj_foo in order to fetch the next page of the list.
    /// </param>
    /// <param name="before">
    ///     A cursor for use in pagination. before is an object ID that defines your place in the list.
    ///     For instance, if you make a list request and receive 100 objects, ending with obj_foo,
    ///     your subsequent call can include before=obj_foo in order to fetch the previous page of the list.
    /// </param>
    public ListQuery(int? limit = null, SortOrder order = SortOrder.Descending, string? after = null, string? before = null)
    {
        Limit = limit;
        Order = order;
        After = after;
        Before = before;
    }

    /// <summary>
    ///     A limit on the number of objects to be returned. Limit can range between 1 and 100, and the default is 20.
    /// </summary>
    [JsonProperty("limit")]
    public int? Limit { get; set; }

    /// <summary>
    ///     Sort order by the 'created_at' timestamp of the objects.
    /// </summary>
    [JsonProperty("order")]
    public SortOrder? Order { get; set; }

    /// <summary>
    ///     A cursor for use in pagination.
    ///     after is an object ID that defines your place in the list.
    ///     For instance, if you make a list request and receive 100 objects, ending with obj_foo,
    ///     your subsequent call can include after=obj_foo in order to fetch the next page of the list.
    /// </summary>
    [JsonProperty("after")]
    public string? After { get; set; }

    /// <summary>
    ///     A cursor for use in pagination. before is an object ID that defines your place in the list.
    ///     For instance, if you make a list request and receive 100 objects, ending with obj_foo,
    ///     your subsequent call can include before=obj_foo in order to fetch the previous page of the list.
    /// </summary>
    [JsonProperty("before")]
    public string? Before { get; set; }

    /// <summary>
    /// Used by Google for cursor paging.
    /// </summary>
    [JsonIgnore]
    public string? PageToken { get; set; }

    /// <summary>
    /// File purpose filter. Required by MiniMax when listing files.
    /// </summary>
    [JsonIgnore]
    public FilePurpose? Purpose { get; set; }

    /// <summary>
    /// Transforms the <see cref="ListQuery"/> into a series of <see cref="Uri" /> query parameters.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public Dictionary<string, object>? ToQueryParams(IEndpointProvider provider)
    {
        return ToQueryParams(provider.Provider, this);
    }
    
    /// <summary>
    /// Transforms the <see cref="ListQuery"/> into a series of <see cref="Uri" /> query parameters.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public Dictionary<string, object>? ToQueryParams(LLmProviders provider)
    {
        return ToQueryParams(provider, this);
    }
    
    /// <summary>
    /// Transforms the <see cref="ListQuery"/> into a series of <see cref="Uri" /> query parameters.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static Dictionary<string, object>? ToQueryParams(LLmProviders provider, ListQuery? query)
    {
        if (query is null)
        {
            return null;
        }

        Dictionary<string, object> parameters = [];

        switch (provider)
        {
            case LLmProviders.Google:
            {
                if (query.Limit is not null)
                {
                    parameters["pageSize"] = query.Limit;
                }

                if (query.PageToken is not null)
                {
                    parameters["pageToken"] = query.PageToken;
                }

                return parameters;
            }
            case LLmProviders.Anthropic:
            {
                if (query.Limit is not null)
                {
                    parameters["limit"] = query.Limit.Value.Clamp(1, 1_000); // https://docs.anthropic.com/en/api/files-list#parameter-limit
                }
            
                string? afterId = !string.IsNullOrEmpty(query.After) ? query.After : query.PageToken;

                if (!string.IsNullOrEmpty(afterId))
                {
                    parameters["after_id"] = afterId;
                }

                if (!string.IsNullOrEmpty(query.Before))
                {
                    parameters["before_id"] = query.Before;
                }

                return parameters;
            }
            case LLmProviders.MiniMax:
            {
                if (query.Purpose is not null)
                {
                    parameters["purpose"] = query.Purpose.Value switch
                    {
                        FilePurpose.VoiceClone => "voice_clone",
                        FilePurpose.PromptAudio => "prompt_audio",
                        FilePurpose.TextToAudioAsyncInput => "t2a_async_input",
                        _ => "voice_clone"
                    };
                }

                return parameters;
            }
        }

        if (query.Limit is not null)
        {
            parameters.Add("limit", query.Limit);
        }

        switch (query.Order)
        {
            case SortOrder.Descending:
            {
                parameters.Add("order", "desc");
                break;
            }
            case SortOrder.Ascending:
            {
                parameters.Add("order", "asc");
                break;
            }
        }

        if (!string.IsNullOrEmpty(query.After))
        {
            parameters.Add("after", query.After);
        }

        if (!string.IsNullOrEmpty(query.Before))
        {
            parameters.Add("before", query.Before);
        }

        return parameters;
    }
}