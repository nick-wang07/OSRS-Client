namespace Core.Windows;

/// <summary>Locates the main window of a running process by name.</summary>
public interface IWindowFinder
{
    /// <summary>Returns the main window handle, or zero when no matching window exists.</summary>
    nint FindMainWindow(string processName);
}
