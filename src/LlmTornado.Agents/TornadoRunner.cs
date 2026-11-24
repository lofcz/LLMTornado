using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Agents.Utility;
using LlmTornado.Responses;

namespace LlmTornado.Agents;

/// <summary>
/// <c>Runner</c> to run the agent loop
/// </summary>
public class TornadoRunner
{
    /// <summary>
    /// Invoke the agent loop to begin async
    /// </summary>
    /// <param name="agent">Agent to Run</param>
    /// <param name="input">Message to the Agent</param>
    /// <param name="guardRail">Input Guardrail To perform</param>
    /// <param name="singleTurn">Set loop to not loop</param>
    /// <param name="maxTurns">Max loops to perform</param>
    /// <param name="messagesToAppend"> Input messages to add to response</param>
    /// <param name="streaming">Enable streaming</param>
    /// <param name="runnerCallback">delegate to send event information </param>
    /// <param name="responseId">Previous Response ID from response API</param>
    /// <param name="cancellationToken">Cancellation token to cancel the run</param>
    /// <param name="toolPermissionHandle">Delegate to request tool permission from user</param>
    /// <returns>Result of the run</returns>
    /// <exception cref="GuardRailTriggerException">Triggers when Guardrail detects bad input</exception>
    /// <exception cref="Exception"> Max Turns Reached or Error</exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task<Conversation> RunAsync(
        TornadoAgent agent,
        string? input = null,
        GuardRailFunction? guardRail = null,
        bool singleTurn = false,
        int maxTurns = 10,
        List<ChatMessage>? messagesToAppend = null,
        Func<AgentRunnerEvents, ValueTask>? runnerCallback = null,
        bool streaming = false,
        string? responseId = null,
        Func<string, ValueTask<bool>>? toolPermissionHandle = null,
        TornadoRunnerOptions? runnerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        return await RunAsyncInternal(agent, input is null ? null : [ new ChatMessagePart(input) ], guardRail, singleTurn, maxTurns, messagesToAppend, runnerCallback, streaming, responseId, toolPermissionHandle, runnerOptions, cancellationToken);
    }
    
    /// <summary>
    /// Invoke the agent loop to begin async
    /// </summary>
    /// <param name="agent">Agent to Run</param>
    /// <param name="input">Message to the Agent</param>
    /// <param name="guardRail">Input Guardrail To perform</param>
    /// <param name="singleTurn">Set loop to not loop</param>
    /// <param name="maxTurns">Max loops to perform</param>
    /// <param name="messagesToAppend"> Input messages to add to response</param>
    /// <param name="streaming">Enable streaming</param>
    /// <param name="runnerCallback">delegate to send event information </param>
    /// <param name="responseId">Previous Response ID from response API</param>
    /// <param name="cancellationToken">Cancellation token to cancel the run</param>
    /// <param name="toolPermissionHandle">Delegate to request tool permission from user</param>
    /// <returns>Result of the run</returns>
    /// <exception cref="GuardRailTriggerException">Triggers when Guardrail detects bad input</exception>
    /// <exception cref="Exception"> Max Turns Reached or Error</exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task<Conversation> RunAsync(
        TornadoAgent agent,
        List<ChatMessagePart>? input = null,
        GuardRailFunction? guardRail = null,
        bool singleTurn = false,
        int maxTurns = 10,
        List<ChatMessage>? messagesToAppend = null,
        Func<AgentRunnerEvents, ValueTask>? runnerCallback = null,
        bool streaming = false,
        string? responseId = null,
        Func<string, ValueTask<bool>>? toolPermissionHandle = null,
        TornadoRunnerOptions? runnerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        return await RunAsyncInternal(agent, input, guardRail, singleTurn, maxTurns, messagesToAppend, runnerCallback, streaming, responseId, toolPermissionHandle, runnerOptions, cancellationToken);
    }
    
    private static async Task<Conversation> RunAsyncInternal(
        TornadoAgent agent,
        List<ChatMessagePart>? input = null,
        GuardRailFunction? guardRail = null,
        bool singleTurn = false,
        int maxTurns = 10,
        List<ChatMessage>? messagesToAppend = null,
        Func<AgentRunnerEvents, ValueTask>? runnerCallback = null,
        bool streaming = false,
        string? responseId = null,
        Func<string, ValueTask<bool>>? toolPermissionHandle = null,
        TornadoRunnerOptions? runnerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        agent.Running = true;
        agent.Cancelled = false;

        try
        {
            runnerOptions ??= new TornadoRunnerOptions();
            Conversation conversation = SetupConversation(agent, input, messagesToAppend, responseId, runnerOptions, cancellationToken);

            if (runnerCallback is not null)
            {
                AgentRequestTokenTelemetry telemetry = await AgentTokenTelemetryCalculator.CalculatePreflightAsync(
                    agent,
                    conversation,
                    messagesToAppend,
                    responseId,
                    cancellationToken).ConfigureAwait(false);

                await runnerCallback.Invoke(new AgentRunnerRequestPreparedEvent(telemetry, conversation));
            }
        
            // check if the input triggers a guardrail to stop the agent from continuing
            await CheckInputGuardrail(agent, conversation, input, runnerOptions, guardRail, runnerCallback);

            return await RunAgentLoop(conversation, agent, runnerOptions, singleTurn, maxTurns, runnerCallback, streaming,  responseId, toolPermissionHandle,cancellationToken);
        }
        finally
        {
            agent.Running = false;
            agent.Cancelled = false;
        }
    }

    /// <summary>
    /// Invoke the agent loop to begin async without a agent defined
    /// </summary>
    /// <param name="api">Client with api key</param>
    /// <param name="model">Model to use</param> 
    /// <param name="input">Message to the Agent</param>
    /// <param name="guardRail">Input Guardrail To perform</param>
    /// <param name="singleTurn">Set loop to not loop</param>
    /// <param name="maxTurns">Max loops to perform</param>
    /// <param name="messagesToAppend"> Input messages to add to response</param>
    /// <param name="streaming">Enable streaming</param>
    /// <param name="runnerCallback">delegate to send event information </param>
    /// <param name="responseId">Previous Response ID from response API</param>
    /// <param name="cancellationToken">Cancellation token to cancel the run</param>
    /// <param name="toolPermissionHandle">Delegate to request tool permission from user</param>
    /// <returns>Result of the run</returns>
    /// <exception cref="GuardRailTriggerException">Triggers when Guardrail detects bad input</exception>
    /// <exception cref="Exception"> Max Turns Reached or Error</exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task<Conversation> RunAsync(
        TornadoApi api,
        ChatModel model,
        ChatRequest options,
        string input = "",
        string instructions = "You are a useful assistant",
        GuardRailFunction? guardRail = null,
        bool singleTurn = false,
        int maxTurns = 10,
        List<ChatMessage>? messagesToAppend = null,
        Func<AgentRunnerEvents, ValueTask>? runnerCallback = null,
        bool streaming = false,
        string responseId = "",
        Func<string, ValueTask<bool>>? toolPermissionHandle = null,
        TornadoRunnerOptions? runnerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        TornadoAgent agent = new TornadoAgent(api, model, instructions:instructions) { Options = options };

        return await RunAsync(agent, input, guardRail, singleTurn, maxTurns, messagesToAppend, runnerCallback, streaming, responseId, toolPermissionHandle, runnerOptions: runnerOptions, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Main Agent Loop running the functions calls 
    /// </summary>
    /// <param name="chat"></param>
    /// <param name="agent"></param>
    /// <param name="singleTurn"></param>
    /// <param name="maxTurns"></param>
    /// <param name="runnerCallback"></param>
    /// <param name="streaming"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="toolPermissionRequest"></param>
    /// <returns></returns>
    private static async Task<Conversation> RunAgentLoop(
        Conversation chat,
        TornadoAgent agent,
        TornadoRunnerOptions runnerOptions,
        bool singleTurn = false,
        int maxTurns = 10,
        Func<AgentRunnerEvents, ValueTask>? runnerCallback = null,
        bool streaming = false,
        string? responseId = null,
        Func<string, ValueTask<bool>>? toolPermissionRequest = null,
        CancellationToken cancellationToken = default
    )
    {     
        int turns = 0;
        agent.LastRunExitReason = null;

        if (runnerCallback is not null)
        {
            await runnerCallback.Invoke(new AgentRunnerStartedEvent(chat));   
        }

        // agentic loop
        try
        {
            do
            {
                if (await CheckForCancellation(agent, chat, runnerCallback, runnerOptions, cancellationToken))
                {
                    break;
                }

                if (await CheckForMaxTurns(agent, chat, turns, maxTurns, runnerCallback, runnerOptions))
                {
                    break;
                }

                if (await CheckForMaxTokens(agent, chat, chat.Messages.Sum(TokenEstimator.EstimateTokens), runnerCallback, runnerOptions))
                {
                    break;
                }

                turns++;
                chat = await GetNewResponse(agent, chat, runnerOptions, streaming, runnerCallback, toolPermissionRequest, cancellationToken);
            } while (GotToolCall(chat) && !singleTurn);
        }
        catch (Exception ex)
        {
            if (runnerCallback is not null)
            {
                await runnerCallback.Invoke(new AgentRunnerErrorEvent(ex.Message, chat, ex));
            }

            agent.LastRunExitReason = new ErrorExitReason(ex.Message, ex);

            throw;
        }

        if (runnerCallback is not null)
        {
            await runnerCallback.Invoke(new AgentRunnerCompletedEvent(chat));    
        }

        if(agent.LastRunExitReason == null)
        {
            agent.LastRunExitReason = new CompletedExitReason();
        }
        
        return chat;
    }

    // [consideration] Feeling very off about using setup here or maintaining the conversation within the agent class
    // Depends on if we want to keep the agent stateless or not
    /// <param name="agent"></param>
    /// <param name="input"></param>
    /// <param name="messages"></param>
    /// <param name="responseId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private static Conversation SetupConversation(TornadoAgent agent, List<ChatMessagePart>? input = null, List<ChatMessage>? messages = null, string? responseId = null, TornadoRunnerOptions? runnerOptions = null, CancellationToken cancellationToken = default)
    {
        Conversation chat = agent.Client.Chat.CreateConversation(agent.Options);

        //Set the cancellation token for the agent client
        chat.RequestParameters.CancellationToken = cancellationToken;

        bool sysMesageAtStart = runnerOptions?.SystemMessageAtStart ?? true; // Default true for system message at the start.

        if (sysMesageAtStart)
        {
            chat.AddSystemMessage(agent.Instructions); //Set the instructions for the agent at the start of the conversation
        }

        //Setup the messages from previous runs or memory
        chat = AddMessagesToConversation(chat, messages);

        //Add the latest message to the stream
        if (input?.Count > 0)
        {
            chat.AppendUserInput(input);
        }

        //Set response id
        if (!string.IsNullOrEmpty(responseId))
        {
            chat.RequestParameters.ResponseRequestParameters ??= new ResponseRequest();
            chat.RequestParameters.ResponseRequestParameters.PreviousResponseId = responseId;
        }

        //Setting up system message at the end.
        if (!sysMesageAtStart)
        {
            chat.AddSystemMessage(agent.Instructions); //Set the instructions for the agent
        }

        return chat;
    }

    private static Conversation AddMessagesToConversation(Conversation chat, List<ChatMessage>? messages = null)
    {
        if (messages == null) return chat;

        foreach (ChatMessage message in messages)
        {
            if (message.Role == ChatMessageRoles.System) continue; //Skip system messages if any to avoid Instruction overlap
            chat.AppendMessage(message);
        }

        return chat;
    }

    private static async Task CheckInputGuardrail(TornadoAgent agent, Conversation chat, List<ChatMessagePart>? input, TornadoRunnerOptions runnerOptions, GuardRailFunction? guardRail, Func<AgentRunnerEvents, ValueTask>? runnerCallback = null)
    {
        if (guardRail != null)
        {
            GuardRailFunctionOutput? result = (GuardRailFunctionOutput?)(await AsyncHelpers.InvokeValueTaskFuncAsync(guardRail, [input?.FirstOrDefault()?.Text]));

            if (result is { TripwireTriggered: true })
            {
                GuardRailTriggerException triggerException = new GuardRailTriggerException($"Input Guardrail Stopped the agent from continuing because, {result.OutputInfo}");

                if (runnerCallback is not null)
                {
                    await runnerCallback.Invoke(new AgentRunnerGuardrailTriggeredEvent(chat, $"Input Guardrail Stopped the agent from continuing because, {result.OutputInfo}"));
                    await runnerCallback.Invoke(new AgentRunnerErrorEvent(triggerException.Message, chat, triggerException));
                }

                agent.LastRunExitReason = new InputGuardRailTriggeredExitReason(triggerException.Message, triggerException);

                throw triggerException;
            }
        }
    }

    private static async ValueTask<bool> CheckForCancellation(TornadoAgent agent, Conversation chat, Func<AgentRunnerEvents, ValueTask>? runnerCallback = null, TornadoRunnerOptions? runnerOptions = null, CancellationToken cancellationToken = default)
    {
        if (agent.Cancelled)
        {
            return true;
        }
        
        if (cancellationToken.IsCancellationRequested)
        {
            agent.LastRunExitReason = new CancelledExitReason("Operation was cancelled by user.");

            OperationCanceledException ex = new OperationCanceledException("Operation was cancelled by user.");

            if (runnerCallback is not null)
            {
                await runnerCallback.Invoke(new AgentRunnerCancelledEvent(chat));
                await runnerCallback.Invoke(new AgentRunnerErrorEvent(ex.Message, chat, ex));
            }

            if (runnerOptions?.ThrowOnCancelled ?? false)
            {
                throw ex;
            }
            
            return true;   
        }
        
        return false;
    }

    private static async ValueTask<bool> CheckForMaxTurns(TornadoAgent agent, Conversation chat, int currentTurn, int maxTurns, Func<AgentRunnerEvents, ValueTask>? runnerCallback = null, TornadoRunnerOptions? runnerOptions = null)
    {
        if (currentTurn < maxTurns)
        {
            return false;
        }

        Exception error = new Exception("Max Turns Reached");

        agent.LastRunExitReason = new MaxTurnsReachedExitReason();

        if (runnerCallback is not null)
        {
            await runnerCallback.Invoke(new AgentRunnerMaxTurnsReachedEvent(chat));
            await runnerCallback.Invoke(new AgentRunnerErrorEvent(error.Message, chat, error));
        }

        if (runnerOptions?.ThrowOnMaxTurnsExceeded ?? false)
        {
            throw error;
        }

        return true;
    }

    private static async ValueTask<bool> CheckForMaxTokens(TornadoAgent agent, Conversation chat, int currentTokens, Func<AgentRunnerEvents, ValueTask>? runnerCallback = null, TornadoRunnerOptions? runnerOptions = null)
    {
        if (runnerOptions?.TokenLimit is null || currentTokens < runnerOptions.TokenLimit)
        {
            return false;
        }
        
        Exception error = new Exception("Max Tokens Reached");

        if (runnerCallback is not null)
        {
            await runnerCallback.Invoke(new AgentRunnerMaxTokensReachedEvent(chat));
            await runnerCallback.Invoke(new AgentRunnerErrorEvent(error.Message, chat, error));    
        }

        agent.LastRunExitReason = new MaxTokensReachedExitReason();

        return runnerOptions?.ThrowOnTokenLimitExceeded ?? true ? throw error : true;
    }

    private static bool GotToolCall(Conversation chat)
    {
        return CheckForChatToolCall(chat);
    }

    static ChatMessage? LastMessage(Conversation chat)
    {
        return chat.Messages.Count > 0 ? chat.Messages[^1] : null;
    }

    private static bool CheckForChatToolCall(Conversation chat)
    {
        return LastMessage(chat) is { Role: ChatMessageRoles.Tool };
    }

    /// <summary>
    /// Handle Tool calls from Agent loop
    /// </summary>
    /// <param name="agent"></param>
    /// <param name="toolCall"></param>
    /// <param name="toolPermissionHandle">Request tool permission handle</param>
    /// <returns></returns>
    private static async Task<FunctionResult> HandleToolCall(TornadoAgent agent, FunctionCall toolCall, TornadoRunnerOptions runnerOptions, Func<string, ValueTask<bool>>? toolPermissionHandle = null, CancellationToken cancellationToken = default)
    {
        bool permissionGranted = true;
        FunctionResult functionResult = new FunctionResult(toolCall, "No Result", FunctionResultSetContentModes.Passthrough);

        //Safety check for tool permission if required by the agent for the specific tool being called, if permission handle is provided
        if (toolPermissionHandle != null)
        {
            if (agent.ToolPermissionRequired.TryGetValue(toolCall.Name, out bool requiresPermission) && requiresPermission)
            {
                //If tool permission is required, ask user for permission
                string requestMessage = $"Tool: {toolCall.Name}";
                if (!string.IsNullOrEmpty(toolCall.Arguments))
                {
                    requestMessage += $"\nArguments: {toolCall.Arguments}";
                }
                permissionGranted = await toolPermissionHandle.Invoke(requestMessage);
            }
        }

        if (!permissionGranted)
        {
            //If permission is not granted, remove the tool call from the request
            functionResult = new FunctionResult(toolCall, "Tool Permission was not granted by user", FunctionResultSetContentModes.Passthrough);
        }
        else
        {
            try
            {
                if (agent.McpTools.ContainsKey(toolCall.Name))
                {
                    functionResult = await ToolRunner.CallMcpToolAsync(agent, toolCall);
                }
                else
                {
                    functionResult = await ToolRunner.CallFuncToolAsync(agent, toolCall, cancellationToken);
                }
            }
            catch (Exception e)
            {
                agent.LastRunExitReason = new ToolErrorExitReason(toolCall.Name, exception:e);
                if (runnerOptions?.ThrowOnToolError ?? false)
                {
                    throw new Exception($"Error occurred while calling tool {toolCall.Name}: {e.Message}", e);
                }
                else
                {
                    functionResult = new FunctionResult(toolCall, $"Error occurred while calling tool {toolCall.Name}: {e.Message}", FunctionResultSetContentModes.Passthrough);
                }
            }
        }

        return functionResult;
    }

    private static async Task<Conversation> CheckForToolCallsAndHandle(Conversation chat, TornadoAgent agent, TornadoRunnerOptions runnerOptions, Func<AgentRunnerEvents, ValueTask>? runnerCallback = null, Func<string, ValueTask<bool>>? toolPermissionRequest = null, CancellationToken cancellationToken = default)
    {
        List<ToolCall>? calls = chat.Messages.Count is 0 ? null : chat.Messages[^1].ToolCalls;

        if (calls is not null)
        {
            foreach (ToolCall tc in calls)
            {
                if (tc.FunctionCall is null)
                {
                    continue;
                }

                FunctionCall fn = tc.FunctionCall;

                if (runnerCallback is not null)
                {
                    await runnerCallback.Invoke(new AgentRunnerToolInvokedEvent(fn, chat));    
                }

                fn.Result = await HandleToolCall(agent, fn, runnerOptions, toolPermissionRequest, cancellationToken); //[consideration]I could go parallel here but not sure if its worth the complexity

                if (runnerCallback is not null)
                {
                    await runnerCallback.Invoke(new AgentRunnerToolCompletedEvent(fn, chat));
                }
            }
        }

        return chat;
    }

    private static void RefreshToolsForNextTurn(TornadoAgent agent, Conversation chat)
    {
        // we want to refresh the tools for the next turn in case they are dynamic or have updated information
       //dynamically set the tools for the request
        if (agent.Options.Tools != null)
        {
            chat.RequestParameters.Tools = agent.Options.Tools;
        }

        if (agent.ResponseOptions?.Tools != null)
        {
            chat.RequestParameters.ResponseRequestParameters?.Tools = agent.ResponseOptions.Tools;
        }
    }

    /// <summary>
    /// Get response from the model or If Error delete last message in thread and retry (max agent loops will cap)
    /// </summary>
    /// <param name="agent">Agent to respond</param>
    /// <param name="chat">Current Conversation</param>
    /// <param name="Streaming">Should we stream the response</param>
    /// <param name="runnerCallback">Callback events</param>
    /// <param name="toolPermissionRequest">Request Tool permissions</param>
    /// <returns></returns>
    private static async Task<Conversation> GetNewResponse(
        TornadoAgent agent,
        Conversation chat,
        TornadoRunnerOptions runnerOptions,
        bool Streaming = false,
        Func<AgentRunnerEvents, ValueTask>? runnerCallback = null,
        Func<string, ValueTask<bool>>? toolPermissionRequest = null,
        CancellationToken cancellationToken = default
        )
    {
            // we handle this ourselves
            chat.RequestParameters.InvokeClrToolsAutomatically = false;

            RefreshToolsForNextTurn(agent, chat);

            chat = await CheckForToolCallsAndHandle(chat, agent, runnerOptions, runnerCallback, toolPermissionRequest, cancellationToken);
        try{
            if (Streaming && runnerCallback != null)
            {
                return await HandleStreaming(agent, chat, runnerOptions, runnerCallback, toolPermissionRequest, cancellationToken);
            }
            
            RestDataOrException<ChatRichResponse> response = await chat.GetResponseRichSafe(async functions =>
            {
                List<Task> tasks = [];
                
                foreach (FunctionCall fn in functions)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        if (runnerCallback is not null)
                        {
                            await runnerCallback.Invoke(new AgentRunnerToolInvokedEvent(fn, chat));
                        }
                        
                        // guard against double execution
                        fn.Result ??= await HandleToolCall(agent, fn, runnerOptions, toolPermissionRequest, cancellationToken);

                        if (runnerCallback is not null)
                        {
                            await runnerCallback.Invoke(new AgentRunnerToolCompletedEvent(fn, chat));
                        }
                    }));
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }, cancellationToken);


            if (response.Exception != null)
            {
                agent.LastRunExitReason = new ResponseErrorExitReason(response.Exception);

                if(runnerCallback is not null) 
                {
                    await runnerCallback.Invoke(new AgentRunnerErrorEvent(response.Exception.Message, chat, response.Exception));
                }

                if (runnerOptions?.ThrowOnResponseError ?? false)
                {
                    throw response.Exception;
                }

                return chat;
            }

            if (runnerCallback is not null && response is { Exception: null })
            {
                await runnerCallback.Invoke(new AgentRunnerUsageReceivedEvent(response.Data?.Usage, chat));
            }
        }
        catch (Exception ex)
        {
            agent.LastRunExitReason = new RequestErrorExitReason(ex);

            if (runnerCallback is not null)
            {
                await runnerCallback.Invoke(new AgentRunnerErrorEvent(ex.Message, chat, ex));
            }

            if(runnerOptions?.ThrowOnRequestError ?? false)
            {
               throw;
            }
        }

        return chat;
    }

    //[consideration] Need to massively improve this to handle all the streaming events
    private static async Task<Conversation> HandleStreaming(TornadoAgent agent, Conversation chat, TornadoRunnerOptions runnerOptions, Func<AgentRunnerEvents, ValueTask>? runnerCallback = null, Func<string, ValueTask<bool>>? toolPermissionRequest = null, CancellationToken cancellationToken = default)
    {
        //Create Open response
        await chat.StreamResponseRich(new ChatStreamEventHandler
        {
            MessageTokenExHandler = (exText) =>
            {
                //Call the streaming callback for text
                return Threading.ValueTaskCompleted;
            },
            MessageTokenHandler = async (text) =>
            {
                if (runnerCallback is not null)
                {
                    await runnerCallback.Invoke(new AgentRunnerStreamingEvent(new ModelStreamingOutputTextDeltaEvent(1, 1, 1, text ?? string.Empty), chat));   
                }
            },
            ReasoningTokenHandler = async (reasoning) =>
            {
                if (runnerCallback is not null)
                {
                    await runnerCallback.Invoke(new AgentRunnerStreamingEvent(new ModelStreamingReasoningPartAddedEvent(1, 1, 1, reasoningText: reasoning.Content ?? string.Empty), chat));
                }
            },
            FunctionCallDeltaHandler = async (update) =>
            {
                if (runnerCallback is not null)
                {
                    await runnerCallback.Invoke(new AgentRunnerStreamingEvent(
                        new ModelStreamingFunctionCallDeltaEvent(
                            1,
                            update.Name,
                            update.ArgumentsDelta,
                            update.ArgumentsSnapshot,
                            update.CallId,
                            update.Index,
                            update.IsComplete),
                        chat));
                }
            },
            BlockFinishedHandler = (message) =>
            {
                //Call the streaming callback for completion
                return Threading.ValueTaskCompleted;
            },
            MessagePartHandler = (part) =>
            {
                //Need to handle other modalities here like images/audio don't have classes for them yet
                return Threading.ValueTaskCompleted;
            },
            OnResponseEvent = async (response) =>
            {
                if (runnerCallback is not null)
                {
                    await runnerCallback.Invoke(new AgentRunnerResponseApiEvent(response, chat));
                }
            },
            FunctionCallHandler = async (toolCall) =>
            {
                //Add the tool call to the response output
                foreach (FunctionCall fn in toolCall)
                {
                    if (runnerCallback is not null)
                    {
                        await runnerCallback.Invoke(new AgentRunnerToolInvokedEvent(fn, chat));
                        fn.Result ??= await HandleToolCall(agent, fn, runnerOptions, toolPermissionRequest, cancellationToken); //I could go parallel here but not sure if its worth the complexity
                        await runnerCallback.Invoke(new AgentRunnerToolCompletedEvent(fn, chat));   
                    }
                }
            },
            MessageTypeResolvedHandler = (messageType) =>
            {
                return Threading.ValueTaskCompleted;
            },
            MutateChatRequestHandler = async (request) =>
            {
                if (runnerCallback is not null)
                {
                    await runnerCallback.Invoke(new AgentRunnerStreamingEvent(new ModelStreamingCreatedEvent(1), chat));
                }
                
                return request;
            },
            HttpExceptionHandler = async (exception) =>
            {
                if (runnerCallback is not null)
                {
                    await runnerCallback.Invoke(new AgentRunnerErrorEvent(exception.Exception.Message, chat, exception.Exception));
                }

                if(runnerOptions?.ThrowOnResponseError ?? false) 
                {
                    agent.LastRunExitReason = new ResponseErrorExitReason(exception.Exception);
                    throw exception.Exception;
                }
            },
            OnUsageReceived = async (usage) =>
            {
                if (runnerCallback is not null)
                {
                    await runnerCallback.Invoke(new AgentRunnerUsageReceivedEvent(usage, chat));   
                }
            },
            OutboundHttpRequestHandler = (http) =>
            {
                return Threading.ValueTaskCompleted;
            },
            OnFinished = (finishedData) =>
            {
                return Threading.ValueTaskCompleted;
            }
        }, cancellationToken);

        if (runnerCallback is not null)
        {
            await runnerCallback.Invoke(new AgentRunnerStreamingEvent(new ModelStreamingCompletedEvent(1), chat));
        }
        
        return chat;
    }
}
