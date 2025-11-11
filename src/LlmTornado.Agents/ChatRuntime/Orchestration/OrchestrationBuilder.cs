using LlmTornado.Agents.ChatRuntime.Orchestration;
using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.DataModels;
using LlmTornado.Agents.Utility;
using LlmTornado.Chat;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
#if !MODERN
using Polyfills;
#endif

namespace LlmTornado.Agents.ChatRuntime;

public class OrchestrationBuilder
{
    public OrchestrationRuntimeConfiguration Configuration { get; private set; }

    public OrchestrationBuilder(OrchestrationRuntimeConfiguration config)
    {
        Configuration = config;
    }

    public OrchestrationBuilder WithRuntimeInitializer(Func<OrchestrationRuntimeConfiguration, ValueTask> customInitializer)
    {
        Configuration.CustomInitialization = customInitializer;
        return this;
    }
    public OrchestrationBuilder WithDataRecording()
    {
        Configuration.RecordSteps = true;
        return this;
    }

    public OrchestrationBuilder WithOnRuntimeEvent(Func<ChatRuntimeEvents, ValueTask> onRuntimeEvent)
    {
        Configuration.OnRuntimeEvent = onRuntimeEvent;
        return this;
    }

    public OrchestrationBuilder WithChatMemory(string memoryFilePath)
    {

        if (Path.GetExtension(memoryFilePath).ToLower() != ".json")
        {
            throw new ArgumentException("Conversation file must be a .json file");
        }

        Configuration.MessageHistoryFileLocation = memoryFilePath;
        
        return this;
    }

    public OrchestrationBuilder WithRuntimeProperty(string key, object value)
    {
        Configuration.RuntimeProperties.AddOrUpdate(key, value, (_, _) => value);
        return this;
    }

    public OrchestrationBuilder WithCancellationTokenSource(CancellationTokenSource cts)
    {
        Configuration.cts = cts;
        return this;
    }

    /// <summary>
    /// Enables debug logging for orchestration processes.
    /// When enabled, detailed diagnostic information about advancement checking and state transitions will be logged.
    /// </summary>
    /// <returns>The current OrchestrationBuilder instance for method chaining.</returns>
    public OrchestrationBuilder WithDebug()
    {
        Configuration.Options.Debug = true;
        return this;
    }

    public OrchestrationBuilder SetEntryRunnable(OrchestrationRunnableBase entryRunnable)
    {
        Configuration.SetEntryRunnable(entryRunnable);
        entryRunnable.Orchestrator = Configuration;
        return this;
    }

    public OrchestrationBuilder SetOutputRunnable(OrchestrationRunnableBase outputRunnable, bool withDeadEnd = false)
    {
        Configuration.SetRunnableWithResult(outputRunnable);
        outputRunnable.Orchestrator = Configuration;

        if (withDeadEnd)
        {
            outputRunnable.AllowDeadEnd = true;
        }
        
        return this;
    }
    
    public OrchestrationBuilder AddAdvancer<TOutput>(OrchestrationRunnableBase fromRunnable, OrchestrationRunnableBase toRunnable)
    {
        Configuration.Runnables.TryAdd(fromRunnable.RunnableName, fromRunnable);
            
        Configuration.Runnables.TryAdd(toRunnable.RunnableName, toRunnable);

        fromRunnable.Orchestrator = Configuration;
        toRunnable.Orchestrator = Configuration;
        fromRunnable.AddAdvancer(new OrchestrationAdvancer<TOutput>(_ => true, toRunnable));
        return this;
    }

    public OrchestrationBuilder AddDeadEndAdvancer<TOutput>(OrchestrationRunnableBase fromRunnable, OrchestrationRunnableBase toRunnable)
    {
        Configuration.Runnables.TryAdd(fromRunnable.RunnableName, fromRunnable);
        Configuration.Runnables.TryAdd(toRunnable.RunnableName, toRunnable);

        fromRunnable.Orchestrator = Configuration;
        toRunnable.Orchestrator = Configuration;
        fromRunnable.AddAdvancer(new OrchestrationAdvancer<TOutput>(_ => true, toRunnable));
        toRunnable.AllowDeadEnd = true;
        return this;
    }

    public OrchestrationBuilder AddDeadEndAdvancer<TValue, TOutput>(OrchestrationRunnableBase fromRunnable, AdvancementRequirement<TValue> condition, AdvancementResultConverter<TValue, TOutput> converter, OrchestrationRunnableBase toRunnable)
    {
        Configuration.Runnables.TryAdd(fromRunnable.RunnableName, fromRunnable);
        Configuration.Runnables.TryAdd(toRunnable.RunnableName, toRunnable);

        fromRunnable.Orchestrator = Configuration;
        toRunnable.Orchestrator = Configuration;
        fromRunnable.AddAdvancer(new OrchestrationAdvancer<TValue, TOutput>(condition, converter, toRunnable));
        toRunnable.AllowDeadEnd = true;
        return this;
    }

    public OrchestrationBuilder AddAdvancer<TOutput>(OrchestrationRunnableBase fromRunnable, AdvancementRequirement<TOutput> condition, OrchestrationRunnableBase toRunnable)
    {
        Configuration.Runnables.TryAdd(fromRunnable.RunnableName, fromRunnable);
        Configuration.Runnables.TryAdd(toRunnable.RunnableName, toRunnable);

        fromRunnable.Orchestrator = Configuration;
        toRunnable.Orchestrator = Configuration;
        fromRunnable.AddAdvancer(new OrchestrationAdvancer<TOutput>(condition, toRunnable));
        return this;
    }

    public OrchestrationBuilder AddAdvancer<TValue, TOutput>(OrchestrationRunnableBase fromRunnable, AdvancementRequirement<TValue> condition, AdvancementResultConverter<TValue, TOutput> converter, OrchestrationRunnableBase toRunnable)
    {
        Configuration.Runnables.TryAdd(fromRunnable.RunnableName, fromRunnable);
        Configuration.Runnables.TryAdd(toRunnable.RunnableName, toRunnable);

        fromRunnable.Orchestrator = Configuration;
        toRunnable.Orchestrator = Configuration;
        fromRunnable.AddAdvancer(new OrchestrationAdvancer<TValue, TOutput>(condition, converter, toRunnable));
        return this;
    }

    public OrchestrationBuilder AddAdvancer<TValue, TOutput>(OrchestrationRunnableBase fromRunnable, AdvancementResultConverter<TValue, TOutput> converter, OrchestrationRunnableBase toRunnable, AdvancementRequirement<TValue>? condition = null)
    {
        Configuration.Runnables.TryAdd(fromRunnable.RunnableName, fromRunnable);
        Configuration.Runnables.TryAdd(toRunnable.RunnableName, toRunnable);

        fromRunnable.Orchestrator = Configuration;
        toRunnable.Orchestrator = Configuration;
        condition ??= _ => true;
        fromRunnable.AddAdvancer(new OrchestrationAdvancer<TValue, TOutput>(condition, converter, toRunnable));
        return this;
    }

    public OrchestrationBuilder AddAdvancers(OrchestrationRunnableBase fromRunnable, params OrchestrationAdvancer[] advancers)
    {
        Configuration.Runnables.TryAdd(fromRunnable.RunnableName, fromRunnable);

        foreach(OrchestrationAdvancer advancer in advancers)
        {
            if (!Configuration.Runnables.ContainsKey(advancer.NextRunnable.RunnableName))
                Configuration.Runnables.Add(advancer.NextRunnable.RunnableName, advancer.NextRunnable);
            advancer.NextRunnable.Orchestrator = Configuration;
            fromRunnable.AddAdvancer(advancer);
        }
        
        fromRunnable.Orchestrator = Configuration;
        return this;
    }

    public OrchestrationBuilder AddParallelAdvancement(OrchestrationRunnableBase fromRunnable, params OrchestrationAdvancer[] advancers)
    {
        Configuration.Runnables.TryAdd(fromRunnable.RunnableName, fromRunnable);
        fromRunnable.Orchestrator = Configuration;
        foreach (OrchestrationAdvancer advancer in advancers)
        {
            if (!Configuration.Runnables.ContainsKey(advancer.NextRunnable.RunnableName))
                Configuration.Runnables.Add(advancer.NextRunnable.RunnableName, advancer.NextRunnable);
            advancer.NextRunnable.Orchestrator = Configuration;
            fromRunnable.AddAdvancer(advancer);
        }

        fromRunnable.AllowsParallelAdvances = true;
        return this;
    }


    public OrchestrationBuilder AddCombinationalAdvancement<TValue>(
        OrchestrationRunnableBase[] fromRunnables, 
        AdvancementRequirement<TValue> condition, 
        OrchestrationRunnableBase toRunnable, 
        int? requiredInputToAdvance = null, 
        string combinationRunnableName = "")
    {
        requiredInputToAdvance ??= fromRunnables.Length;
        Configuration.Runnables.TryAdd(toRunnable.RunnableName, toRunnable);

        CombinationalWaiterRunnable<TValue> combinationalWaiter = new CombinationalWaiterRunnable<TValue>(
            Configuration,
            combinationRunnableName,
            requiredInputToAdvance.Value);

        Configuration.Runnables.TryAdd(combinationalWaiter.RunnableName, combinationalWaiter); 

        foreach (OrchestrationRunnableBase fromRunnable in fromRunnables)
        {
            Configuration.Runnables.TryAdd(fromRunnable.RunnableName, fromRunnable);
            fromRunnable.Orchestrator = Configuration;
            fromRunnable.AddAdvancer(new OrchestrationAdvancer<TValue>(condition, combinationalWaiter));
        }
        
        toRunnable.Orchestrator = Configuration;
        combinationalWaiter.AddAdvancer((req) => { 
            if(req is null) return false;
            return req.InputCount >= req.RequiredInputCount; 
        }, 
        toRunnable);

        return this;
    }


    public OrchestrationBuilder AddExitPath<TOutput>(OrchestrationRunnableBase fromRunnable, AdvancementRequirement<TOutput> condition)
    {
        ExitRunnable<TOutput> toRunnable = new ExitRunnable<TOutput>(Configuration, "Tornado_Exit");
        Configuration.Runnables.TryAdd(fromRunnable.RunnableName, fromRunnable);
        Configuration.Runnables.TryAdd(toRunnable.RunnableName, toRunnable);

        fromRunnable.Orchestrator = Configuration;
        toRunnable.Orchestrator = Configuration;
        fromRunnable.AddAdvancer(new OrchestrationAdvancer<TOutput>(condition, toRunnable));
        return this;
    }

    public OrchestrationBuilder CreateDotGraphVisualization(string filePath, string graphName = "OrchestrationGraph")
    {
        OrchestrationVisualization.SaveDotGraphToFile(Configuration, filePath, graphName);
        return this;
    }

    public OrchestrationBuilder CreatePlantUmlVisualization(string filePath, string graphName = "OrchestrationGraph")
    {
        OrchestrationVisualization.SavePlantUMLToFile(Configuration, filePath, graphName);
        return this;
    }

    /// <summary>
    /// Validates the orchestration graph for type safety and structural correctness.
    /// This method should be called before Build() to ensure the graph is properly configured.
    /// </summary>
    /// <param name="throwOnError">If true, throws GraphValidationException on validation errors. If false, returns validation result.</param>
    /// <returns>The validation result containing any errors or warnings</returns>
    public GraphValidationResult Validate(bool throwOnError = true)
    {
        var result = GraphCompiler.ValidateGraph(Configuration);
        
        if (throwOnError)
        {
            result.ThrowIfInvalid();
        }
        
        return result;
    }

    /// <summary>
    /// Compiles and validates the orchestration graph, then builds the configuration.
    /// This is the recommended method to build an orchestration as it ensures type safety.
    /// </summary>
    /// <param name="validateGraph">If true, validates the graph structure before building</param>
    /// <returns>The compiled and validated orchestration configuration</returns>
    public OrchestrationRuntimeConfiguration Compile(bool validateGraph = true)
    {
        if (validateGraph)
        {
            Validate(throwOnError: true);
        }
        
        return Build();
    }

    public OrchestrationRuntimeConfiguration Build()
    {
        return Configuration;
    }
}
public class CombinationalResult<TValue>
{
    public List<TValue> Values { get; set; }
    public int InputCount { get; set; } = 0;
    public int RequiredInputCount { get; set; } = 1;

    public CombinationalResult(List<TValue> values)
    {
        Values = values;
        InputCount = 0;
    }
}

public class CombinationalWaiterRunnable<TValue> : OrchestrationRunnable<TValue, CombinationalResult<TValue>>
{
    public int RequiredInputCount { get; set; } = 0;
    public CombinationalWaiterRunnable(OrchestrationRuntimeConfiguration configuration, string? runnableName = "", int requiredInputCount = 1)
        : base(configuration, runnableName)
    {
        SingleInvokeForProcesses = true;
        RequiredInputCount = requiredInputCount;
    }

    public override ValueTask<CombinationalResult<TValue>> Invoke(RunnableProcess<TValue, CombinationalResult<TValue>> input)
    {
        CombinationalResult<TValue> current = new CombinationalResult<TValue>(Input) { InputCount = Input.Count, RequiredInputCount = this.RequiredInputCount };
        return new ValueTask<CombinationalResult<TValue>>(current);
    }
}