using Core.Safety;

namespace Core.Scripting;

/// <summary>
/// Runs one script at a time on a background task, linking its lifetime to the kill switch.
/// </summary>
/// <remarks>
/// Deliberately single-script: the input layer cannot be driven concurrently, so allowing
/// two scripts at once would interleave mouse events into nonsense.
/// </remarks>
public sealed class ScriptRunner(IKillSwitch killSwitch, IScriptLogger logger) : IDisposable
{
    private readonly Lock _gate = new();
    private CancellationTokenSource? _linked;
    private Task? _run;
    private bool _disposed;

    public ScriptState State { get; private set; } = ScriptState.Idle;

    public IScript? Current { get; private set; }

    /// <summary>Set when the last run ended in <see cref="ScriptState.Faulted"/>.</summary>
    public Exception? Fault { get; private set; }

    public bool IsRunning => State == ScriptState.Running;

    public event EventHandler<ScriptState>? StateChanged;

    /// <summary>Starts a script.</summary>
    /// <exception cref="InvalidOperationException">Another script is already running.</exception>
    public void Start(IScript script, ScriptContext context)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(context);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            if (State == ScriptState.Running)
            {
                throw new InvalidOperationException(
                    $"'{Current?.Name}' is already running; stop it before starting another.");
            }

            if (killSwitch.IsTriggered)
            {
                throw new InvalidOperationException("The kill switch has been triggered; scripts cannot start.");
            }

            _linked?.Dispose();
            _linked = CancellationTokenSource.CreateLinkedTokenSource(killSwitch.Token);

            Current = script;
            Fault = null;
            SetState(ScriptState.Running);

            _run = Execute(script, context, _linked.Token);
        }
    }

    private async Task Execute(IScript script, ScriptContext context, CancellationToken cancellationToken)
    {
        // Yield first so Start returns immediately rather than running the script
        // synchronously up to its first await.
        await Task.Yield();

        try
        {
            logger.Info($"Starting '{script.Name}'.");

            await script.Run(context, cancellationToken);

            logger.Info($"'{script.Name}' completed.");
            SetState(ScriptState.Completed);
        }
        catch (OperationCanceledException)
        {
            logger.Info($"'{script.Name}' stopped.");
            SetState(ScriptState.Stopped);
        }
        catch (Exception exception)
        {
            Fault = exception;
            logger.Error($"'{script.Name}' faulted.", exception);
            SetState(ScriptState.Faulted);
        }
    }

    /// <summary>Requests cancellation and waits for the script to unwind.</summary>
    public async Task Stop()
    {
        Task? run;

        lock (_gate)
        {
            run = _run;
            _linked?.Cancel();
        }

        if (run is not null)
        {
            // Execute swallows script failures into State, so this never rethrows.
            await run;
        }
    }

    private void SetState(ScriptState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _linked?.Cancel();
        _linked?.Dispose();
        _linked = null;
    }
}
