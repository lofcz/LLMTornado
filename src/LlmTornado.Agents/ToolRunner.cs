using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Infra;

namespace LlmTornado.Agents;

/// <summary>
/// Class to Invoke the tools during run
/// </summary>
public static class ToolRunner
{
    /// <summary>
    /// Normalizes arguments string for JSON deserialization.
    /// Returns "{}" for null, empty, whitespace, literal "null", "undefined", or empty array "[]".
    /// This handles edge cases where LLMs send null, empty string, or omit arguments entirely.
    /// </summary>
    public static string NormalizeArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return "{}";
        }
        
        string trimmed = arguments.Trim();
        
        // Handle JSON null literal, undefined, and empty array
        if (trimmed is "null" or "undefined" or "[]")
        {
            return "{}";
        }
        
        return arguments;
    }

    /// <summary>
    /// Process tool result through agent's ToolResultProcessor if configured
    /// </summary>
    private static async ValueTask<FunctionResult> ProcessToolResult(TornadoAgent agent, FunctionCall call, FunctionResult result)
    {
        if (agent.ToolResultProcessor != null && result != null)
        {
            await agent.ToolResultProcessor(call.Name, result, call);
        }
        return result;
    }
    
    /// <summary>
    /// Invoke function from FunctionCallItem and return FunctionOutputItem
    /// </summary>
    /// <param name="agent"></param>
    /// <param name="call"></param>
    /// <param name="cancellationToken">Passed to a delegate parameter of type <see cref="CancellationToken"/>, if the tool declares one.</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<FunctionResult> CallFuncToolAsync(TornadoAgent agent, FunctionCall call, CancellationToken cancellationToken = default)
    {
        if (!agent.ToolList.TryGetValue(call.Name, out Tool? tool))
        {
            throw new Exception($"I don't have a tool called {call.Name}");
        }
        
        if (tool?.Delegate is not null)
        {
            string normalizedArgs = NormalizeArguments(call.Arguments);
            MethodInvocationResult invocationResult = await call.Invoke(normalizedArgs, cancellationToken).ConfigureAwait(false);
            FunctionResult result = call.Result ?? (invocationResult.InvocationSuccessful
                ? new FunctionResult(call, new
                {
                    result = "ok"
                })
                : new FunctionResult(call, new
                {
                    error = invocationResult.InvocationException?.Message,
                }, false));
            
            return await ProcessToolResult(agent, call, result);
        }

        return new FunctionResult(call, "Error No Delegate found");
    }

    private static string GetInputFromFunctionArgs(string? args)
    {
        string normalizedArgs = NormalizeArguments(args);
        string errorMessage = string.Empty;
        try
        {
            using JsonDocument argumentsJson = JsonDocument.Parse(normalizedArgs);
            if (argumentsJson.RootElement.TryGetProperty("input", out JsonElement jValue))
            {
                return jValue.GetString() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        
        return $"Error Could not deserialize json argument Input from last function call ERROR: {errorMessage}";
    }

    /// <summary>
    /// Calls the MCP tool and returns the result
    /// </summary>
    /// <param name="agent">The agent invoking the tool</param>
    /// <param name="call">The function call containing the arguments</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    /// <exception cref="System.Text.Json.JsonException"></exception>
    public static async Task<FunctionResult> CallMcpToolAsync(TornadoAgent agent, FunctionCall call)
    {
        if (!agent.McpTools.TryGetValue(call.Name, out Tool? tool))
            throw new Exception($"I don't have a tool called {call.Name}");

        string normalizedArgs = NormalizeArguments(call.Arguments);
        Dictionary<string, object?>? dict;

        try
        {
            dict = JsonConvert.DeserializeObject<Dictionary<string, object?>>(normalizedArgs);
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            throw new System.Text.Json.JsonException($"Function arguments for {call.Name} are not valid JSON: {call.Arguments}", ex);
        }

        // call the tool on MCP server, pass args
        await call.ResolveRemote(dict);

        // extract tool result and pass it back to the model
        if (call.Result?.RemoteContent is McpContent mcpContent)
        {
            List<string> texts = [];
            
            foreach (IMcpContentBlock block in mcpContent.McpContentBlocks)
            {
                if (block is McpContentBlockText textBlock)
                {
                    texts.Add(textBlock.Text);
                }
            }

            if (texts.Count > 0)
            {
                call.Result.Content = string.Join("\n", texts);
            }
        }
        
        FunctionResult result = await ProcessToolResult(agent, call, call.Result);
        return result;
    }
}