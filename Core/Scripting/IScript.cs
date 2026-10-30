namespace Core.Scripting;

/// <summary>
/// A single automated activity.
/// </summary>
/// <remarks>
/// Scripts own their own loop rather than implementing a fixed tick, because OSRS
/// activities differ too much in shape — some poll tightly, others wait minutes.
/// Implementations must observe the cancellation token so the kill switch can stop them.
/// </remarks>
public interface IScript
{
    string Name { get; }

    string Description { get; }

    Task Run(ScriptContext context, CancellationToken cancellationToken);
}
