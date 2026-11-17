using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Sse;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;
using LlmTornado.Threads;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using LlmTornado.Chat;
using LlmTornado.Responses.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Responses;

/// <summary>
/// OpenAI's most advanced interface for generating model responses. Supports text and image inputs, and text outputs. Create stateful interactions with the model, using the output of previous responses as input. Extend the model's capabilities with built-in tools for file search, web search, computer use, and more. Allow the model access to external systems and data using function calling.
/// </summary>
public class ResponsesEndpoint : EndpointBase
{
    private static readonly FrozenDictionary<string, ResponseEventTypes> EventTypeToEnum = new Dictionary<string, ResponseEventTypes>
    {
        ["response.inject.created"] = ResponseEventTypes.ResponseInjectCreated,
        ["response.inject.failed"] = ResponseEventTypes.ResponseInjectFailed,
        ["response.steer.accepted"] = ResponseEventTypes.ResponseSteerAccepted,
        ["response.steer.pending"] = ResponseEventTypes.ResponseSteerPending,
        ["response.steer.failed"] = ResponseEventTypes.ResponseSteerFailed,
        ["response.web_search_call.searching"] = ResponseEventTypes.ResponseWebSearchCallSearching,
        ["response.web_search_call.in_progress"] = ResponseEventTypes.ResponseWebSearchCallInProgress,
        ["response.web_search_call.completed"] = ResponseEventTypes.ResponseWebSearchCallCompleted,
        ["response.refusal.done"] = ResponseEventTypes.ResponseRefusalDone,
        ["response.refusal.delta"] = ResponseEventTypes.ResponseRefusalDelta,
        ["response.reasoning_summary_text.done"] = ResponseEventTypes.ResponseReasoningSummaryTextDone,
        ["response.reasoning_summary_text.delta"] = ResponseEventTypes.ResponseReasoningSummaryTextDelta,
        ["response.reasoning_summary_part.done"] = ResponseEventTypes.ResponseReasoningSummaryPartDone,
        ["response.reasoning_summary_part.added"] = ResponseEventTypes.ResponseReasoningSummaryPartAdded,
        ["response.reasoning_summary.done"] = ResponseEventTypes.ResponseReasoningSummaryDone,
        ["response.reasoning_summary.delta"] = ResponseEventTypes.ResponseReasoningSummaryDelta,
        ["response.reasoning.done"] = ResponseEventTypes.ResponseReasoningDone,
        ["response.reasoning.delta"] = ResponseEventTypes.ResponseReasoningDelta,
        ["response.queued"] = ResponseEventTypes.ResponseQueued,
        ["response.output_text.done"] = ResponseEventTypes.ResponseOutputTextDone,
        ["response.output_text.delta"] = ResponseEventTypes.ResponseOutputTextDelta,
        ["response.output_text_annotation.added"] = ResponseEventTypes.ResponseOutputTextAnnotationAdded,
        ["response.output_text.annotation.added"] = ResponseEventTypes.ResponseOutputTextAnnotationAdded,
        ["response.output_item.added"] = ResponseEventTypes.ResponseOutputItemAdded,
        ["response.output_item.done"] = ResponseEventTypes.ResponseOutputItemDone,
        ["response.content_part.added"] = ResponseEventTypes.ResponseContentPartAdded,
        ["response.content_part.done"] = ResponseEventTypes.ResponseContentPartDone,
        ["response.mcp_list_tools.in_progress"] = ResponseEventTypes.ResponseMcpListToolsInProgress,
        ["response.mcp_list_tools.failed"] = ResponseEventTypes.ResponseMcpListToolsFailed,
        ["response.mcp_list_tools.completed"] = ResponseEventTypes.ResponseMcpListToolsCompleted,
        ["response.mcp_call.in_progress"] = ResponseEventTypes.ResponseMcpCallInProgress,
        ["response.mcp_call.failed"] = ResponseEventTypes.ResponseMcpCallFailed,
        ["response.mcp_call.completed"] = ResponseEventTypes.ResponseMcpCallCompleted,
        ["response.mcp_call.arguments.done"] = ResponseEventTypes.ResponseMcpCallArgumentsDone,
        ["response.mcp_call.arguments.delta"] = ResponseEventTypes.ResponseMcpCallArgumentsDelta,
        ["response.in_progress"] = ResponseEventTypes.ResponseInProgress,
        ["response.image_generation_call.partial_image"] = ResponseEventTypes.ResponseImageGenerationCallPartialImage,
        ["response.image_generation_call.in_progress"] = ResponseEventTypes.ResponseImageGenerationCallInProgress,
        ["response.image_generation_call.generating"] = ResponseEventTypes.ResponseImageGenerationCallGenerating,
        ["response.image_generation_call.completed"] = ResponseEventTypes.ResponseImageGenerationCallCompleted,
        ["response.function_call_arguments.done"] = ResponseEventTypes.ResponseFunctionCallArgumentsDone,
        ["response.function_call_arguments.delta"] = ResponseEventTypes.ResponseFunctionCallArgumentsDelta,
        ["response.file_search_call.searching"] = ResponseEventTypes.ResponseFileSearchCallSearching,
        ["response.file_search_call.in_progress"] = ResponseEventTypes.ResponseFileSearchCallInProgress,
        ["response.file_search_call.completed"] = ResponseEventTypes.ResponseFileSearchCallCompleted,
        ["response.apply_patch_call.in_progress"] = ResponseEventTypes.ResponseApplyPatchCallInProgress,
        ["response.apply_patch_call.completed"] = ResponseEventTypes.ResponseApplyPatchCallCompleted,
        ["response.apply_patch_call.failed"] = ResponseEventTypes.ResponseApplyPatchCallFailed,
        ["response.shell_call.in_progress"] = ResponseEventTypes.ResponseShellCallInProgress,
        ["response.shell_call.completed"] = ResponseEventTypes.ResponseShellCallCompleted,
        ["response.shell_call.failed"] = ResponseEventTypes.ResponseShellCallFailed,
        ["error"] = ResponseEventTypes.ResponseError,
        ["response.code_interpreter_call.in_progress"] = ResponseEventTypes.ResponseCodeInterpreterCallInProgress,
        ["response.code_interpreter_call.interpreting"] = ResponseEventTypes.ResponseCodeInterpreterCallInterpreting,
        ["response.code_interpreter_call.completed"] = ResponseEventTypes.ResponseCodeInterpreterCallCompleted,
        ["response.reasoning_text.delta"] = ResponseEventTypes.ResponseReasoningTextDelta,
        ["response.mcp_call_arguments.delta"] = ResponseEventTypes.ResponseMcpCallArgumentsTextDelta,
        ["response.mcp_call_arguments.done"] = ResponseEventTypes.ResponseMcpCallArgumentsTextDone,
        ["response.custom_tool_call_input.delta"] = ResponseEventTypes.ResponseCustomToolCallInputDelta,
        ["response.custom_tool_call_input.done"] = ResponseEventTypes.ResponseCustomToolCallInputDone,
        ["response.reasoning_text.done"] = ResponseEventTypes.ResponseReasoningDone,
        ["response.code_interpreter_call_code.done"] = ResponseEventTypes.ResponseCodeInterpreterCallCodeDone,
        ["response.code_interpreter_call_code.delta"] = ResponseEventTypes.ResponseCodeInterpreterCallCodeDelta,
        ["response.failed"] = ResponseEventTypes.ResponseFailed,
        ["response.completed"] = ResponseEventTypes.ResponseCompleted,
        ["response.created"] = ResponseEventTypes.ResponseCreated,
        ["response.incomplete"] = ResponseEventTypes.ResponseIncomplete,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<ResponseEventTypes, Type> EventTypeToType = new Dictionary<ResponseEventTypes, Type>
    {
        [ResponseEventTypes.ResponseInjectCreated] = typeof(ResponseEventInjectCreated),
        [ResponseEventTypes.ResponseInjectFailed] = typeof(ResponseEventInjectFailed),
        [ResponseEventTypes.ResponseSteerAccepted] = typeof(ResponseEventSteerAccepted),
        [ResponseEventTypes.ResponseSteerPending] = typeof(ResponseEventSteerPending),
        [ResponseEventTypes.ResponseSteerFailed] = typeof(ResponseEventSteerFailed),
        [ResponseEventTypes.ResponseWebSearchCallSearching] = typeof(ResponseEventWebSearchCallSearching),
        [ResponseEventTypes.ResponseWebSearchCallInProgress] = typeof(ResponseEventWebSearchCallInProgress),
        [ResponseEventTypes.ResponseWebSearchCallCompleted] = typeof(ResponseEventWebSearchCallCompleted),
        [ResponseEventTypes.ResponseRefusalDone] = typeof(ResponseEventRefusalDone),
        [ResponseEventTypes.ResponseRefusalDelta] = typeof(ResponseEventRefusalDelta),
        [ResponseEventTypes.ResponseReasoningSummaryTextDone] = typeof(ResponseEventReasoningSummaryTextDone),
        [ResponseEventTypes.ResponseReasoningSummaryTextDelta] = typeof(ResponseEventReasoningSummaryTextDelta),
        [ResponseEventTypes.ResponseReasoningSummaryPartDone] = typeof(ResponseEventReasoningSummaryPartDone),
        [ResponseEventTypes.ResponseReasoningSummaryPartAdded] = typeof(ResponseEventReasoningSummaryPartAdded),
        [ResponseEventTypes.ResponseReasoningSummaryDone] = typeof(ResponseEventReasoningSummaryDone),
        [ResponseEventTypes.ResponseReasoningSummaryDelta] = typeof(ResponseEventReasoningSummaryDelta),
        [ResponseEventTypes.ResponseReasoningDone] = typeof(ResponseEventReasoningDone),
        [ResponseEventTypes.ResponseReasoningDelta] = typeof(ResponseEventReasoningDelta),
        [ResponseEventTypes.ResponseQueued] = typeof(ResponseEventQueued),
        [ResponseEventTypes.ResponseOutputTextDone] = typeof(ResponseEventOutputTextDone),
        [ResponseEventTypes.ResponseOutputTextDelta] = typeof(ResponseEventOutputTextDelta),
        [ResponseEventTypes.ResponseOutputTextAnnotationAdded] = typeof(ResponseEventOutputTextAnnotationAdded),
        [ResponseEventTypes.ResponseOutputItemAdded] = typeof(ResponseEventOutputItemAdded),
        [ResponseEventTypes.ResponseOutputItemDone] = typeof(ResponseEventOutputItemDone),
        [ResponseEventTypes.ResponseContentPartAdded] = typeof(ResponseEventContentPartAdded),
        [ResponseEventTypes.ResponseContentPartDone] = typeof(ResponseEventContentPartDone),
        [ResponseEventTypes.ResponseMcpListToolsInProgress] = typeof(ResponseEventMcpListToolsInProgress),
        [ResponseEventTypes.ResponseMcpListToolsFailed] = typeof(ResponseEventMcpListToolsFailed),
        [ResponseEventTypes.ResponseMcpListToolsCompleted] = typeof(ResponseEventMcpListToolsCompleted),
        [ResponseEventTypes.ResponseMcpCallInProgress] = typeof(ResponseEventMcpCallInProgress),
        [ResponseEventTypes.ResponseMcpCallFailed] = typeof(ResponseEventMcpCallFailed),
        [ResponseEventTypes.ResponseMcpCallCompleted] = typeof(ResponseEventMcpCallCompleted),
        [ResponseEventTypes.ResponseMcpCallArgumentsDone] = typeof(ResponseEventMcpCallArgumentsDone),
        [ResponseEventTypes.ResponseMcpCallArgumentsDelta] = typeof(ResponseEventMcpCallArgumentsDelta),
        [ResponseEventTypes.ResponseInProgress] = typeof(ResponseEventInProgress),
        [ResponseEventTypes.ResponseImageGenerationCallPartialImage] = typeof(ResponseEventImageGenerationCallPartialImage),
        [ResponseEventTypes.ResponseImageGenerationCallInProgress] = typeof(ResponseEventImageGenerationCallInProgress),
        [ResponseEventTypes.ResponseImageGenerationCallGenerating] = typeof(ResponseEventImageGenerationCallGenerating),
        [ResponseEventTypes.ResponseImageGenerationCallCompleted] = typeof(ResponseEventImageGenerationCallCompleted),
        [ResponseEventTypes.ResponseFunctionCallArgumentsDone] = typeof(ResponseEventFunctionCallArgumentsDone),
        [ResponseEventTypes.ResponseFunctionCallArgumentsDelta] = typeof(ResponseEventFunctionCallArgumentsDelta),
        [ResponseEventTypes.ResponseFileSearchCallSearching] = typeof(ResponseEventFileSearchCallSearching),
        [ResponseEventTypes.ResponseFileSearchCallInProgress] = typeof(ResponseEventFileSearchCallInProgress),
        [ResponseEventTypes.ResponseFileSearchCallCompleted] = typeof(ResponseEventFileSearchCallCompleted),
        [ResponseEventTypes.ResponseApplyPatchCallInProgress] = typeof(ResponseEventApplyPatchCallInProgress),
        [ResponseEventTypes.ResponseApplyPatchCallCompleted] = typeof(ResponseEventApplyPatchCallCompleted),
        [ResponseEventTypes.ResponseApplyPatchCallFailed] = typeof(ResponseEventApplyPatchCallFailed),
        [ResponseEventTypes.ResponseShellCallInProgress] = typeof(ResponseEventShellCallInProgress),
        [ResponseEventTypes.ResponseShellCallCompleted] = typeof(ResponseEventShellCallCompleted),
        [ResponseEventTypes.ResponseShellCallFailed] = typeof(ResponseEventShellCallFailed),
        [ResponseEventTypes.ResponseError] = typeof(ResponseEventError),
        [ResponseEventTypes.ResponseCodeInterpreterCallInProgress] = typeof(ResponseEventCodeInterpreterCallInProgress),
        [ResponseEventTypes.ResponseCodeInterpreterCallInterpreting] = typeof(ResponseEventCodeInterpreterCallInterpreting),
        [ResponseEventTypes.ResponseCodeInterpreterCallCompleted] = typeof(ResponseEventCodeInterpreterCallCompleted),
        [ResponseEventTypes.ResponseReasoningTextDelta] = typeof(ResponseEventReasoningTextDelta),
        [ResponseEventTypes.ResponseMcpCallArgumentsTextDelta] = typeof(ResponseEventMcpCallArgumentsTextDelta),
        [ResponseEventTypes.ResponseMcpCallArgumentsTextDone] = typeof(ResponseEventMcpCallArgumentsTextDone),
        [ResponseEventTypes.ResponseCustomToolCallInputDelta] = typeof(ResponseEventCustomToolCallInputDelta),
        [ResponseEventTypes.ResponseCustomToolCallInputDone] = typeof(ResponseEventCustomToolCallInputDone),
        [ResponseEventTypes.ResponseCodeInterpreterCallCodeDone] = typeof(ResponseEventCodeInterpreterCallCodeDone),
        [ResponseEventTypes.ResponseCodeInterpreterCallCodeDelta] = typeof(ResponseEventCodeInterpreterCallCodeDelta),
        [ResponseEventTypes.ResponseFailed] = typeof(ResponseEventFailed),
        [ResponseEventTypes.ResponseCompleted] = typeof(ResponseEventCompleted),
        [ResponseEventTypes.ResponseCreated] = typeof(ResponseEventCreated),
        [ResponseEventTypes.ResponseIncomplete] = typeof(ResponseEventIncomplete),
    }.ToFrozenDictionary();

    private static IResponseEvent? DeserializeEvent(string data, ResponseEventTypes eventType)
    {
        if (EventTypeToType.TryGetValue(eventType, out Type? type))
        {
            return (IResponseEvent?)JsonConvert.DeserializeObject(data, type);
        }
        
        return null;
    }

    internal static IResponseEvent? DeserializeResponseEvent(string type, string data) =>
        EventTypeToEnum.TryGetValue(type, out ResponseEventTypes eventType) ? DeserializeEvent(data, eventType) : null;

    private static T DeserializeEvent<T>(string data) where T : class
    {
        return data.JsonDecode<T>()!;
    }

    internal ResponsesEndpoint(TornadoApi api) : base(api)
    {
    }

    /// <summary>
    ///     The name of the endpoint, which is the final path segment in the API URL.  For example, "completions".
    /// </summary>
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Responses;

    /// <summary>
    /// Retrieves a model response with the given ID.
    /// </summary>
    public Task<ResponseResult?> GetResponse(string responseId, CancellationToken cancellationToken = default) =>
        GetResponse(responseId, null, cancellationToken);

    /// <summary>Accesses a stored response with explicit beta header options.</summary>
    public async Task<ResponseResult?> GetResponse(string responseId, ResponseApiOptions? options, CancellationToken cancellationToken = default)
    {
        HttpCallResult<ResponseResult>? data = await GetResponseSafe(responseId, options, cancellationToken).ConfigureAwait(false);
        
        if (!data.Ok)
        {
            throw data.Exception;
        }
        
        return data.Data;
    }
    
    /// <summary>
    /// Retrieves a model response with the given ID.
    /// </summary>
    public Task<HttpCallResult<ResponseResult>> GetResponseSafe(string responseId, CancellationToken cancellationToken = default) =>
        GetResponseSafe(responseId, null, cancellationToken);

    /// <summary>Accesses a stored response with explicit beta header options.</summary>
    public async Task<HttpCallResult<ResponseResult>> GetResponseSafe(string responseId, ResponseApiOptions? options, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.GetProvider(LLmProviders.OpenAi);
        return await HttpGet<ResponseResult>(provider, Endpoint, url: GetUrl(provider, $"/{responseId}"), ct: cancellationToken, headers: options?.Headers()).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Deletes a model response with the given ID.
    /// </summary>
    public Task<ResponseDeleted> DeleteResponse(string responseId, CancellationToken cancellationToken = default) =>
        DeleteResponse(responseId, null, cancellationToken);

    /// <summary>Accesses a stored response with explicit beta header options.</summary>
    public async Task<ResponseDeleted> DeleteResponse(string responseId, ResponseApiOptions? options, CancellationToken cancellationToken = default)
    {
        HttpCallResult<ResponseDeleted> data = await DeleteResponseSafe(responseId, options, cancellationToken);
        
        if (!data.Ok)
        {
            throw data.Exception;
        }
        
        return data.Data;
    }
    
    /// <summary>
    /// Deletes a model response with the given ID.
    /// </summary>
    public Task<HttpCallResult<ResponseDeleted>> DeleteResponseSafe(string responseId, CancellationToken cancellationToken = default) =>
        DeleteResponseSafe(responseId, null, cancellationToken);

    /// <summary>Accesses a stored response with explicit beta header options.</summary>
    public async Task<HttpCallResult<ResponseDeleted>> DeleteResponseSafe(string responseId, ResponseApiOptions? options, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.GetProvider(LLmProviders.OpenAi);
        return await HttpDelete<ResponseDeleted>(provider, Endpoint, url: GetUrl(provider, $"/{responseId}"), requestObject: options, ct: cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Cancels a model response with the given ID.
    /// </summary>
    public Task<ResponseResult> CancelResponse(string responseId, CancellationToken cancellationToken = default) =>
        CancelResponse(responseId, null, cancellationToken);

    /// <summary>Accesses a stored response with explicit beta header options.</summary>
    public async Task<ResponseResult> CancelResponse(string responseId, ResponseApiOptions? options, CancellationToken cancellationToken = default)
    {
        HttpCallResult<ResponseResult>? data = await CancelResponseSafe(responseId, options, cancellationToken).ConfigureAwait(false);
        
        if (!data.Ok)
        {
            throw data.Exception;
        }
        
        return data.Data;
    }
    
    /// <summary>
    /// Cancels a model response with the given ID.
    /// </summary>
    public Task<HttpCallResult<ResponseResult>> CancelResponseSafe(string responseId, CancellationToken cancellationToken = default) =>
        CancelResponseSafe(responseId, null, cancellationToken);

    /// <summary>Accesses a stored response with explicit beta header options.</summary>
    public async Task<HttpCallResult<ResponseResult>> CancelResponseSafe(string responseId, ResponseApiOptions? options, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.GetProvider(LLmProviders.OpenAi);
        return await HttpPost<ResponseResult>(provider, Endpoint, url: GetUrl(provider, $"/{responseId}/cancel"), requestObj: options, ct: cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Returns a list of input items for a given response.
    /// </summary>
    public Task<ListResponse<ResponseInputItem>> ListResponseInputItems(string responseId, ListQuery? query = null, CancellationToken cancellationToken = default) =>
        ListResponseInputItems(responseId, null, query, cancellationToken);

    /// <summary>Lists stored input with explicit beta header options.</summary>
    public async Task<ListResponse<ResponseInputItem>> ListResponseInputItems(string responseId, ResponseApiOptions? options, ListQuery? query = null, CancellationToken cancellationToken = default)
    {
        HttpCallResult<ListResponse<ResponseInputItem>> data = await ListResponseInputItemsSafe(responseId, options, query, cancellationToken).ConfigureAwait(false);

        if (!data.Ok)
        {
            throw data.Exception;
        }
        
        return data.Data;
    }
    
    /// <summary>
    /// Returns a list of input items for a given response.
    /// </summary>
    public Task<HttpCallResult<ListResponse<ResponseInputItem>>> ListResponseInputItemsSafe(string responseId, ListQuery? query = null, CancellationToken cancellationToken = default) =>
        ListResponseInputItemsSafe(responseId, null, query, cancellationToken);

    /// <summary>Lists stored input with explicit beta header options.</summary>
    public async Task<HttpCallResult<ListResponse<ResponseInputItem>>> ListResponseInputItemsSafe(string responseId, ResponseApiOptions? options, ListQuery? query = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.GetProvider(LLmProviders.OpenAi);
        return await HttpGet<ListResponse<ResponseInputItem>>(provider, Endpoint, url: GetUrl(provider, $"/{responseId}/input_items"), queryParams: query?.ToQueryParams(LLmProviders.OpenAi), ct: cancellationToken, headers: options?.Headers()).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Compacts a conversation context window. Returns a compacted set of output items
    /// that can be passed as input to subsequent /responses calls.
    /// </summary>
    /// <param name="request">The compact request.</param>
    public async Task<ResponseCompactResult> CompactResponse(ResponseCompactRequest request)
    {
        HttpCallResult<ResponseCompactResult> data = await CompactResponseSafe(request).ConfigureAwait(false);

        if (!data.Ok)
        {
            throw data.Exception;
        }

        return data.Data;
    }
    
    /// <summary>
    /// Compacts a conversation context window. Returns a compacted set of output items
    /// that can be passed as input to subsequent /responses calls.
    /// </summary>
    /// <param name="request">The compact request.</param>
    public async Task<HttpCallResult<ResponseCompactResult>> CompactResponseSafe(ResponseCompactRequest request)
    {
        IEndpointProvider provider = Api.GetProvider(request.Model ?? ChatModel.OpenAi.Gpt56.V56Terra);
        string body = request.Serialize();
        return await HttpPost<ResponseCompactResult>(provider, Endpoint, url: GetUrl(provider, "/compact"), postData: body, ct: request.CancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Returns the number of input tokens a response request would consume, without actually generating a response.
    /// </summary>
    /// <param name="request">The request to count tokens for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ResponseInputTokensResult> CountInputTokens(ResponseRequest request, CancellationToken cancellationToken = default)
    {
        HttpCallResult<ResponseInputTokensResult> data = await CountInputTokensSafe(request, cancellationToken).ConfigureAwait(false);

        if (!data.Ok)
        {
            throw data.Exception;
        }

        return data.Data;
    }

    /// <summary>
    /// Returns the number of input tokens a response request would consume, without actually generating a response.
    /// </summary>
    /// <param name="request">The request to count tokens for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HttpCallResult<ResponseInputTokensResult>> CountInputTokensSafe(ResponseRequest request, CancellationToken cancellationToken = default)
    {
        IEndpointProvider provider = Api.GetProvider(request.Model ?? ChatModel.OpenAi.Gpt56.V56Terra);
        TornadoRequestContent requestBody = request.Serialize(provider);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, request.CancellationToken);
        return await HttpPost<ResponseInputTokensResult>(provider, Endpoint, url: GetUrl(provider, "/input_tokens"), postData: requestBody.Body, model: request.Model, requestObj: request, ct: linked.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a responses API request.
    /// </summary>
    /// <param name="request">The request</param>
    public async Task<ResponseResult> CreateResponse(ResponseRequest request)
    {
        HttpCallResult<ResponseResult> data = await CreateResponseSafe(request).ConfigureAwait(false);

        if (!data.Ok)
        {
            throw data.Exception;
        }

        return data.Data;
    }
    
    /// <summary>
    /// Creates a responses API request.
    /// </summary>
    /// <param name="request">The request</param>
    public async Task<HttpCallResult<ResponseResult>> CreateResponseSafe(ResponseRequest request)
    {
        IEndpointProvider provider = Api.GetProvider(request.Model ?? ChatModel.OpenAi.Gpt56.V56Terra);
        TornadoRequestContent requestBody = request.Serialize(provider);
        HttpCallResult<ResponseResult> result = await HttpPost<ResponseResult>(provider, Endpoint, url: requestBody.Url, postData: requestBody.Body, model: request.Model, requestObj: request, ct: request.CancellationToken).ConfigureAwait(false);

        if (result is { Ok: true, Data: not null })
        {
            await HandleResponse(request, result.Data).ConfigureAwait(false);
        }

        return result;
    }

    internal static async Task<object?> HandleResponse(ResponseRequest request, ResponseResult result, ISet<string>? suppliedCallIds = null)
    {
        if (request.Text?.Format is ResponseTextFormatConfigurationJsonSchema jsonSchema)
        {
            string? outputText = result.OutputText;
            
            if (!outputText.IsNullOrWhiteSpace())
            {
                await jsonSchema.Invoke(outputText).ConfigureAwait(false);
                return jsonSchema.Result;
            }

            return null;
        }

        List<ResponseFunctionTool>? toolDefs = request.Tools?.OfType<ResponseFunctionTool>().ToList();
        
        if (toolDefs?.Any(x => x.Delegate is not null) ?? false)
        {
            List<ResponseFunctionToolCallItem>? toolCalls = result.Output?.OfType<ResponseFunctionToolCallItem>().ToList();

            if (toolCalls?.Count > 0)
            {
                List<Task> tasks = [];
                
                foreach (ResponseFunctionToolCallItem toolCall in toolCalls)
                {
                    if (suppliedCallIds?.Contains(toolCall.CallId) == true) continue;
                    toolCall.Result = null;
                    ResponseFunctionTool? match = toolDefs.FirstOrDefault(x => string.Equals(x.Name, toolCall.Name));

                    if (match?.Delegate is null)
                    {
                        continue;
                    }

                    // Async tool execution belongs to the application; do not block response delivery or start unobserved jobs.
                    if ((toolCall.Async ?? match.Async) == true) continue;
                    
                    tasks.Add(InvokeToolAsync(match, toolCall));
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);   
                }   
            }
        }

        return null;
    }
    
    private static async Task InvokeToolAsync(ResponseFunctionTool match, ResponseFunctionToolCallItem toolCall)
    {
        await match.Invoke(toolCall.Arguments).ConfigureAwait(false);
        toolCall.Result = match.Result;
    }

    /// <summary>
    /// Creates a new session.
    /// </summary>
    public ResponsesSession CreateSession(ResponseRequest request, ResponseStreamEventHandler eventsHandler)
    {
        return new ResponsesSession
        {
            Request = request,
            Endpoint = this,
            EventsHandler = eventsHandler
        };
    }
    
    /// <summary>
    /// Creates a new session.
    /// </summary>
    public ResponsesSession CreateSession()
    {
        return new ResponsesSession
        {
            Request = null,
            Endpoint = this,
            EventsHandler = null
        };
    }
    
    /// <summary>
    /// Creates a new session.
    /// </summary>
    public ResponsesSession CreateSession(ResponseStreamEventHandler eventsHandler)
    {
        return new ResponsesSession
        {
            Request = null,
            Endpoint = this,
            EventsHandler = eventsHandler
        };
    }

    /// <summary>
    /// Opens a persistent WebSocket connection to <c>wss://api.openai.com/v1/responses</c> for low-latency,
    /// multi-turn agentic workflows. Each turn is sent as a <c>response.create</c> event on the open socket.
    /// </summary>
    /// <param name="options">Optional connection options.</param>
    /// <param name="cancellationToken">Cancellation token used while establishing the connection.</param>
    public async Task<ResponsesWebSocketConnection> ConnectWebSocketAsync(
        ResponsesWebSocketConnectOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ResponsesWebSocketConnectOptions();
        IEndpointProvider provider = Api.GetProvider(LLmProviders.OpenAi);
        ClientWebSocket webSocket = new ClientWebSocket();
        ResponsesWebSocketConnection.ApplyAuthHeaders(webSocket, provider, options);

        string url = ResponsesWebSocketConnection.BuildWebSocketUrl(provider, options.UrlOverride);

        try
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, options.CancellationToken);
            await webSocket.ConnectAsync(new Uri(url), linked.Token).ConfigureAwait(false);
            options.OnOpen?.Invoke();
        }
        catch (Exception ex)
        {
            webSocket.Dispose();
            options.OnError?.Invoke(ex);
            throw;
        }

        return new ResponsesWebSocketConnection(this, provider, webSocket, options);
    }

    /// <summary>
    ///     Stream Realtime API events as they arrive, using the provided event handler to process each event type.
    /// </summary>
    /// <param name="request">The request to send to the API.</param>
    /// <param name="eventsHandler">Optional event handler to process streaming events.</param>
    /// <param name="token">Optional cancellation token.</param>
    public async Task StreamResponseRich(ResponseRequest request, ResponseStreamEventHandler? eventsHandler = null, CancellationToken token = default)
    {
        await StreamResponseRichInternal(request, null, eventsHandler, false, token).ConfigureAwait(false);
    }
    
    /// <summary>
    ///     Stream Realtime API events as they arrive, using the provided event handler to process each event type.
    /// </summary>
    /// <param name="request">The request to send to the API.</param>
    /// <param name="eventsHandler">Optional event handler to process streaming events.</param>
    /// <param name="token">Optional cancellation token.</param>
    public async Task StreamResponseRichSafe(ResponseRequest request, ResponseStreamEventHandler? eventsHandler = null, CancellationToken token = default)
    {
        await StreamResponseRichInternal(request, null, eventsHandler, true, token).ConfigureAwait(false);
    }
    
    internal async Task StreamResponseRichInternal(ResponseRequest request, ResponsesSession? session = null, ResponseStreamEventHandler? eventsHandler = null, bool isSafe = false, CancellationToken token = default)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, request.CancellationToken);
        token = linked.Token;
        bool? streamOption = request.Stream;
        IEndpointProvider provider = Api.GetProvider(request.Model ?? ChatModel.OpenAi.Gpt56.V56Terra);
        TornadoRequestContent requestBody;
        try
        {
            request.Stream = true;
            requestBody = request.Serialize(provider);
        }
        finally { request.Stream = streamOption; }

        await using TornadoStreamRequest tornadoStreamRequest = await HttpStreamingRequestData(provider, Endpoint, requestBody.Url, queryParams: null, HttpVerbs.Post, requestBody.Body, request.Model, request, token).ConfigureAwait(false);

        if (tornadoStreamRequest.Exception is not null)
        {
            if (isSafe || eventsHandler?.OnException is not null)
            {
                if (eventsHandler?.OnException != null)
                {
                    await eventsHandler.OnException(tornadoStreamRequest);
                }
            }
            else
            {
                throw tornadoStreamRequest.Exception;   
            }
        }

        if (tornadoStreamRequest.StreamReader is not null)
        {
            await foreach (ServerSentEvent runStreamEvent in ReadResponseEvents(tornadoStreamRequest.StreamReader, token).ConfigureAwait(false))
            {
                if (eventsHandler?.OnSse != null)
                {
                    await eventsHandler.OnSse(runStreamEvent);
                }

                string type = ResolveResponseEventType(runStreamEvent.EventType, runStreamEvent.Data);
                await DispatchResponseStreamEventAsync(type, runStreamEvent.Data, session, eventsHandler, token).ConfigureAwait(false);
            }
        }
    }

    private static async IAsyncEnumerable<ServerSentEvent> ReadResponseEvents(StreamReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (SseItem<string> item in SseParser.Create(reader.BaseStream).EnumerateAsync(cancellationToken).ConfigureAwait(false))
            yield return new ServerSentEvent { Data = item.Data, EventType = item.EventType };
    }

    internal static string ResolveResponseEventType(string? eventType, string data)
    {
        if (!string.IsNullOrEmpty(eventType) && !string.Equals(eventType, SseParser.EventTypeDefault, StringComparison.Ordinal))
        {
            return eventType;
        }

        try
        {
            return JObject.Parse(data)["type"]?.ToString() ?? eventType ?? string.Empty;
        }
        catch
        {
            return eventType ?? string.Empty;
        }
    }

    internal async Task<bool> DispatchResponseStreamEventAsync(
        string type,
        string data,
        ResponsesSession? session,
        ResponseStreamEventHandler? eventsHandler,
        CancellationToken token)
    {
        if (!EventTypeToEnum.TryGetValue(type, out ResponseEventTypes eventType))
        {
            return false;
        }

        if (eventsHandler?.OnEvent is not null || eventType is ResponseEventTypes.ResponseCompleted or ResponseEventTypes.ResponseCreated or ResponseEventTypes.ResponseFailed or ResponseEventTypes.ResponseIncomplete)
        {
            IResponseEvent? evt = DeserializeEvent(data, eventType);

            if (eventType is ResponseEventTypes.ResponseCreated && evt is ResponseEventCreated created)
            {
                session ??= new ResponsesSession { Endpoint = this };
                session.CurrentResponse = created.Response;
            }

            if (eventType is ResponseEventTypes.ResponseCompleted && evt is ResponseEventCompleted completed)
            {
                session ??= new ResponsesSession { Endpoint = this };
                session.CurrentResponse = completed.Response;
            }

            if (evt is ResponseEventFailed failed)
            {
                session ??= new ResponsesSession { Endpoint = this };
                session.CurrentResponse = failed.Response;
            }
            if (evt is ResponseEventIncomplete incomplete)
            {
                session ??= new ResponsesSession { Endpoint = this };
                session.CurrentResponse = incomplete.Response;
            }

            if (evt is not null && eventsHandler?.OnEvent is not null)
            {
                await eventsHandler.OnEvent(evt).ConfigureAwait(false);
            }
        }

        if (eventsHandler is null)
        {
            return eventType is ResponseEventTypes.ResponseCompleted
                or ResponseEventTypes.ResponseFailed
                or ResponseEventTypes.ResponseIncomplete
                or ResponseEventTypes.ResponseError;
        }

        switch (eventType)
        {
            case ResponseEventTypes.ResponseInjectCreated:
                if (eventsHandler.OnResponseInjectCreated is not null)
                    await eventsHandler.OnResponseInjectCreated(DeserializeEvent<ResponseEventInjectCreated>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseInjectFailed:
                if (eventsHandler.OnResponseInjectFailed is not null)
                    await eventsHandler.OnResponseInjectFailed(DeserializeEvent<ResponseEventInjectFailed>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseSteerAccepted:
                if (eventsHandler.OnResponseSteerAccepted is not null)
                    await eventsHandler.OnResponseSteerAccepted(DeserializeEvent<ResponseEventSteerAccepted>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseSteerPending:
                if (eventsHandler.OnResponseSteerPending is not null)
                    await eventsHandler.OnResponseSteerPending(DeserializeEvent<ResponseEventSteerPending>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseSteerFailed:
                if (eventsHandler.OnResponseSteerFailed is not null)
                    await eventsHandler.OnResponseSteerFailed(DeserializeEvent<ResponseEventSteerFailed>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseCreated:
                if (eventsHandler.OnResponseCreated != null)
                    await eventsHandler.OnResponseCreated(DeserializeEvent<ResponseEventCreated>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseInProgress:
                if (eventsHandler.OnResponseInProgress != null)
                    await eventsHandler.OnResponseInProgress(DeserializeEvent<ResponseEventInProgress>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseCompleted:
                if (eventsHandler.OnResponseCompleted != null)
                    await eventsHandler.OnResponseCompleted(DeserializeEvent<ResponseEventCompleted>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseFailed:
                if (eventsHandler.OnResponseFailed != null)
                    await eventsHandler.OnResponseFailed(DeserializeEvent<ResponseEventFailed>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseIncomplete:
                if (eventsHandler.OnResponseIncomplete != null)
                    await eventsHandler.OnResponseIncomplete(DeserializeEvent<ResponseEventIncomplete>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseQueued:
                if (eventsHandler.OnResponseQueued != null)
                    await eventsHandler.OnResponseQueued(DeserializeEvent<ResponseEventQueued>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseError:
                if (eventsHandler.OnResponseError != null)
                    await eventsHandler.OnResponseError(DeserializeEvent<ResponseEventError>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseOutputItemAdded:
                if (eventsHandler.OnResponseOutputItemAdded != null)
                    await eventsHandler.OnResponseOutputItemAdded(DeserializeEvent<ResponseEventOutputItemAdded>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseOutputItemDone:
                if (eventsHandler.OnResponseOutputItemDone != null)
                    await eventsHandler.OnResponseOutputItemDone(DeserializeEvent<ResponseEventOutputItemDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseContentPartAdded:
                if (eventsHandler.OnResponseContentPartAdded != null)
                    await eventsHandler.OnResponseContentPartAdded(DeserializeEvent<ResponseEventContentPartAdded>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseContentPartDone:
                if (eventsHandler.OnResponseContentPartDone != null)
                    await eventsHandler.OnResponseContentPartDone(DeserializeEvent<ResponseEventContentPartDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseOutputTextDelta:
                if (eventsHandler.OnResponseOutputTextDelta != null)
                    await eventsHandler.OnResponseOutputTextDelta(DeserializeEvent<ResponseEventOutputTextDelta>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseOutputTextDone:
                if (eventsHandler.OnResponseOutputTextDone != null)
                    await eventsHandler.OnResponseOutputTextDone(DeserializeEvent<ResponseEventOutputTextDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseOutputTextAnnotationAdded:
                if (eventsHandler.OnResponseOutputTextAnnotationAdded != null)
                    await eventsHandler.OnResponseOutputTextAnnotationAdded(DeserializeEvent<ResponseEventOutputTextAnnotationAdded>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseRefusalDelta:
                if (eventsHandler.OnResponseRefusalDelta != null)
                    await eventsHandler.OnResponseRefusalDelta(DeserializeEvent<ResponseEventRefusalDelta>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseRefusalDone:
                if (eventsHandler.OnResponseRefusalDone != null)
                    await eventsHandler.OnResponseRefusalDone(DeserializeEvent<ResponseEventRefusalDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseFunctionCallArgumentsDelta:
                if (eventsHandler.OnResponseFunctionCallArgumentsDelta != null)
                    await eventsHandler.OnResponseFunctionCallArgumentsDelta(DeserializeEvent<ResponseEventFunctionCallArgumentsDelta>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseFunctionCallArgumentsDone:
                if (eventsHandler.OnResponseFunctionCallArgumentsDone != null)
                    await eventsHandler.OnResponseFunctionCallArgumentsDone(DeserializeEvent<ResponseEventFunctionCallArgumentsDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseFileSearchCallInProgress:
                if (eventsHandler.OnResponseFileSearchCallInProgress != null)
                    await eventsHandler.OnResponseFileSearchCallInProgress(DeserializeEvent<ResponseEventFileSearchCallInProgress>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseFileSearchCallSearching:
                if (eventsHandler.OnResponseFileSearchCallSearching != null)
                    await eventsHandler.OnResponseFileSearchCallSearching(DeserializeEvent<ResponseEventFileSearchCallSearching>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseFileSearchCallCompleted:
                if (eventsHandler.OnResponseFileSearchCallCompleted != null)
                    await eventsHandler.OnResponseFileSearchCallCompleted(DeserializeEvent<ResponseEventFileSearchCallCompleted>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseApplyPatchCallInProgress:
                if (eventsHandler.OnResponseApplyPatchCallInProgress != null)
                    await eventsHandler.OnResponseApplyPatchCallInProgress(DeserializeEvent<ResponseEventApplyPatchCallInProgress>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseApplyPatchCallCompleted:
                if (eventsHandler.OnResponseApplyPatchCallCompleted != null)
                    await eventsHandler.OnResponseApplyPatchCallCompleted(DeserializeEvent<ResponseEventApplyPatchCallCompleted>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseApplyPatchCallFailed:
                if (eventsHandler.OnResponseApplyPatchCallFailed != null)
                    await eventsHandler.OnResponseApplyPatchCallFailed(DeserializeEvent<ResponseEventApplyPatchCallFailed>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseShellCallInProgress:
                if (eventsHandler.OnResponseShellCallInProgress != null)
                    await eventsHandler.OnResponseShellCallInProgress(DeserializeEvent<ResponseEventShellCallInProgress>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseShellCallCompleted:
                if (eventsHandler.OnResponseShellCallCompleted != null)
                    await eventsHandler.OnResponseShellCallCompleted(DeserializeEvent<ResponseEventShellCallCompleted>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseShellCallFailed:
                if (eventsHandler.OnResponseShellCallFailed != null)
                    await eventsHandler.OnResponseShellCallFailed(DeserializeEvent<ResponseEventShellCallFailed>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseWebSearchCallInProgress:
                if (eventsHandler.OnResponseWebSearchCallInProgress != null)
                    await eventsHandler.OnResponseWebSearchCallInProgress(DeserializeEvent<ResponseEventWebSearchCallInProgress>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseWebSearchCallSearching:
                if (eventsHandler.OnResponseWebSearchCallSearching != null)
                    await eventsHandler.OnResponseWebSearchCallSearching(DeserializeEvent<ResponseEventWebSearchCallSearching>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseWebSearchCallCompleted:
                if (eventsHandler.OnResponseWebSearchCallCompleted != null)
                    await eventsHandler.OnResponseWebSearchCallCompleted(DeserializeEvent<ResponseEventWebSearchCallCompleted>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseReasoningDelta:
                if (eventsHandler.OnResponseReasoningDelta != null)
                    await eventsHandler.OnResponseReasoningDelta(DeserializeEvent<ResponseEventReasoningDelta>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseReasoningDone:
                if (eventsHandler.OnResponseReasoningDone != null)
                    await eventsHandler.OnResponseReasoningDone(DeserializeEvent<ResponseEventReasoningDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseReasoningSummaryPartAdded:
                if (eventsHandler.OnResponseReasoningSummaryPartAdded != null)
                    await eventsHandler.OnResponseReasoningSummaryPartAdded(DeserializeEvent<ResponseEventReasoningSummaryPartAdded>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseReasoningSummaryPartDone:
                if (eventsHandler.OnResponseReasoningSummaryPartDone != null)
                    await eventsHandler.OnResponseReasoningSummaryPartDone(DeserializeEvent<ResponseEventReasoningSummaryPartDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseReasoningSummaryTextDelta:
                if (eventsHandler.OnResponseReasoningSummaryTextDelta != null)
                    await eventsHandler.OnResponseReasoningSummaryTextDelta(DeserializeEvent<ResponseEventReasoningSummaryTextDelta>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseReasoningSummaryTextDone:
                if (eventsHandler.OnResponseReasoningSummaryTextDone != null)
                    await eventsHandler.OnResponseReasoningSummaryTextDone(DeserializeEvent<ResponseEventReasoningSummaryTextDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseReasoningSummaryDelta:
                if (eventsHandler.OnResponseReasoningSummaryDelta != null)
                    await eventsHandler.OnResponseReasoningSummaryDelta(DeserializeEvent<ResponseEventReasoningSummaryDelta>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseReasoningSummaryDone:
                if (eventsHandler.OnResponseReasoningSummaryDone != null)
                    await eventsHandler.OnResponseReasoningSummaryDone(DeserializeEvent<ResponseEventReasoningSummaryDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseImageGenerationCallInProgress:
                if (eventsHandler.OnResponseImageGenerationCallInProgress != null)
                    await eventsHandler.OnResponseImageGenerationCallInProgress(DeserializeEvent<ResponseEventImageGenerationCallInProgress>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseImageGenerationCallGenerating:
                if (eventsHandler.OnResponseImageGenerationCallGenerating != null)
                    await eventsHandler.OnResponseImageGenerationCallGenerating(DeserializeEvent<ResponseEventImageGenerationCallGenerating>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseImageGenerationCallPartialImage:
                if (eventsHandler.OnResponseImageGenerationCallPartialImage != null)
                    await eventsHandler.OnResponseImageGenerationCallPartialImage(DeserializeEvent<ResponseEventImageGenerationCallPartialImage>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseImageGenerationCallCompleted:
                if (eventsHandler.OnResponseImageGenerationCallCompleted != null)
                    await eventsHandler.OnResponseImageGenerationCallCompleted(DeserializeEvent<ResponseEventImageGenerationCallCompleted>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseMcpCallArgumentsDelta:
                if (eventsHandler.OnResponseMcpCallArgumentsDelta != null)
                    await eventsHandler.OnResponseMcpCallArgumentsDelta(DeserializeEvent<ResponseEventMcpCallArgumentsDelta>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseMcpCallArgumentsDone:
                if (eventsHandler.OnResponseMcpCallArgumentsDone != null)
                    await eventsHandler.OnResponseMcpCallArgumentsDone(DeserializeEvent<ResponseEventMcpCallArgumentsDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseMcpCallInProgress:
                if (eventsHandler.OnResponseMcpCallInProgress != null)
                    await eventsHandler.OnResponseMcpCallInProgress(DeserializeEvent<ResponseEventMcpCallInProgress>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseMcpCallCompleted:
                if (eventsHandler.OnResponseMcpCallCompleted != null)
                    await eventsHandler.OnResponseMcpCallCompleted(DeserializeEvent<ResponseEventMcpCallCompleted>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseMcpCallFailed:
                if (eventsHandler.OnResponseMcpCallFailed != null)
                    await eventsHandler.OnResponseMcpCallFailed(DeserializeEvent<ResponseEventMcpCallFailed>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseMcpListToolsInProgress:
                if (eventsHandler.OnResponseMcpListToolsInProgress != null)
                    await eventsHandler.OnResponseMcpListToolsInProgress(DeserializeEvent<ResponseEventMcpListToolsInProgress>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseMcpListToolsCompleted:
                if (eventsHandler.OnResponseMcpListToolsCompleted != null)
                    await eventsHandler.OnResponseMcpListToolsCompleted(DeserializeEvent<ResponseEventMcpListToolsCompleted>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseMcpListToolsFailed:
                if (eventsHandler.OnResponseMcpListToolsFailed != null)
                    await eventsHandler.OnResponseMcpListToolsFailed(DeserializeEvent<ResponseEventMcpListToolsFailed>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseCodeInterpreterCallInProgress:
                if (eventsHandler.OnResponseCodeInterpreterCallInProgress != null)
                    await eventsHandler.OnResponseCodeInterpreterCallInProgress(DeserializeEvent<ResponseEventCodeInterpreterCallInProgress>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseCodeInterpreterCallCodeDelta:
                if (eventsHandler.OnResponseCodeInterpreterCallCodeDelta != null)
                    await eventsHandler.OnResponseCodeInterpreterCallCodeDelta(DeserializeEvent<ResponseEventCodeInterpreterCallCodeDelta>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseCodeInterpreterCallCodeDone:
                if (eventsHandler.OnResponseCodeInterpreterCallCodeDone != null)
                    await eventsHandler.OnResponseCodeInterpreterCallCodeDone(DeserializeEvent<ResponseEventCodeInterpreterCallCodeDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseCodeInterpreterCallInterpreting:
                if (eventsHandler.OnResponseCodeInterpreterCallInterpreting != null)
                    await eventsHandler.OnResponseCodeInterpreterCallInterpreting(DeserializeEvent<ResponseEventCodeInterpreterCallInterpreting>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseCodeInterpreterCallCompleted:
                if (eventsHandler.OnResponseCodeInterpreterCallCompleted != null)
                    await eventsHandler.OnResponseCodeInterpreterCallCompleted(DeserializeEvent<ResponseEventCodeInterpreterCallCompleted>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseReasoningTextDelta:
                if (eventsHandler.OnResponseReasoningTextDelta != null)
                    await eventsHandler.OnResponseReasoningTextDelta(DeserializeEvent<ResponseEventReasoningTextDelta>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseMcpCallArgumentsTextDelta:
                if (eventsHandler.OnResponseMcpCallArgumentsTextDelta != null)
                    await eventsHandler.OnResponseMcpCallArgumentsTextDelta(DeserializeEvent<ResponseEventMcpCallArgumentsTextDelta>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseMcpCallArgumentsTextDone:
                if (eventsHandler.OnResponseMcpCallArgumentsTextDone != null)
                    await eventsHandler.OnResponseMcpCallArgumentsTextDone(DeserializeEvent<ResponseEventMcpCallArgumentsTextDone>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseCustomToolCallInputDelta:
                if (eventsHandler.OnResponseCustomToolCallInputDelta != null)
                    await eventsHandler.OnResponseCustomToolCallInputDelta(DeserializeEvent<ResponseEventCustomToolCallInputDelta>(data)).ConfigureAwait(false);
                break;
            case ResponseEventTypes.ResponseCustomToolCallInputDone:
                if (eventsHandler.OnResponseCustomToolCallInputDone != null)
                    await eventsHandler.OnResponseCustomToolCallInputDone(DeserializeEvent<ResponseEventCustomToolCallInputDone>(data)).ConfigureAwait(false);
                break;
        }

        return eventType is ResponseEventTypes.ResponseCompleted
            or ResponseEventTypes.ResponseFailed
            or ResponseEventTypes.ResponseIncomplete
            or ResponseEventTypes.ResponseError;
    }
}
