namespace Core.Safety;

/// <summary>
/// Emergency stop. Everything the bot runs observes <see cref="Token"/>, so triggering
/// it aborts all in-flight work and hands the mouse back to the user.
/// </summary>
public interface IKillSwitch
{
    /// <summary>Canceled when the kill switch fires.</summary>
    CancellationToken Token { get; }

    bool IsTriggered { get; }

    /// <summary>Fires the kill switch. Safe to call repeatedly; only the first call has effect.</summary>
    void Trigger(string reason);

    /// <summary>Raised once, on the first trigger, carrying the reason.</summary>
    event EventHandler<string>? Triggered;
}
