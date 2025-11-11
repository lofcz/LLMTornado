using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace LlmTornado.Tests;

[TestFixture]
public class GraphValidationTests
{
    // Simple test orchestration that doesn't enforce types
    private class TestOrchestration : Orchestration
    {
        public TestOrchestration() : base() { }

        public override void SetEntryRunnable(OrchestrationRunnableBase initialRunnable)
        {
            InitialRunnable = initialRunnable;
        }
    }

    // Simple test runnables for testing type validation
    private class StringRunnable : OrchestrationRunnable<string, string>
    {
        public StringRunnable(Orchestration orchestrator, string? name = null) 
            : base(orchestrator, name ?? $"StringRunnable_{Guid.NewGuid().ToString().Substring(0, 8)}") { }

        public override ValueTask<string> Invoke(RunnableProcess<string, string> input)
        {
            return new ValueTask<string>(input.Input.ToUpper());
        }
    }

    private class IntRunnable : OrchestrationRunnable<int, int>
    {
        public IntRunnable(Orchestration orchestrator, string? name = null) 
            : base(orchestrator, name ?? $"IntRunnable_{Guid.NewGuid().ToString().Substring(0, 8)}") { }

        public override ValueTask<int> Invoke(RunnableProcess<int, int> input)
        {
            return new ValueTask<int>(input.Input * 2);
        }
    }

    private class StringToIntRunnable : OrchestrationRunnable<string, int>
    {
        public StringToIntRunnable(Orchestration orchestrator, string? name = null) 
            : base(orchestrator, name ?? $"StringToIntRunnable_{Guid.NewGuid().ToString().Substring(0, 8)}") { }

        public override ValueTask<int> Invoke(RunnableProcess<string, int> input)
        {
            return new ValueTask<int>(input.Input.Length);
        }
    }

    private class IntToStringRunnable : OrchestrationRunnable<int, string>
    {
        public IntToStringRunnable(Orchestration orchestrator, string? name = null) 
            : base(orchestrator, name ?? $"IntToStringRunnable_{Guid.NewGuid().ToString().Substring(0, 8)}") { }

        public override ValueTask<string> Invoke(RunnableProcess<int, string> input)
        {
            return new ValueTask<string>(input.Input.ToString());
        }
    }

    private class ChatMessageRunnable : OrchestrationRunnable<LlmTornado.Chat.ChatMessage, LlmTornado.Chat.ChatMessage>
    {
        public ChatMessageRunnable(Orchestration orchestrator, string? name = null) 
            : base(orchestrator, name ?? $"ChatMessageRunnable_{Guid.NewGuid().ToString().Substring(0, 8)}") { }

        public override ValueTask<LlmTornado.Chat.ChatMessage> Invoke(RunnableProcess<LlmTornado.Chat.ChatMessage, LlmTornado.Chat.ChatMessage> input)
        {
            return new ValueTask<LlmTornado.Chat.ChatMessage>(input.Input);
        }
    }

    #region Basic Validation Tests

    [Test]
    public void ValidateGraph_WithNoInitialRunnable_ShouldReturnError()
    {
        // Arrange
        var config = new TestOrchestration();

        // Act
        var result = GraphCompiler.ValidateGraph(config);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0], Does.Contain("No initial runnable"));
    }

    [Test]
    public void ValidateGraph_WithRunnableWithoutAdvancers_ShouldReturnError()
    {
        // Arrange
        var config = new TestOrchestration();
        var runnable = new StringRunnable(config);
        config.SetEntryRunnable(runnable);

        // Act
        var result = GraphCompiler.ValidateGraph(config);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Count, Is.GreaterThan(0));
        Assert.That(result.Errors[0], Does.Contain("has no advancers"));
    }

    [Test]
    public void ValidateGraph_WithDeadEndRunnable_ShouldBeValid()
    {
        // Arrange
        var config = new TestOrchestration();
        var runnable = new StringRunnable(config) { AllowDeadEnd = true };
        config.SetEntryRunnable(runnable);

        // Act
        var result = GraphCompiler.ValidateGraph(config);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ValidateGraph_WithValidGraph_ShouldSucceed()
    {
        // Arrange
        var config = new TestOrchestration();
        var runnable1 = new StringRunnable(config);
        var runnable2 = new StringRunnable(config) { AllowDeadEnd = true };
        
        runnable1.AddAdvancer(runnable2);
        config.SetEntryRunnable(runnable1);

        // Act
        var result = GraphCompiler.ValidateGraph(config);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    #endregion

    #region Type Mismatch Detection Tests

    [Test]
    public void ValidateGraph_WithTypeMismatch_ShouldThrowAtAdvancerCreation()
    {
        // Arrange
        var config = new TestOrchestration();
        var stringRunnable = new StringRunnable(config);
        var intRunnable = new IntRunnable(config) { AllowDeadEnd = true };
        
        // Act & Assert
        // Type mismatch should be caught when adding the advancer (compile-time check)
        var ex = Assert.Throws<InvalidOperationException>(() => 
            stringRunnable.AddAdvancer(intRunnable));
        
        Assert.That(ex.Message, Does.Contain("Type mismatch"));
        Assert.That(ex.Message, Does.Contain("String"));
        Assert.That(ex.Message, Does.Contain("Int32"));
    }

    [Test]
    public void ValidateGraph_WithConverter_ShouldBeValid()
    {
        // Arrange
        var config = new TestOrchestration();
        var runnable1 = new StringRunnable(config);
        var runnable2 = new IntRunnable(config) { AllowDeadEnd = true };
        
        // Use a converter to transform string to int
        runnable1.AddAdvancer<int>(
            (str) => str.Length,
            runnable2,
            (str) => true
        );
        config.SetEntryRunnable(runnable1);

        // Act
        var result = GraphCompiler.ValidateGraph(config);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    #endregion

    #region Typed Orchestration Validation Tests

    [Test]
    public void ValidateTypedGraph_WithCorrectTypes_ShouldSucceed()
    {
        // Arrange
        var orchestration = new Orchestration<string, string>();
        var runnable1 = new StringRunnable(orchestration);
        var runnable2 = new StringRunnable(orchestration) { AllowDeadEnd = true };
        
        runnable1.AddAdvancer(runnable2);
        orchestration.SetEntryRunnable(runnable1);
        orchestration.SetRunnableWithResult(runnable2);

        // Act
        var result = GraphCompiler.ValidateTypedGraph(orchestration);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ValidateTypedGraph_WithWrongInputType_ShouldReturnError()
    {
        // Arrange
        var orchestration = new Orchestration<int, string>();
        var stringRunnable = new StringRunnable(orchestration) { AllowDeadEnd = true };

        // Act & Assert
        // Setting a string runnable as entry when orchestration expects int should throw
        Assert.Throws<InvalidCastException>(() => orchestration.SetEntryRunnable(stringRunnable));
    }

    [Test]
    public void ValidateTypedGraph_WithWrongOutputType_ShouldReturnError()
    {
        // Arrange
        var orchestration = new Orchestration<string, int>();
        var stringRunnable = new StringRunnable(orchestration) { AllowDeadEnd = true };
        orchestration.SetEntryRunnable(stringRunnable);

        // Act & Assert
        // Setting a string runnable as result when orchestration expects int should throw
        Assert.Throws<InvalidCastException>(() => orchestration.SetRunnableWithResult(stringRunnable));
    }

    #endregion

    #region OrchestrationBuilder Validation Tests

    [Test]
    public void OrchestrationBuilder_Validate_WithInvalidGraph_ShouldThrow()
    {
        // Arrange
        var runtimeConfig = new OrchestrationRuntimeConfiguration();
        var builder = new OrchestrationBuilder(runtimeConfig);
        // Create runnable with the same orchestration
        var runnable = new ChatMessageRunnable(runtimeConfig);
        
        builder.SetEntryRunnable(runnable);
        // No advancers and not a dead end - should fail validation

        // Act & Assert
        Assert.Throws<GraphValidationException>(() => builder.Validate(throwOnError: true));
    }

    [Test]
    public void OrchestrationBuilder_Validate_WithValidGraph_ShouldNotThrow()
    {
        // Arrange
        var runtimeConfig = new OrchestrationRuntimeConfiguration();
        var builder = new OrchestrationBuilder(runtimeConfig);
        var runnable = new ChatMessageRunnable(runtimeConfig) { AllowDeadEnd = true };
        
        builder.SetEntryRunnable(runnable);

        // Act & Assert
        Assert.DoesNotThrow(() => builder.Validate(throwOnError: true));
    }

    [Test]
    public void OrchestrationBuilder_Compile_WithInvalidGraph_ShouldThrow()
    {
        // Arrange
        var runtimeConfig = new OrchestrationRuntimeConfiguration();
        var builder = new OrchestrationBuilder(runtimeConfig);
        var runnable = new ChatMessageRunnable(runtimeConfig);
        
        builder.SetEntryRunnable(runnable);

        // Act & Assert
        Assert.Throws<GraphValidationException>(() => builder.Compile(validateGraph: true));
    }

    [Test]
    public void OrchestrationBuilder_Compile_WithValidGraph_ShouldSucceed()
    {
        // Arrange
        var runtimeConfig = new OrchestrationRuntimeConfiguration();
        var builder = new OrchestrationBuilder(runtimeConfig);
        var runnable = new ChatMessageRunnable(runtimeConfig) { AllowDeadEnd = true };
        
        builder.SetEntryRunnable(runnable);

        // Act
        var compiledConfig = builder.Compile(validateGraph: true);

        // Assert
        Assert.That(compiledConfig, Is.Not.Null);
        Assert.That(compiledConfig.InitialRunnable, Is.EqualTo(runnable));
    }

    [Test]
    public void OrchestrationBuilder_Compile_SkipValidation_ShouldNotThrow()
    {
        // Arrange
        var runtimeConfig = new OrchestrationRuntimeConfiguration();
        var builder = new OrchestrationBuilder(runtimeConfig);
        var runnable = new ChatMessageRunnable(runtimeConfig);
        
        builder.SetEntryRunnable(runnable);

        // Act & Assert
        Assert.DoesNotThrow(() => builder.Compile(validateGraph: false));
    }

    #endregion

    #region Error Message Quality Tests

    [Test]
    public void AdvancerTypeMismatch_ShouldProvideHelpfulErrorMessage()
    {
        // Arrange
        var config = new TestOrchestration();
        var stringRunnable = new StringRunnable(config);
        var intRunnable = new IntRunnable(config);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => 
            stringRunnable.AddAdvancer((str) => true, intRunnable));
        
        Assert.That(ex.Message, Does.Contain("Type mismatch"));
        Assert.That(ex.Message, Does.Contain(intRunnable.RunnableName));
        Assert.That(ex.Message, Does.Contain("String"));
        Assert.That(ex.Message, Does.Contain("Int"));
    }

    [Test]
    public void ValidationResult_ToString_ShouldFormatNicely()
    {
        // Arrange
        var result = new GraphValidationResult();
        result.AddError("Error 1");
        result.AddError("Error 2");
        result.AddWarning("Warning 1");

        // Act
        var formatted = result.ToString();

        // Assert
        Assert.That(formatted, Does.Contain("2 error(s)"));
        Assert.That(formatted, Does.Contain("1 warning(s)"));
        Assert.That(formatted, Does.Contain("Error 1"));
        Assert.That(formatted, Does.Contain("Error 2"));
        Assert.That(formatted, Does.Contain("Warning 1"));
    }

    #endregion

    #region Complex Graph Validation Tests

    [Test]
    public void ValidateGraph_WithUnreachableRunnable_ShouldWarn()
    {
        // Arrange
        var config = new TestOrchestration();
        var runnable1 = new StringRunnable(config) { AllowDeadEnd = true };
        var runnable2 = new StringRunnable(config) { AllowDeadEnd = true };
        
        config.SetEntryRunnable(runnable1);
        // runnable2 is added to Runnables but not connected
        config.Runnables.Add("unreachable", runnable2);

        // Act
        var result = GraphCompiler.ValidateGraph(config);

        // Assert
        Assert.That(result.IsValid, Is.True); // Still valid, just has warnings
        Assert.That(result.Warnings.Count, Is.GreaterThan(0));
        Assert.That(result.Warnings[0], Does.Contain("unreachable"));
    }

    [Test]
    public void ValidateGraph_WithMultiplePaths_ShouldValidateAll()
    {
        // Arrange
        var config = new TestOrchestration();
        var start = new StringRunnable(config);
        var path1 = new StringRunnable(config) { AllowDeadEnd = true };
        var path2 = new StringRunnable(config) { AllowDeadEnd = true };
        
        start.AllowsParallelAdvances = true;
        start.AddAdvancer(path1);
        start.AddAdvancer(path2);
        config.SetEntryRunnable(start);

        // Act
        var result = GraphCompiler.ValidateGraph(config);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    #endregion
}
