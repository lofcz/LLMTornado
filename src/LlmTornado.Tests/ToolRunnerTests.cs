using LlmTornado.Agents;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using LlmTornado.Code.Vendor;
using LlmTornado.Common;
using LlmTornado.Infra;
using Newtonsoft.Json;
using NUnit.Framework;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace LlmTornado.Tests;

/// <summary>
/// Tests for ToolRunner tool invocation with various argument scenarios.
/// Covers edge cases where LLMs send null, empty, or whitespace arguments.
/// </summary>
[TestFixture]
public class ToolRunnerTests
{
    #region Test Tools - Simulating Real Tool Signatures

    [Description("Returns a non-string object")]
    public static object GetTemperature()
    {
        return new { temperature = 21 };
    }

    /// <summary>
    /// Tool with no parameters - like MCP's list_allowed_directories
    /// </summary>
    [Description("Lists all allowed directories")]
    public static string ListAllowedDirectories()
    {
        return "[\"C:\\\\Users\", \"C:\\\\Projects\"]";
    }

    /// <summary>
    /// Tool with optional parameters only
    /// </summary>
    [Description("Gets current time with optional timezone")]
    public static string GetCurrentTime(string? timezone = null)
    {
        return timezone == null ? "12:00 UTC" : $"12:00 {timezone}";
    }

    /// <summary>
    /// Tool with required parameter
    /// </summary>
    [Description("Searches for a query")]
    public static string Search(string query)
    {
        return $"Results for: {query}";
    }

    /// <summary>
    /// Tool with mixed required and optional parameters
    /// </summary>
    [Description("Gets weather for a location")]
    public static string GetWeather(double latitude, double longitude, string? units = "metric")
    {
        return $"Weather at {latitude},{longitude} in {units}: Sunny 25°";
    }

    #endregion

    #region NormalizeArguments Unit Tests

    [Test]
    public void NormalizeArguments_NullInput_ReturnsEmptyObject()
    {
        string result = ToolRunner.NormalizeArguments(null);
        Assert.That(result, Is.EqualTo("{}"));
    }

    [Test]
    public void NormalizeArguments_EmptyString_ReturnsEmptyObject()
    {
        string result = ToolRunner.NormalizeArguments("");
        Assert.That(result, Is.EqualTo("{}"));
    }

    [Test]
    public void NormalizeArguments_WhitespaceOnly_ReturnsEmptyObject()
    {
        string result = ToolRunner.NormalizeArguments("   ");
        Assert.That(result, Is.EqualTo("{}"));
    }

    [Test]
    public void NormalizeArguments_LiteralNull_ReturnsEmptyObject()
    {
        // LLMs sometimes send the literal string "null"
        string result = ToolRunner.NormalizeArguments("null");
        Assert.That(result, Is.EqualTo("{}"));
    }

    [Test]
    public void NormalizeArguments_LiteralUndefined_ReturnsEmptyObject()
    {
        string result = ToolRunner.NormalizeArguments("undefined");
        Assert.That(result, Is.EqualTo("{}"));
    }

    [Test]
    public void NormalizeArguments_EmptyArray_ReturnsEmptyObject()
    {
        string result = ToolRunner.NormalizeArguments("[]");
        Assert.That(result, Is.EqualTo("{}"));
    }

    [Test]
    public void NormalizeArguments_ValidEmptyObject_ReturnsOriginal()
    {
        string result = ToolRunner.NormalizeArguments("{}");
        Assert.That(result, Is.EqualTo("{}"));
    }

    [Test]
    public void NormalizeArguments_ValidJsonWithProperties_ReturnsOriginal()
    {
        string input = "{\"query\":\"test\"}";
        string result = ToolRunner.NormalizeArguments(input);
        Assert.That(result, Is.EqualTo(input));
    }

    #endregion

    #region Non-string delegate results

    [Test]
    public async Task CallFuncToolAsync_ObjectResult_ContentIsDelegateValueWithoutWrapper()
    {
        TornadoAgent agent = CreateTestAgent(GetTemperature);
        FunctionCall call = CreateFunctionCall("GetTemperature", null, agent);

        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        Assert.That(result.InvocationSucceeded, Is.True);
        Assert.That(result.Content, Is.EqualTo("{\"temperature\":21}"));
    }

    #endregion

    #region CallFuncToolAsync Integration Tests

    [Test]
    public async Task CallFuncToolAsync_NoArgTool_WithNullArguments_Succeeds()
    {
        // Arrange - Simulate LLM calling list_allowed_directories with null args
        TornadoAgent agent = CreateTestAgent(ListAllowedDirectories);
        FunctionCall call = CreateFunctionCall("ListAllowedDirectories", null, agent);

        // Act
        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.InvocationSucceeded, Is.True);
        Assert.That(result.Content, Does.Contain("C:\\\\Users"));
    }

    [Test]
    public async Task CallFuncToolAsync_NoArgTool_WithEmptyStringArguments_Succeeds()
    {
        // Arrange - LLM sends "" as arguments
        TornadoAgent agent = CreateTestAgent(ListAllowedDirectories);
        FunctionCall call = CreateFunctionCall("ListAllowedDirectories", "", agent);

        // Act
        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.InvocationSucceeded, Is.True);
    }

    [Test]
    public async Task CallFuncToolAsync_NoArgTool_WithLiteralNullArguments_Succeeds()
    {
        // Arrange - LLM sends literal "null" string
        TornadoAgent agent = CreateTestAgent(ListAllowedDirectories);
        FunctionCall call = CreateFunctionCall("ListAllowedDirectories", "null", agent);

        // Act
        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.InvocationSucceeded, Is.True);
    }

    [Test]
    public async Task CallFuncToolAsync_NoArgTool_WithEmptyObjectArguments_Succeeds()
    {
        // Arrange - LLM sends proper empty object
        TornadoAgent agent = CreateTestAgent(ListAllowedDirectories);
        FunctionCall call = CreateFunctionCall("ListAllowedDirectories", "{}", agent);

        // Act
        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.InvocationSucceeded, Is.True);
    }

    [Test]
    public async Task CallFuncToolAsync_OptionalArgTool_WithNullArguments_UsesDefaults()
    {
        // Arrange - Tool with optional param, LLM sends null
        TornadoAgent agent = CreateTestAgent(GetCurrentTime);
        FunctionCall call = CreateFunctionCall("GetCurrentTime", null, agent);

        // Act
        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.InvocationSucceeded, Is.True);
        Assert.That(result.Content, Does.Contain("UTC"));
    }

    [Test]
    public async Task CallFuncToolAsync_OptionalArgTool_WithProvidedArg_UsesProvidedValue()
    {
        // Arrange - Tool with optional param, LLM provides value
        TornadoAgent agent = CreateTestAgent(GetCurrentTime);
        FunctionCall call = CreateFunctionCall("GetCurrentTime", "{\"timezone\":\"PST\"}", agent);

        // Act
        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.InvocationSucceeded, Is.True);
        Assert.That(result.Content, Does.Contain("PST"));
    }

    [Test]
    public async Task CallFuncToolAsync_RequiredArgTool_WithValidArgs_Succeeds()
    {
        // Arrange
        TornadoAgent agent = CreateTestAgent(Search);
        FunctionCall call = CreateFunctionCall("Search", "{\"query\":\"hello world\"}", agent);

        // Act
        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.InvocationSucceeded, Is.True);
        Assert.That(result.Content, Does.Contain("hello world"));
    }

    [Test]
    public async Task CallFuncToolAsync_MixedArgTool_WithOnlyRequiredArgs_UsesDefaults()
    {
        // Arrange - Only provide required args, let optional use default
        TornadoAgent agent = CreateTestAgent(GetWeather);
        FunctionCall call = CreateFunctionCall("GetWeather", "{\"latitude\":42.0,\"longitude\":-71.0}", agent);

        // Act
        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.InvocationSucceeded, Is.True);
        Assert.That(result.Content, Does.Contain("metric")); // default value
    }

    [Test]
    public async Task CallFuncToolAsync_MixedArgTool_WithAllArgs_UsesProvidedValues()
    {
        // Arrange
        TornadoAgent agent = CreateTestAgent(GetWeather);
        FunctionCall call = CreateFunctionCall("GetWeather", 
            "{\"latitude\":42.0,\"longitude\":-71.0,\"units\":\"imperial\"}", agent);

        // Act
        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.InvocationSucceeded, Is.True);
        Assert.That(result.Content, Does.Contain("imperial"));
    }

    #endregion

    #region Edge Cases - Malformed Arguments

    [Test]
    public async Task CallFuncToolAsync_WithWhitespaceOnlyArguments_Succeeds()
    {
        // Arrange - Some LLMs send whitespace
        TornadoAgent agent = CreateTestAgent(ListAllowedDirectories);
        FunctionCall call = CreateFunctionCall("ListAllowedDirectories", "   \t\n  ", agent);

        // Act
        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.InvocationSucceeded, Is.True);
    }

    [Test]
    public async Task CallFuncToolAsync_WithEmptyArrayArguments_Succeeds()
    {
        // Arrange - Some LLMs might send []
        TornadoAgent agent = CreateTestAgent(ListAllowedDirectories);
        FunctionCall call = CreateFunctionCall("ListAllowedDirectories", "[]", agent);

        // Act
        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.InvocationSucceeded, Is.True);
    }

    #endregion

    #region Exceptions Thrown By Tools

    [Description("Reads a sensor that is offline")]
    public static string ReadOfflineSensor()
    {
        throw new InvalidOperationException("sensor offline");
    }

    [Test]
    public async Task CallFuncToolAsync_ToolThrows_ReportsTheToolException()
    {
        TornadoAgent agent = CreateTestAgent(ReadOfflineSensor);
        FunctionCall call = CreateFunctionCall("ReadOfflineSensor", null, agent);

        FunctionResult result = await ToolRunner.CallFuncToolAsync(agent, call);

        Assert.That(result.InvocationSucceeded, Is.False);
        Assert.That(result.Content, Does.Contain("sensor offline"));
        Assert.That(result.Content, Does.Not.Contain("target of an invocation"));
    }

    [Description("Returns a record with a null column")]
    public static object GetRecordWithNull()
    {
        return new { id = "1", deleted_at = (string?)null };
    }

    [Test]
    public async Task Invoke_ObjectResultWithNullProperty_OmitsTheNullByDefault()
    {
        TornadoAgent agent = CreateTestAgent(GetRecordWithNull);
        FunctionCall call = CreateFunctionCall("GetRecordWithNull", null, agent);

        await call.Invoke("{}");

        Assert.That(call.Result, Is.Not.Null);
        Assert.That(call.Result!.Content, Is.EqualTo("{\"id\":\"1\"}"));
    }

    [Test]
    public async Task Invoke_ObjectResultWithNullProperty_KeepsTheNullWhenOptedIn()
    {
        Tool tool = new Tool(GetRecordWithNull, new ToolMetadata { KeepNullsInResult = true });
        tool.Serialize(new OpenAiEndpointProvider(LLmProviders.OpenAi));
        FunctionCall call = new FunctionCall { Name = "GetRecordWithNull", Tool = tool };

        await call.Invoke("{}");

        Assert.That(call.Result, Is.Not.Null);
        Assert.That(call.Result!.Content, Is.EqualTo("{\"id\":\"1\",\"deleted_at\":null}"));
    }

    [Test]
    public async Task Invoke_ToolThrows_ReturnsTheToolExceptionNotTheReflectionWrapper()
    {
        TornadoAgent agent = CreateTestAgent(ReadOfflineSensor);
        FunctionCall call = CreateFunctionCall("ReadOfflineSensor", null, agent);

        MethodInvocationResult invocationResult = await call.Invoke("{}");

        Assert.That(invocationResult.InvocationException, Is.TypeOf<InvalidOperationException>());
        Assert.That(invocationResult.InvocationException!.Message, Is.EqualTo("sensor offline"));
    }

    #endregion

    #region Simulated MCP-Style Tool Invocation

    [Test]
    public void SimulateMcpTool_ListAllowedDirectories_WithNullArgs_Succeeds()
    {
        // This simulates what happens when an MCP tool like list_allowed_directories
        // is called by an LLM that sends null/empty arguments
        
        string? llmArguments = null;  // LLM omits arguments
        string normalizedArgs = ToolRunner.NormalizeArguments(llmArguments);
        
        // Verify normalization works
        Assert.That(normalizedArgs, Is.EqualTo("{}"));
        
        // Verify it can be deserialized
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object?>>(normalizedArgs);
        Assert.That(dict, Is.Not.Null);
        Assert.That(dict, Is.Empty);
    }

    [Test]
    public void SimulateMcpTool_ListDirectory_WithValidPath_Succeeds()
    {
        // Simulate MCP tool with required path argument
        string llmArguments = "{\"path\":\"C:\\\\Users\"}";
        string normalizedArgs = ToolRunner.NormalizeArguments(llmArguments);
        
        Assert.That(normalizedArgs, Is.EqualTo(llmArguments));
        
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object?>>(normalizedArgs);
        Assert.That(dict, Is.Not.Null);
        Assert.That(dict!.ContainsKey("path"), Is.True);
    }

    #endregion

    #region Cancellation

    [Test]
    public async Task CallFuncToolAsync_CancellationToken_ReachesDelegateParameter()
    {
        TornadoAgent agent = CreateTestAgent(GetTemperature);
        Tool tool = new Tool(WaitUntilCancelled, new ToolMetadata { Ignore = ["cancellationToken"] });
        tool.Serialize(new OpenAiEndpointProvider(LLmProviders.OpenAi));
        agent.ToolList["WaitUntilCancelled"] = tool;
        FunctionCall call = CreateFunctionCall("WaitUntilCancelled", "{}", agent);
        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        Task<FunctionResult> running = ToolRunner.CallFuncToolAsync(agent, call, cts.Token);
        FunctionResult result = await running.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(result.Content, Does.Contain("cancelled"));
    }

    [Description("Waits until the token it receives is cancelled.")]
    private static async Task<string> WaitUntilCancelled(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return "cancelled";
        }

        return "finished";
    }

    #endregion

    #region Test Helpers

    private static TornadoAgent CreateTestAgent(Delegate toolDelegate)
    {
        // Create a minimal TornadoApi - we just need it for the agent constructor
        var api = new TornadoApi("test-key");
        
        var agent = new TornadoAgent(
            api,
            ChatModel.OpenAi.Gpt4.Default,
            tools: [toolDelegate]
        );

        // Serialize tools to populate DelegateMetadata (normally done when sending to LLM)
        IEndpointProvider provider = new OpenAiEndpointProvider(LLmProviders.OpenAi);
        foreach (var tool in agent.ToolList.Values)
        {
            tool?.Serialize(provider);
        }

        return agent;
    }

    private static FunctionCall CreateFunctionCall(string name, string? arguments, TornadoAgent agent)
    {
        var call = new FunctionCall
        {
            Name = name,
            Arguments = arguments
        };

        // Link the tool to the call so Invoke works
        if (agent.ToolList.TryGetValue(name, out var tool))
        {
            call.Tool = tool;
        }

        return call;
    }

    #endregion
}
