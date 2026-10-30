using Core.Native;

namespace Core.Safety;

/// <summary>
/// Watches a global hotkey and fires the kill switch when it is pressed.
/// </summary>
/// <remarks>
/// Polls key state rather than installing a WH_KEYBOARD_LL hook. A low-level hook needs
/// a message pump on a dedicated thread and a callback that must outlive garbage
/// collection; polling costs a check every <see cref="PollInterval"/> and is far simpler
/// to reason about. The resulting latency is irrelevant at human timescales.
/// </remarks>
public sealed class HotkeyKillSwitchMonitor : IDisposable
{
    private readonly INativeApi _native;
    private readonly IKillSwitch _killSwitch;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;
    private bool _disposed;

    public HotkeyKillSwitchMonitor(INativeApi native, IKillSwitch killSwitch, VirtualKey hotkey = VirtualKey.F12)
    {
        ArgumentNullException.ThrowIfNull(native);
        ArgumentNullException.ThrowIfNull(killSwitch);

        _native = native;
        _killSwitch = killSwitch;
        Hotkey = hotkey;
    }

    public VirtualKey Hotkey { get; }

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(50);

    public bool IsRunning => _loop is { IsCompleted: false };

    /// <summary>
    /// Checks the hotkey once and fires the kill switch if it is held.
    /// </summary>
    /// <returns>True when this call triggered the kill switch.</returns>
    public bool Poll()
    {
        if (_killSwitch.IsTriggered || !_native.IsKeyDown(Hotkey))
        {
            return false;
        }

        _killSwitch.Trigger($"{Hotkey} pressed");
        return true;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
        {
            return;
        }

        _loop = Run(_stopping.Token);
    }

    private async Task Run(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Poll())
                {
                    return;
                }

                await Task.Delay(PollInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _stopping.Cancel();
        _stopping.Dispose();
    }
}
