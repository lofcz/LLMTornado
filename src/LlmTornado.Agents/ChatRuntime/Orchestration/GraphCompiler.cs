using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Provides compile-time validation and type checking for orchestration graphs.
/// This class ensures that all runnables in the graph have compatible types and proper connections.
/// </summary>
public class GraphCompiler
{
    /// <summary>
    /// Validates the entire orchestration graph for type safety and structural correctness.
    /// </summary>
    /// <param name="orchestration">The orchestration to validate</param>
    /// <returns>A validation result containing any errors found</returns>
    public static GraphValidationResult ValidateGraph(Orchestration orchestration)
    {
        var result = new GraphValidationResult();
        
        if (orchestration.InitialRunnable == null)
        {
            result.AddError("No initial runnable has been set. Call SetEntryRunnable() to define the starting point.");
            return result;
        }

        // Track visited runnables to detect cycles and unreachable nodes
        var visited = new HashSet<string>();
        var toVisit = new Queue<OrchestrationRunnableBase>();
        toVisit.Enqueue(orchestration.InitialRunnable);

        while (toVisit.Count > 0)
        {
            var currentRunnable = toVisit.Dequeue();
            
            if (visited.Contains(currentRunnable.RunnableName))
                continue;
                
            visited.Add(currentRunnable.RunnableName);

            // Validate the current runnable
            ValidateRunnable(currentRunnable, result);

            // Check all advancers from this runnable
            foreach (var advancer in currentRunnable.BaseAdvancers)
            {
                ValidateAdvancer(currentRunnable, advancer, result);
                
                if (advancer.NextRunnable != null && !visited.Contains(advancer.NextRunnable.RunnableName))
                {
                    toVisit.Enqueue(advancer.NextRunnable);
                }
            }
        }

        // Check for unreachable runnables
        var allRunnables = orchestration.Runnables.Values.ToList();
        var unreachable = allRunnables.Where(r => !visited.Contains(r.RunnableName)).ToList();
        
        foreach (var runnable in unreachable)
        {
            result.AddWarning($"Runnable '{runnable.RunnableName}' is defined but unreachable from the initial runnable.");
        }

        return result;
    }

    /// <summary>
    /// Validates a single runnable for structural correctness.
    /// </summary>
    private static void ValidateRunnable(OrchestrationRunnableBase runnable, GraphValidationResult result)
    {
        // Check if runnable has advancers or is marked as dead end
        if (!runnable.AllowDeadEnd && runnable.BaseAdvancers.Count == 0)
        {
            result.AddError($"Runnable '{runnable.RunnableName}' has no advancers and is not marked as a dead end. " +
                          $"Either add an advancer using AddAdvancer() or set AllowDeadEnd = true.");
        }

        // Validate input/output types are not null
        try
        {
            var inputType = runnable.GetInputType();
            var outputType = runnable.GetOutputType();
            
            if (inputType == null)
            {
                result.AddError($"Runnable '{runnable.RunnableName}' has a null input type. This indicates a configuration error.");
            }
            
            if (outputType == null)
            {
                result.AddError($"Runnable '{runnable.RunnableName}' has a null output type. This indicates a configuration error.");
            }
        }
        catch (Exception ex)
        {
            result.AddError($"Runnable '{runnable.RunnableName}' threw an exception when getting types: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates a single advancer for type compatibility between source and target runnables.
    /// </summary>
    private static void ValidateAdvancer(OrchestrationRunnableBase fromRunnable, OrchestrationAdvancer advancer, GraphValidationResult result)
    {
        if (advancer.NextRunnable == null)
        {
            result.AddError($"Advancer from '{fromRunnable.RunnableName}' has a null NextRunnable. All advancers must have a valid target.");
            return;
        }

        var fromOutputType = fromRunnable.GetOutputType();
        var toInputType = advancer.NextRunnable.GetInputType();

        // For conversion advancers (in_out type), validate the converter output type
        if (advancer.type == "in_out")
        {
            // Try to get the TOutput type from the converter method
            if (advancer.ConverterMethod != null)
            {
                var converterReturnType = advancer.ConverterMethod.Method.ReturnType;
                
                if (!toInputType.IsAssignableFrom(converterReturnType))
                {
                    result.AddError($"Type mismatch in advancer from '{fromRunnable.RunnableName}' to '{advancer.NextRunnable.RunnableName}': " +
                                  $"Converter returns type '{GetFriendlyTypeName(converterReturnType)}' but target expects '{GetFriendlyTypeName(toInputType)}'. " +
                                  $"The converter output type must be assignable to the target's input type.");
                }
            }
        }
        else if (advancer.type == "out")
        {
            // For regular advancers, validate that output type matches input type
            if (!toInputType.IsAssignableFrom(fromOutputType))
            {
                result.AddError($"Type mismatch in advancer from '{fromRunnable.RunnableName}' to '{advancer.NextRunnable.RunnableName}': " +
                              $"Source outputs '{GetFriendlyTypeName(fromOutputType)}' but target expects '{GetFriendlyTypeName(toInputType)}'. " +
                              $"Consider adding a converter using AddAdvancer<TValue, TOutput>() to transform the output.");
            }
        }

        // Validate that the advancer has a condition method
        if (advancer.InvokeMethod == null)
        {
            result.AddWarning($"Advancer from '{fromRunnable.RunnableName}' to '{advancer.NextRunnable.RunnableName}' has no condition method. " +
                            $"This advancer will always evaluate to false and never be taken.");
        }
    }

    /// <summary>
    /// Gets a friendly, readable name for a type.
    /// </summary>
    private static string GetFriendlyTypeName(Type type)
    {
        if (type == null)
            return "null";

        if (!type.IsGenericType)
            return type.Name;

        var genericTypeName = type.GetGenericTypeDefinition().Name;
        var genericArgs = type.GetGenericArguments();
        
        // Remove the `1, `2, etc. from generic type names
        var backtickIndex = genericTypeName.IndexOf('`');
        if (backtickIndex > 0)
            genericTypeName = genericTypeName.Substring(0, backtickIndex);

        var argNames = string.Join(", ", genericArgs.Select(GetFriendlyTypeName));
        return $"{genericTypeName}<{argNames}>";
    }

    /// <summary>
    /// Validates type compatibility for a typed orchestration.
    /// </summary>
    public static GraphValidationResult ValidateTypedGraph<TInput, TOutput>(Orchestration<TInput, TOutput> orchestration)
    {
        var result = ValidateGraph(orchestration);

        // Additional validation for typed orchestrations
        if (orchestration.InitialRunnable != null)
        {
            var initialInputType = orchestration.InitialRunnable.GetInputType();
            if (!typeof(TInput).IsAssignableFrom(initialInputType))
            {
                result.AddError($"Initial runnable '{orchestration.InitialRunnable.RunnableName}' expects input type '{GetFriendlyTypeName(initialInputType)}' " +
                              $"but orchestration is configured for '{GetFriendlyTypeName(typeof(TInput))}'. " +
                              $"The initial runnable's input type must be assignable from the orchestration's input type.");
            }
        }

        if (orchestration.RunnableWithResult != null)
        {
            var resultOutputType = orchestration.RunnableWithResult.GetOutputType();
            if (!typeof(TOutput).IsAssignableFrom(resultOutputType))
            {
                result.AddError($"Result runnable '{orchestration.RunnableWithResult.RunnableName}' outputs type '{GetFriendlyTypeName(resultOutputType)}' " +
                              $"but orchestration is configured to return '{GetFriendlyTypeName(typeof(TOutput))}'. " +
                              $"The result runnable's output type must be assignable to the orchestration's output type.");
            }
        }
        else
        {
            result.AddWarning("No result runnable has been set. Call SetRunnableWithResult() to define the output node.");
        }

        return result;
    }
}

/// <summary>
/// Contains the results of graph validation, including errors and warnings.
/// </summary>
public class GraphValidationResult
{
    private readonly List<string> _errors = new List<string>();
    private readonly List<string> _warnings = new List<string>();

    /// <summary>
    /// Gets all validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Gets all validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();

    /// <summary>
    /// Returns true if the graph is valid (no errors).
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    public void AddError(string error)
    {
        _errors.Add(error);
    }

    /// <summary>
    /// Adds a warning to the validation result.
    /// </summary>
    public void AddWarning(string warning)
    {
        _warnings.Add(warning);
    }

    /// <summary>
    /// Returns a formatted string containing all errors and warnings.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        
        if (_errors.Count > 0)
        {
            sb.AppendLine($"Graph Validation Failed with {_errors.Count} error(s):");
            for (int i = 0; i < _errors.Count; i++)
            {
                sb.AppendLine($"  Error {i + 1}: {_errors[i]}");
            }
        }
        else
        {
            sb.AppendLine("Graph Validation Succeeded");
        }

        if (_warnings.Count > 0)
        {
            sb.AppendLine($"{_warnings.Count} warning(s):");
            for (int i = 0; i < _warnings.Count; i++)
            {
                sb.AppendLine($"  Warning {i + 1}: {_warnings[i]}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Throws an exception if the validation result contains any errors.
    /// </summary>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new GraphValidationException(ToString());
        }
    }
}

/// <summary>
/// Exception thrown when graph validation fails.
/// </summary>
public class GraphValidationException : Exception
{
    public GraphValidationException(string message) : base(message)
    {
    }

    public GraphValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
