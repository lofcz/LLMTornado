# Type-Safe Graph Building for Agent Orchestration

## Overview

LlmTornado's agent orchestration system now includes comprehensive type validation to catch errors early and make building agent graphs more obvious and easier. The system provides three levels of type safety:

1. **Compile-Time Type Checking** - Type mismatches are caught when creating advancers
2. **Graph Validation** - Validates the entire graph structure for correctness  
3. **Helpful Error Messages** - Clear explanations of what's wrong and how to fix it

## Basic Example

```csharp
// Create runnables with specific input/output types
var stringProcessor = new StringProcessorRunnable(orchestration);  // string -> string
var intCounter = new IntCounterRunnable(orchestration);            // int -> int

// ❌ This will throw InvalidOperationException immediately
// Error: Type mismatch - StringProcessor outputs 'String' but IntCounter expects 'Int32'
stringProcessor.AddAdvancer(intCounter);

// ✅ Use a converter to fix the type mismatch
stringProcessor.AddAdvancer<int>(
    converter: (str) => str.Length,      // Convert string to int
    nextRunnable: intCounter,
    condition: (str) => !string.IsNullOrEmpty(str)
);
```

## Graph Validation

The `GraphCompiler` class provides comprehensive validation of your entire orchestration graph:

```csharp
var orchestration = new OrchestrationRuntimeConfiguration();
var builder = new OrchestrationBuilder(orchestration);

// Build your graph
builder.SetEntryRunnable(startRunnable);
builder.AddAdvancer<string>(step1, step2);
builder.AddAdvancer<int>(step2, step3);
builder.SetOutputRunnable(finalRunnable);

// Validate the graph without throwing
var result = builder.Validate(throwOnError: false);
if (!result.IsValid)
{
    Console.WriteLine("Graph validation failed:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  ❌ {error}");
    }
    
    foreach (var warning in result.Warnings)
    {
        Console.WriteLine($"  ⚠️  {warning}");
    }
}

// Or validate and throw on error
builder.Validate(throwOnError: true);  // Throws GraphValidationException if invalid
```

## Compilation Phase

The recommended way to build an orchestration is using the `Compile()` method, which validates the graph before returning:

```csharp
var builder = new OrchestrationBuilder(new OrchestrationRuntimeConfiguration());

// Set up your graph
builder.SetEntryRunnable(entryRunnable);
builder.AddAdvancer<string>(runnable1, runnable2);
builder.SetOutputRunnable(outputRunnable);

// Compile with automatic validation (recommended)
var config = builder.Compile(validateGraph: true);

// Or skip validation if you're confident (not recommended)
var config = builder.Compile(validateGraph: false);

// Or just build without validation (legacy behavior)
var config = builder.Build();
```

## Common Validation Errors

### 1. No Initial Runnable

```csharp
var orchestration = new OrchestrationRuntimeConfiguration();
var result = GraphCompiler.ValidateGraph(orchestration);
// Error: "No initial runnable has been set. Call SetEntryRunnable() to define the starting point."
```

**Fix:**
```csharp
orchestration.SetEntryRunnable(myStartRunnable);
```

### 2. Runnable with No Advancers

```csharp
var runnable = new MyRunnable(orchestration);
orchestration.SetEntryRunnable(runnable);
var result = GraphCompiler.ValidateGraph(orchestration);
// Error: "Runnable 'MyRunnable' has no advancers and is not marked as a dead end."
```

**Fix (Option 1 - Add an advancer):**
```csharp
runnable.AddAdvancer(nextRunnable);
```

**Fix (Option 2 - Mark as dead end):**
```csharp
runnable.AllowDeadEnd = true;
```

### 3. Type Mismatch Between Runnables

```csharp
var stringRunnable = new StringRunnable(orchestration);  // string -> string
var intRunnable = new IntRunnable(orchestration);        // int -> int

// ❌ This throws immediately
stringRunnable.AddAdvancer(intRunnable);
// Error: "Type mismatch: Cannot advance to runnable 'IntRunnable'.
//         The advancer outputs type 'String' but the target runnable expects 'Int32'.
//         To fix this, either:
//         1) Change the target runnable to accept 'String' as input, or
//         2) Use AddAdvancer<TValue, TOutput>() to provide a converter..."
```

**Fix (Use a converter):**
```csharp
stringRunnable.AddAdvancer<int>(
    converter: (str) => str.Length,
    nextRunnable: intRunnable,
    condition: (str) => true
);
```

### 4. Unreachable Runnables

```csharp
var start = new StartRunnable(orchestration);
var middle = new MiddleRunnable(orchestration);
var end = new EndRunnable(orchestration);

start.AddAdvancer(end);  // Skips middle!
orchestration.SetEntryRunnable(start);

var result = GraphCompiler.ValidateGraph(orchestration);
// Warning: "Runnable 'MiddleRunnable' is defined but unreachable from the initial runnable."
```

**Fix:**
```csharp
start.AddAdvancer(middle);
middle.AddAdvancer(end);
```

### 5. Wrong Input/Output Types for Typed Orchestrations

```csharp
var orchestration = new Orchestration<int, string>();  // Expects int input, string output
var stringRunnable = new StringRunnable(orchestration); // string -> string

// ❌ This throws InvalidCastException
orchestration.SetEntryRunnable(stringRunnable);
// Error: "Type mismatch for entry runnable 'StringRunnable':
//         The orchestration expects input type 'Int32' but the runnable accepts 'String'."
```

**Fix:**
```csharp
var intToStringRunnable = new IntToStringRunnable(orchestration); // int -> string
orchestration.SetEntryRunnable(intToStringRunnable);
```

## Advanced: Type Converters

When connecting runnables with different types, use converters:

```csharp
// Define runnables with different types
var textAnalyzer = new TextAnalyzerRunnable(orchestration);     // string -> TextAnalysis
var sentimentScorer = new SentimentScorerRunnable(orchestration); // Sentiment -> int
var reporter = new ReporterRunnable(orchestration);             // int -> string

// Connect them with converters
textAnalyzer.AddAdvancer<Sentiment>(
    converter: (analysis) => analysis.Sentiment,
    nextRunnable: sentimentScorer,
    condition: (analysis) => analysis.Confidence > 0.8
);

sentimentScorer.AddAdvancer<int>(
    converter: (score) => score,
    nextRunnable: reporter,
    condition: (score) => score >= 0
);
```

## Best Practices

1. **Always use `Compile()`** instead of `Build()` to catch errors early
2. **Enable validation in development** and only skip it in production if performance is critical
3. **Review validation warnings** - they often point to potential issues
4. **Use meaningful runnable names** to make error messages clearer
5. **Test your graph** with various inputs before deploying

## Benefits

- **Catch errors early** - Type mismatches found at graph construction time, not during execution
- **Clear error messages** - Explains what's wrong and suggests how to fix it
- **Better IDE support** - Generic type parameters provide autocomplete and type checking
- **Structural validation** - Detects unreachable nodes, missing advancers, and other graph issues
- **Safer refactoring** - Type system prevents accidental breakage when changing runnable types

## Migration Guide

Existing code continues to work without changes. To add validation to existing code:

### Before
```csharp
var builder = new OrchestrationBuilder(config);
builder.SetEntryRunnable(start);
builder.AddAdvancer<string>(step1, step2);
var orchestration = builder.Build();
```

### After (Recommended)
```csharp
var builder = new OrchestrationBuilder(config);
builder.SetEntryRunnable(start);
builder.AddAdvancer<string>(step1, step2);
var orchestration = builder.Compile();  // Now validates automatically!
```

Or for manual control:

```csharp
var builder = new OrchestrationBuilder(config);
builder.SetEntryRunnable(start);
builder.AddAdvancer<string>(step1, step2);

// Validate first
var validationResult = builder.Validate(throwOnError: false);
if (!validationResult.IsValid)
{
    LogErrors(validationResult.Errors);
    throw new Exception("Graph validation failed");
}

// Then build
var orchestration = builder.Build();
```
