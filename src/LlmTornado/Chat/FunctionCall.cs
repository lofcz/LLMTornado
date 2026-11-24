using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Infra;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.ChatFunctions;

/// <summary>
/// Built-in tool call
/// </summary>
public class BuiltInToolCall
{
    /// <summary>
    /// The associated data.
    /// </summary>
    public IBuiltInToolCallData Data { get; set; }
        
    /// <summary>
    ///     The full tool call object.
    /// </summary>
    [JsonIgnore]
    public ToolCall? ToolCall { get; set; }

    /// <summary>
    ///     The name of the function.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    ///     Any arguments that need to be passed to the function. This needs to be in JSON format.
    /// </summary>
    [JsonProperty("arguments")]
    public string? Arguments { get; set; }
    
    /// <summary>
    /// Whether a response is expected or not.
    /// </summary>
    [JsonIgnore]
    public bool ResponseExpected { get; set; }
    
    /// <summary>
    /// Crates an instance of the built-in tool call.
    /// </summary>
    public BuiltInToolCall(bool responseExpected, string name, IBuiltInToolCallData data, ToolCall toolCall, string? args = null)
    {
        Data = data;
        Name = name;
        Arguments = args;
        ToolCall = toolCall;
        ResponseExpected = responseExpected;
    } 
}

/// <summary>
/// Shared interface for built-in tool calls.
/// </summary>
public interface IBuiltInToolCallData
{
    
}

/// <summary>
/// Empty data for built-in tools.
/// </summary>
public class DummyBuiltInToolCallData : IBuiltInToolCallData
{
    /// <summary>
    /// Static instance.
    /// </summary>
    public static readonly DummyBuiltInToolCallData Inst = new DummyBuiltInToolCallData();
}

/// <summary>
///     Result of tool call of type "custom".
/// </summary>
public class CustomToolCall
{
    /// <summary>
    ///     The name of the custom tool.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = null!;

    /// <summary>
    ///     Input of the custom call, "arguments".
    /// </summary>
    [JsonProperty("input")]
    public string Input { get; set; } = null!;
    
    /// <summary>
    ///     The full tool call object.
    /// </summary>
    [JsonIgnore]
    public ToolCall? ToolCall { get; set; }
    
    /// <summary>
    ///     Result of the call.
    /// </summary>
    [JsonIgnore]
    public CustomToolCallResult? Result { get; set; }
    
    /// <summary>
    /// Resolves the call.
    /// </summary>
    public CustomToolCall Resolve(object? result)
    {
        Result = new CustomToolCallResult(this, result);
        return this;
    }
}

/// <summary>
///     Result of tool call of type "function".
/// </summary>
public class FunctionCall
{
    /// <summary>
    ///     Arguments.
    /// </summary>
    [JsonIgnore] 
    internal readonly Lazy<ChatFunctionParamsGetter> ArgGetter;

    /// <summary>
    ///     Decoded arguments.
    /// </summary>
    [JsonIgnore] 
    internal readonly Lazy<Dictionary<string, object?>?> DecodedArguments;

    /// <summary>
    ///     Creates an empty function call.
    /// </summary>
    public FunctionCall()
    {
        ArgGetter = new Lazy<ChatFunctionParamsGetter>(() => new ChatFunctionParamsGetter(DecodedArguments?.Value));
        DecodedArguments = new Lazy<Dictionary<string, object?>?>(() => Arguments.IsNullOrWhiteSpace() ? [] : JsonConvert.DeserializeObject<Dictionary<string, object?>>(Arguments));
    }

    internal FunctionCall(Dictionary<string, object?> arguments)
    {
        Arguments = arguments.ToJson();
        ArgGetter = new Lazy<ChatFunctionParamsGetter>(() => new ChatFunctionParamsGetter(DecodedArguments?.Value));
        DecodedArguments = new Lazy<Dictionary<string, object?>?>(() => Arguments.IsNullOrWhiteSpace() ? [] : JsonConvert.DeserializeObject<Dictionary<string, object?>>(Arguments));
    }

    internal FunctionCall(string arguments)
    {
        Arguments = arguments;
        ArgGetter = new Lazy<ChatFunctionParamsGetter>(() => new ChatFunctionParamsGetter(DecodedArguments?.Value));
        DecodedArguments = new Lazy<Dictionary<string, object?>?>(() => Arguments.IsNullOrWhiteSpace() ? [] : JsonConvert.DeserializeObject<Dictionary<string, object?>>(Arguments));
    }
    
    /// <summary>
    /// Tool this call is linked to. This property might not be set outside remote tools (MCP).
    /// </summary>
    [JsonIgnore]
    public Tool? Tool { get; set; }
    
    [JsonIgnore] 
    private string? JsonEncoded { get; set; }

    /// <summary>
    ///     The name of the function.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    ///     Any arguments that need to be passed to the function. This needs to be in JSON format.
    /// </summary>
    [JsonProperty("arguments")]
    public string? Arguments { get; set; }
    
    /// <summary>
    ///     The result of the function. This is resolved by the API consumer.
    /// </summary>
    [JsonIgnore]
    public FunctionResult? Result { get; set; }
    
    /// <summary>
    ///     If a delegate is attached to the <see cref="Tool"/>, this property holds the last result of the delegate invocation.
    /// </summary>
    [JsonIgnore]
    public MethodInvocationResult? LastInvocationResult { get; set; }
    
    /// <summary>
    ///     The full tool call object.
    /// </summary>
    [JsonIgnore]
    public ToolCall? ToolCall { get; set; }

    /// <summary>
    ///     Gets all arguments passed to the function call as a dictionary.
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, object?> GetArguments()
    {
        return ArgGetter.Value.Source ?? [];
    }

    /// <summary>
    /// Gets the specified argument or default value.
    /// </summary>
    public T? GetOrDefault<T>(string param, T? defaultValue = default)
    {
        return Get(param, out T? data, out _) ? data : defaultValue;
    }

    /// <summary>
    /// Gets the specified argument. If the conversion to T fails, the exception is ignored.
    /// </summary>
    /// <param name="param">Key</param>
    /// <param name="data">Type to which the argument should be converted.</param>
    public bool Get<T>(string param, out T? data)
    {
        return Get(param, out data, out _);
    }
    
    /// <summary>
    /// Gets the specified argument. If the conversion to T fails, the exception is ignored.
    /// </summary>
    /// <param name="param">Key</param>
    /// <param name="data">Type to which the argument should be converted.</param>
    public bool TryGetArgument<T>(string param, [NotNullWhen(true)] out T? data)
    {
        return Get(param, out data, out _);
    }

    /// <summary>
    /// Gets the specified argument.
    /// </summary>
    /// <param name="param">Key</param>
    /// <param name="data">Type to which the argument should be converted.</param>
    /// <param name="exception">If the conversion fails, the exception is returned here.</param>
    public bool Get<T>(string param, out T? data, out Exception? exception)
    {
        return Clr.Get(param, GetArguments(), out data, out exception);
    }
    
    /// <summary>
    ///     Gets the json encoded function call, this is cached to avoid serializing the function over and over.
    /// </summary>
    /// <returns></returns>
    public string GetJson()
    {
        return JsonEncoded ??= JsonConvert.SerializeObject(this, EndpointBase.NullSettings);
    }

    /// <summary>
    /// Resolves the call.
    /// </summary>
    public FunctionCall Resolve(object? result)
    {
        Result = new FunctionResult(this, result);
        return this;
    }
    
    /// <summary>
    /// Resolves the call by asynchronously invoking the attached delegate with given JSON data.
    /// </summary>
    public async ValueTask<MethodInvocationResult> Invoke(string data, CancellationToken cancellationToken = default)
    {
        if (Tool is null)
        {
            return new MethodInvocationResult(new Exception("Tool is null, nothing to invoke"));
        }

        MethodInvocationResult invocationResult = await Clr.Invoke(Tool.Delegate, Tool.DelegateMetadata, data, cancellationToken).ConfigureAwait(false);

        if (invocationResult.InvocationException is null)
        {
            if (invocationResult.Result is IEnumerable<IFunctionResultBlock> blocks)
            {
                Result = new FunctionResult(this, blocks.ToList());
            }
            else
            {
                Result = new FunctionResult(this, invocationResult.Result as string ?? (Tool.Metadata?.KeepNullsInResult is true && invocationResult.Result is not null ? JsonConvert.SerializeObject(invocationResult.Result, Formatting.None) : invocationResult.Result.ToJson()), FunctionResultSetContentModes.Passthrough);
            }
        }

        LastInvocationResult = invocationResult;
        return invocationResult;
    }

    /// <summary>
    /// Resolves the call.<br/>
    /// <remarks>
    /// Rich blocks from tool calls are currently supported only by Anthropic. If you use other providers, use only one block of type <see cref="FunctionResultBlockText"/>, or use the overload accepting an arbitrary object to avoid double JSON encoding.
    /// </remarks>
    /// </summary>
    public FunctionCall Resolve(List<IFunctionResultBlock> blocks)
    {
        Result = new FunctionResult(this, blocks);
        return this;
    }

    /// <summary>
    /// Executes the attached <see cref="IRemoteTool"/> - call this only for tools sourced from an MCP connection.
    /// </summary>
    public async ValueTask<FunctionCall> ResolveRemote(object? args = null, IProgress<ToolCallProgress>? progress = null, JsonSerializerOptions? serializerOptions = null, bool fillContent = true, CancellationToken cancellationToken = default)
    {
        if (Tool?.RemoteTool is null)
        {
            Result = new FunctionResult
            {
                InvocationSucceeded = false,
                Content = "Prototype or Prototype.RemoteTool is null, cannot call the tool."
            };
            
            return this;
        }
        
        Result = await Tool.RemoteTool.CallAsync.Invoke(args?.ToDictionary(), progress, serializerOptions, fillContent, cancellationToken);
        return this;
    }
    
    /// <summary>
    /// Resolves the call.
    /// </summary>
    public FunctionCall Resolve(object? result, bool invocationSucceeded)
    {
        Result = new FunctionResult(this, result, invocationSucceeded);
        return this;
    }
    
    /// <summary>
    /// Resolves the call.
    /// </summary>
    public FunctionCall Resolve(object? result, object? passthroughData)
    {
        Result = new FunctionResult(this, result, passthroughData);
        return this;
    }
    
    /// <summary>
    /// Resolves the call.
    /// </summary>
    public FunctionCall Resolve(object? result, object? passthroughData, bool invocationSucceeded)
    {
        Result = new FunctionResult(this, result, passthroughData, invocationSucceeded);
        return this;
    }
}
