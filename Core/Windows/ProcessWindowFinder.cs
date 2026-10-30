using System.Diagnostics;

namespace Core.Windows;

/// <summary>Finds windows by enumerating live processes.</summary>
public sealed class ProcessWindowFinder : IWindowFinder
{
    public nint FindMainWindow(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        var processes = Process.GetProcessesByName(processName);

        try
        {
            // A process that is still starting reports a zero handle, so prefer one that
            // has actually created its window rather than taking the first match blindly.
            foreach (var process in processes)
            {
                if (process.MainWindowHandle != 0)
                {
                    return process.MainWindowHandle;
                }
            }

            return 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
