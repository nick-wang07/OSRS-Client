using System.Diagnostics;
using System.Drawing;
using Core.Native;

namespace Core.Windows;

/// <summary>
/// Tracks the RuneLite window and manages its focus.
/// </summary>
/// <remarks>
/// <para>
/// The bounds reported here are the <em>window client area</em>, which includes RuneLite's
/// own chrome (plugin sidebar, toolbar). That is deliberately not the same thing as the
/// game canvas — locating the 765x503 canvas inside this rectangle needs anchor detection
/// and is handled by the Game layer, not here.
/// </para>
/// </remarks>
public sealed class GameWindowProvider(INativeApi native, IWindowFinder finder) : IFocusTarget
{
    /// <summary>How long <see cref="EnsureFocused"/> waits for Windows to grant foreground.</summary>
    public TimeSpan FocusTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How often foreground state is re-checked while waiting.</summary>
    public TimeSpan FocusPollInterval { get; init; } = TimeSpan.FromMilliseconds(25);

    public nint Handle { get; private set; }

    /// <summary>
    /// True only when a handle is held <em>and</em> the window still exists, so a
    /// closed or restarted client is reported accurately rather than silently no-oping.
    /// </summary>
    public bool IsAttached => Handle != 0 && native.IsWindow(Handle);

    public bool Attach(string processName = "RuneLite")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        nint handle = finder.FindMainWindow(processName);

        Handle = native.IsWindow(handle) ? handle : 0;

        return Handle != 0;
    }

    public void Detach() => Handle = 0;

    /// <summary>
    /// Client area of the RuneLite window in screen coordinates.
    /// Not cached: the window can be moved at any moment, and a stale rectangle
    /// would silently send clicks to the wrong place.
    /// </summary>
    public bool TryGetWindowBounds(out Rectangle bounds)
    {
        if (!IsAttached)
        {
            bounds = Rectangle.Empty;
            return false;
        }

        return native.TryGetClientBounds(Handle, out bounds);
    }

    /// <summary>
    /// Converts a window-relative point to screen coordinates against bounds the caller
    /// already fetched. Prefer this when converting several points so the window is
    /// only queried once.
    /// </summary>
    public static Point ToScreen(Point clientPoint, Rectangle windowBounds) =>
        new(windowBounds.X + clientPoint.X, windowBounds.Y + clientPoint.Y);

    /// <summary>Converts a window-relative point to screen coordinates.</summary>
    public bool TryToScreen(Point clientPoint, out Point screenPoint)
    {
        if (!TryGetWindowBounds(out var bounds))
        {
            screenPoint = Point.Empty;
            return false;
        }

        screenPoint = ToScreen(clientPoint, bounds);
        return true;
    }

    /// <summary>
    /// Brings the game window to the foreground, restoring it first if minimized.
    /// </summary>
    /// <returns>
    /// True once the window actually holds foreground. Windows restricts foreground
    /// stealing, so the request can be refused — this polls to confirm rather than
    /// assuming success after a fixed delay.
    /// </returns>
    public async Task<bool> EnsureFocused(CancellationToken cancellationToken = default)
    {
        if (!IsAttached)
        {
            return false;
        }

        if (native.GetForegroundWindow() == Handle)
        {
            return true;
        }

        // SW_SHOW does not un-minimize; a minimized window needs an explicit restore
        // or input would be delivered to a window nobody can see.
        if (native.IsMinimized(Handle))
        {
            _ = native.RestoreWindow(Handle);
        }

        _ = native.SetForegroundWindow(Handle);

        long deadline = Stopwatch.GetTimestamp() + (long)(FocusTimeout.TotalSeconds * Stopwatch.Frequency);

        while (true)
        {
            if (native.GetForegroundWindow() == Handle)
            {
                return true;
            }

            if (Stopwatch.GetTimestamp() >= deadline)
            {
                return false;
            }

            await Task.Delay(FocusPollInterval, cancellationToken);
        }
    }
}
