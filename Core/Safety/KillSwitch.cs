namespace Core.Safety;

/// <summary>Thread-safe, one-shot kill switch backed by a cancellation token source.</summary>
public sealed class KillSwitch : IKillSwitch, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _gate = new();
    private bool _triggered;
    private bool _disposed;

    public CancellationToken Token => _cts.Token;

    public bool IsTriggered
    {
        get
        {
            lock (_gate)
            {
                return _triggered;
            }
        }
    }

    /// <summary>The reason supplied by the first trigger, or null while untriggered.</summary>
    public string? Reason { get; private set; }

    public event EventHandler<string>? Triggered;

    public void Trigger(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        lock (_gate)
        {
            if (_triggered || _disposed)
            {
                return;
            }

            _triggered = true;
            Reason = reason;
        }

        // Cancel before notifying so any handler already sees a canceled token.
        _cts.Cancel();

        Triggered?.Invoke(this, reason);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _cts.Dispose();
    }
}
