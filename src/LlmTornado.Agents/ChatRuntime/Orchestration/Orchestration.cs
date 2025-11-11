using LlmTornado.Agents.DataModels;
using LlmTornado.Chat;
using LlmTornado.Code;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace LlmTornado.Agents.ChatRuntime.Orchestration;

/// <summary>
/// Represents a state machine that manages the execution of state processes with support for concurrency and
/// cancellation. [SUGGEST USING RuntimeMachine&lt;TInput, TOutput&gt;]
/// </summary>
/// <remarks>The <see cref="RuntimeMachine"/> class provides mechanisms to initialize, run, and manage state
/// processes. It supports concurrent execution of processes up to a specified maximum number of threads, and allows
/// for graceful stopping and cancellation of operations. The state machine can be reset and reused for multiple
/// runs.</remarks>
public abstract class Orchestration
{
    /// <summary>
    /// Gets or sets the initial state of the system or process.
    /// </summary>
    public OrchestrationRunnableBase InitialRunnable { get; protected set; }
    /// <summary>
    /// Triggers events related to the state machine, such as state transitions and errors.
    /// </summary>
    public Action<OrchestrationEvent>? OnOrchestrationEvent { get; set; }

    /// <summary>
    /// List of processes that will be run in the state machine this tick.
    /// </summary>
    /// [SerializationRequired]
    internal List<OrchestrationRunnableBase> CurrentRunnablesWithProcesses { get; private set; } = [];

    /// <summary>
    /// Need this to collect new processes while the current ones can be exiting
    /// </summary>
    private List<RunnableProcess> newRunnableProcesses = [];

    /// <summary>
    /// Trigger to stop the state machine.
    /// </summary>
    public CancellationTokenSource StopTrigger = new CancellationTokenSource();

    /// <summary>
    /// Signals whether the process has completed.
    /// </summary>
    private bool _isCompleted = false;

    /// <summary>
    /// You can use this to store runtime properties that you want to access later or in other states.
    /// </summary>
    /// [SerializationRequired]
    public ConcurrentDictionary<string, object> RuntimeProperties { get; set; } = new ConcurrentDictionary<string, object>();

    /// <summary>
    /// Gets the <see cref="CancellationToken"/> used to signal cancellation of the current operation.
    /// </summary>
    public CancellationToken CancelToken => StopTrigger.Token;

    /// <summary>
    /// Gets or sets a value indicating whether the process is complete.
    /// </summary>
    public bool IsCompleted => _isCompleted;


    // [consideration] I actually don't have this set anywhere.. I need this to have all the states but states are internally linked to each other.
    /// <summary>
    /// Gets or sets the collection of states.
    /// </summary>
    public Dictionary<string, OrchestrationRunnableBase> Runnables { get; set; } = new Dictionary<string, OrchestrationRunnableBase>();

    /// <summary>
    /// Gets or sets a value indicating whether the steps of the process should be recorded.
    /// </summary>
    public bool RecordSteps { get; set; } = false;

    /// <summary>
    /// Gets or sets the collection of steps, where each step is represented as a list of runnable processes.
    /// </summary>
    /// [SerializationRequired]
    public ConcurrentDictionary<int, List<RunnerRecord>> RunSteps { get; set; } = new ConcurrentDictionary<int, List<RunnerRecord>>();

    private int _stepCounter = 0;

    private bool _isInitialized = false;

    /// <summary>
    /// Gets or sets the orchestration configuration options.
    /// </summary>
    public OrchestrationOptions Options { get; set; } = new OrchestrationOptions();

    public Orchestration()
    {

    }

    /// <summary>
    /// Logs a debug message if debug logging is enabled.
    /// This method provides a centralized logging mechanism that can be easily modified in the future.
    /// </summary>
    /// <param name="message">The debug message to log.</param>
    protected internal void LogDebug(string message)
    {
        if (Options.Debug)
        {
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Marks the current operation as finished.
    /// </summary>
    /// <remarks>Sets the <see cref="IsCompleted"/> property to <see langword="true"/>, indicating that
    /// the operation is complete.</remarks>
    public void HasCompletedSuccessfully() { _isCompleted = true; }

    /// <summary>
    /// Used to stop the state machine and cancel any ongoing operations.
    /// </summary>
    public void Cancel() => StopTrigger.Cancel();

    /// <summary>
    /// Add a record of the current step in the orchestration process.
    /// </summary>
    /// <param name="record"></param>
    public void AddRecordStep(RunnerRecord record)
    {
        if (!RecordSteps) return;

        RunSteps.AddOrUpdate(_stepCounter, [record], (key, existingList) =>
        {
            existingList.Add(record);
            return existingList;
        });
    }
    /// <summary>
    /// Asynchronously exits all active runnables.
    /// </summary>
    /// <remarks>This method initiates the exit sequence for each active runnable and waits for all
    /// runnables to complete their exit operations.  It runs the exit operations concurrently to improve
    /// performance.</remarks>
    /// <returns></returns>
    private async Task ExitCurrentRunnableProcesses()
    {
        List<Task> Tasks = [];
        CurrentRunnablesWithProcesses.ForEach(process => {
            Tasks.Add(Task.Run(async () => await BeginExit(process), CancelToken));
        });
        await Task.WhenAll(Tasks);
        Tasks.Clear();

        OnOrchestrationEvent?.Invoke(new OnVerboseOrchestrationEvent("Finished Exiting all runnables!"));
    }

    private async Task BeginExit(OrchestrationRunnableBase runnable)
    {
        await runnable._CleanupRunnable();
        OnOrchestrationEvent?.Invoke(new OnFinishedRunnableEvent(runnable)); //Invoke the exit state machine event
    }

    /// <summary>
    /// Processes each active process asynchronously, ensuring that the number of concurrent executions is limited
    /// by the thread limiter.
    /// </summary>
    /// <remarks>This method runs each active process in parallel, respecting the concurrency limits
    /// imposed by the <c>threadLimiter</c>. It waits for all processes to complete before returning.</remarks>
    /// <returns></returns>
    private async Task ProcessTick()
    {
        //If there are no processes, return
        if (CurrentRunnablesWithProcesses.Count == 0) { HasCompletedSuccessfully(); return; }

        _stepCounter++;

        OnOrchestrationEvent?.Invoke(new OnTickOrchestrationEvent()); //Invoke the tick state machine event
        List<Task> tasks = [];
        
        CurrentRunnablesWithProcesses.ForEach(runnable => tasks.Add(Task.Run(async () =>
        {
            OnOrchestrationEvent?.Invoke(new OnInvokedRunnableEvent(runnable)); //Invoke the state entered event
            await runnable.Invoke(); //Invoke the state process
        }, CancelToken)));

        //Wait for collection
        await Task.WhenAll(tasks);
        tasks.Clear();
    }

    /// <summary>
    /// Initializes the specified state process by setting its current state machine and adding it to the active
    /// processes.
    /// </summary>
    /// <remarks>This method ensures thread-safe access to the state machine by using a semaphore. It
    /// sets the current state machine for the process if it is not already set and adds the process to the list of
    /// active processes. After initialization, it enters the state of the process.</remarks>
    /// <param name="process">The state process to initialize. This parameter cannot be null.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">Thrown when process or process.Runnable is null.</exception>
    private static void AddProcessToRunnable(RunnableProcess process)
    {
        if (process?.Runner == null)
            throw new ArgumentNullException(nameof(process), "Process and its Runtime cannot be null");

        process.Runner.AddRunnableProcess(process); //preset input
    }

    private async Task InitializeRunnable(OrchestrationRunnableBase runnable)
    {
        //Gain access to state machine
        runnable.Orchestrator ??= this; //Set the current state machine if not already set

        OnOrchestrationEvent?.Invoke(new OnStartedRunnableEvent(runnable)); //Invoke the state entered event  

        await runnable._InitializeRunnable();
    }

    /// <summary>
    /// Initializes all new state processes asynchronously.
    /// </summary>
    /// <remarks>This method clears the current active processes and initializes each new state
    /// process concurrently. After initialization, it ensures that only distinct state processes, based on their
    /// state ID, remain active.</remarks>
    /// <returns></returns>
    private async Task SetCurrentRunnableProcesses()
    {
        //Clear all the active processes
        CurrentRunnablesWithProcesses.Clear();

        OnOrchestrationEvent?.Invoke(new OnVerboseOrchestrationEvent($"Initializing {newRunnableProcesses.Count} new state processes..."));

        //If there are no new processes, return
        if (newRunnableProcesses.Count == 0) { HasCompletedSuccessfully(); return; }

        //Setup each state with the process input
        foreach (RunnableProcess process in newRunnableProcesses)
        {
            AddProcessToRunnable(process);
        }

        //This is to remove running the same state twice with two processes.. it gets input from _EnterRuntime
        // Replace the DistinctBy line with this GroupBy approach which is compatible with .NET Standard 2.0
        CurrentRunnablesWithProcesses = newRunnableProcesses.Select(process => process.Runner)
            .GroupBy(state => state.Id)
            .Select(g => g.First())
            .ToList();

        List<Task> tasks = [];
        //Setup each state with the process input
        foreach (OrchestrationRunnableBase runnable in CurrentRunnablesWithProcesses)
        {
            tasks.Add(Task.Run(async () => await InitializeRunnable(runnable), CancelToken));
        }
        await Task.WhenAll(tasks);
        tasks.Clear();

        _isInitialized = true; //Set the initialized flag to true after the process is initialized
    }

    /// <summary>
    /// Retrieves a list of new state processes based on the current conditions.
    /// </summary>
    /// <remarks>This method evaluates each active process and collects new state processes that meet
    /// specific conditions. The returned list may be empty if no new state processes are identified.</remarks>
    /// <returns>A list of <see cref="RunnableProcess"/> objects representing the new state processes that meet the specified
    /// conditions. The list will be empty if no new processes are found.</returns>
    private void SetNewRunnableProcesses()
    {
        newRunnableProcesses.Clear();
        CurrentRunnablesWithProcesses.ForEach(process => {
            newRunnableProcesses.AddRange(process.CanAdvance() ?? []);
        });

        OnOrchestrationEvent?.Invoke(new OnVerboseOrchestrationEvent("Finished validating Runtime conditions for transition"));
    }

    /// <summary>
    /// Determines whether the current operation is finished and triggers the necessary actions if so.
    /// </summary>
    /// <remarks>If the operation is finished, this method will asynchronously exit all processes and
    /// invoke the <see cref="OnFinishedTriggered"/> event.</remarks>
    /// <returns><see langword="true"/> if the operation is finished and the exit processes have been triggered; otherwise,
    /// <see langword="false"/>.</returns>
    private async Task<bool> CheckIfCompleted()
    {
        if (IsCompleted)
        {
            await ExitCurrentRunnableProcesses();
            OnOrchestrationEvent?.Invoke(new OnFinishedOrchestrationEvent()); //Invoke the finished state machine event
            return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether the cancellation has been requested and handles the cancellation process.
    /// </summary>
    /// <remarks>If the cancellation is requested, this method triggers the <see
    /// cref="OnCancelledOrchestrationEvent"/> event, exits all processes asynchronously, and returns <see
    /// langword="true"/>. Otherwise, it returns <see langword="false"/>.</remarks>
    /// <returns><see langword="true"/> if the cancellation has been requested and handled; otherwise, <see
    /// langword="false"/>.</returns>
    private async Task<bool> CheckIfCancelled()
    {
        if (StopTrigger.IsCancellationRequested)
        {
            //what if invoking the state? How do we handle that?
            await ExitCurrentRunnableProcesses();
            OnOrchestrationEvent?.Invoke(new OnCancelledOrchestrationEvent()); //Invoke the cancelled state machine event
            return true;
        }
        return false;
    }

    /// <summary>
    /// Resets the current state machine, preparing the system for a new execution cycle.
    /// </summary>
    /// <remarks>This method sets the <see cref="IsCompleted"/> flag to <see langword="false"/>, resets
    /// the stop trigger,  and clears the list of active processes. It should be called before starting a new run to
    /// ensure  that the system is in a clean state.</remarks>
    private void ResetRun()
    {
        OnOrchestrationEvent?.Invoke(new OnVerboseOrchestrationEvent("Resetting RuntimeMachine"));
        _isCompleted = false;
        _stepCounter = 0;
        _isInitialized = false;
        StopTrigger = new CancellationTokenSource(); //Reset the stop trigger
        CurrentRunnablesWithProcesses.Clear();
        newRunnableProcesses.Clear();
    }

    /// <summary>
    /// Sets the initial state for the entry point of the state machine.
    /// </summary>
    /// <param name="initialRunnable">The initial state to be set. Must have an input type assignable to <typeparamref name="TInput"/>.</param>
    /// <exception cref="InvalidCastException">Thrown if the input type of <paramref name="initialRunnable"/> is not assignable to <typeparamref
    /// name="TInput"/>.</exception>
    public virtual void SetEntryRunnable(OrchestrationRunnableBase initialRunnable)
    {
        InitialRunnable = initialRunnable;
    }

    internal virtual async Task Initialize(object? input = null)
    {
        ResetRun(); //Reset the state machine before running

        newRunnableProcesses.Add(new RunnableProcess(InitialRunnable, input));

        //Initialize the process with the starting state and input
        await SetCurrentRunnableProcesses();

        OnOrchestrationEvent?.Invoke(new OnVerboseOrchestrationEvent("Initilization Completed!")); //Invoke the begin state machine event
    }

    /// <summary>
    /// Invokes the state machine asynchronously, starting from the initial state and processing the provided input.
    /// </summary>
    /// <param name="input"> Input result for the Orchestration</param>
    /// <returns></returns>
    public async Task InvokeAsync(object? input = null)
    {
        await Initialize(input);
        await RunToCompletion();
    }

    internal async Task RunToCompletion()
    {
        if(!_isInitialized)
        {
            throw new InvalidOperationException("Runtime has not been initialized. Call InitializeRuntime() before running to completion.");
        }

        OnOrchestrationEvent?.Invoke(new OnBeginOrchestrationEvent()); //Invoke the begin state machine event

        while (!IsCompleted && !StopTrigger.IsCancellationRequested)
        {
            await InvokeStep();
        }
    }

    private async Task InvokeStep()
    {
        if(!_isInitialized)
        {
            throw new InvalidOperationException("Runtime has not been initialized. Call InitializeRuntime() before invoking steps.");
        }

        //Collect each state Result
        await ProcessTick();

        //stop the state machine if needed & exit all states
        if(await CheckIfCompleted()) return;


        if(await CheckIfCancelled()) return;

        //Create List of transitions to new states from conditional movement
        SetNewRunnableProcesses();

        //Exit the current Processes
        await ExitCurrentRunnableProcesses();

        //Reset Active Processes Here
        await SetCurrentRunnableProcesses();
    }

    internal void OnStartingRunnableProcess(RunnableProcess process)
    {
        OnOrchestrationEvent?.Invoke(new OnStartedRunnableProcessEvent(process));
    }

    internal void OnFinishedRunnableProcess(RunnableProcess process)
    {
        OnOrchestrationEvent?.Invoke(new OnFinishedRunnableProcessEvent(process));
    }
}

/// <summary>
/// Represents a state machine that processes inputs of type <typeparamref name="TInput"/> and produces outputs of
/// type <typeparamref name="TOutput"/>.
/// </summary>
/// <remarks>The <see cref="Orchestration{TInput, TOutput}"/> class allows for the execution of a series of
/// states, starting from a specified entry state and concluding at a result state.</remarks>
/// <typeparam name="TInput">The type of input that the state machine processes.</typeparam>
/// <typeparam name="TOutput">The type of output that the state machine produces.</typeparam>
public class Orchestration<TInput, TOutput> : Orchestration
{
    /// <summary>
    /// Executes the asynchronous operation, initializing with the specified input and running to completion.
    /// </summary>
    /// <param name="input">The input parameter used to initialize the operation. Can be <see langword="null"/> or the default value of
    /// <typeparamref name="TInput"/>.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<TOutput[]?> InvokeAsync(TInput? input = default)
    {
        await Initialize(input);
        await RunToCompletion();
        return Results;
    }

    /// <summary>
    /// Result of the state machine run, containing a Array of outputs of type <typeparamref name="TOutput"/>.
    /// </summary>
    public TOutput[]? Results => RunnableWithResult.GetBaseResults().ToList().Select(result=> (TOutput)result).ToArray();

    /// <summary>
    /// Retrieves the results as a list of objects.
    /// </summary>
    /// <remarks>This method converts the items in the <c>Results</c> collection to a list of objects. If
    /// <c>Results</c> is <see langword="null"/>, the method returns <see langword="null"/>.</remarks>
    /// <returns>A list of objects converted from the <c>Results</c> collection, or <see langword="null"/> if <c>Results</c> is
    /// <see langword="null"/>.</returns>
    public List<object>? GetResults()
    {
        return RunnableWithResult.GetBaseResults().ToList()!;
    }

    /// <summary>
    /// Gets or sets the result state of the operation.
    /// </summary>
    public OrchestrationRunnableBase RunnableWithResult { get; private set; }

    public Orchestration() { }

    /// <summary>
    /// Executes the state machine starting from the specified start state and processes the given input.
    /// </summary>
    /// <param name="input">The input data to be processed by the state machine.</param>
    /// <returns>A task representing the asynchronous operation, containing a list of results produced by the state machine.
    /// Each result corresponds to an output from the state machine, and may be null if no output is produced for a
    /// particular state transition.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the start state or result state is not set before execution.</exception>
    private async Task Initialize(TInput? input = default)
    {
        //Input validation before running the state machine
        if (InitialRunnable == null)
        {
            throw new InvalidOperationException("Need to Set a Start Runtime for the Resulting RuntimeMachine");
        }

        if (RunnableWithResult == null)
        {
            throw new InvalidOperationException("Need to Set a Result Runtime for the Resulting RuntimeMachine");
        }

        await base.Initialize(input);
    }


    /// <summary>
    /// Sets the initial state for the entry point of the state machine.
    /// </summary>
    /// <param name="initialRunnable">The initial state to be set. Must have an input type assignable to <typeparamref name="TInput"/>.</param>
    /// <exception cref="InvalidCastException">Thrown if the input type of <paramref name="initialRunnable"/> is not assignable to <typeparamref
    /// name="TInput"/>.</exception>
    public override void SetEntryRunnable(OrchestrationRunnableBase initialRunnable)
    {
        if (!typeof(TInput).IsAssignableFrom(initialRunnable.GetInputType()))
        {
            var expectedTypeName = GetFriendlyTypeName(typeof(TInput));
            var actualTypeName = GetFriendlyTypeName(initialRunnable.GetInputType());
            
            throw new InvalidCastException(
                $"Type mismatch for entry runnable '{initialRunnable.RunnableName}': " +
                $"The orchestration expects input type '{expectedTypeName}' but the runnable accepts '{actualTypeName}'. " +
                $"Ensure your entry runnable can accept '{expectedTypeName}' as its input type.");
        }

        InitialRunnable = initialRunnable;
    }

    /// <summary>
    /// Sets the output state for the current state machine.
    /// </summary>
    /// <remarks>This method updates the current output state, ensuring type compatibility with the
    /// expected output type.</remarks>
    /// <param name="outputRunnable">The state to be set as the output. Must have an output type assignable to <typeparamref name="TOutput"/>.</param>
    /// <exception cref="InvalidCastException">Thrown if the output type of <paramref name="outputRunnable"/> is not assignable to <typeparamref
    /// name="TOutput"/>.</exception>
    public void SetRunnableWithResult(OrchestrationRunnableBase outputRunnable)
    {
        if (!typeof(TOutput).IsAssignableFrom(outputRunnable.GetOutputType()))
        {
            var expectedTypeName = GetFriendlyTypeName(typeof(TOutput));
            var actualTypeName = GetFriendlyTypeName(outputRunnable.GetOutputType());
            
            throw new InvalidCastException(
                $"Type mismatch for result runnable '{outputRunnable.RunnableName}': " +
                $"The orchestration expects output type '{expectedTypeName}' but the runnable produces '{actualTypeName}'. " +
                $"Ensure your result runnable outputs '{expectedTypeName}' or a compatible type.");
        }

        RunnableWithResult = outputRunnable;
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
}

