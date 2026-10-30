namespace Core.Windows;

/// <summary>A window that input must be focused on before it is driven.</summary>
public interface IFocusTarget
{
    /// <summary>Brings the target to the foreground. Returns false if focus could not be obtained.</summary>
    Task<bool> EnsureFocused(CancellationToken cancellationToken = default);
}
