namespace Core.Scripting;

/// <summary>Lifecycle state of a script run.</summary>
public enum ScriptState
{
    Idle,
    Running,
    Completed,
    Stopped,
    Faulted,
}
