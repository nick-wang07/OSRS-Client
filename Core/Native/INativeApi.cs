using System.Drawing;

namespace Core.Native;

/// <summary>
/// Managed-facing abstraction over the Win32 calls the bot needs.
/// Exists so window, input and safety logic can be unit tested against a fake
/// instead of the real desktop.
/// </summary>
public interface INativeApi
{
    /// <summary>True when the handle still refers to a live window.</summary>
    bool IsWindow(nint hWnd);

    /// <summary>True when the window is currently minimized.</summary>
    bool IsMinimized(nint hWnd);

    /// <summary>
    /// Client area of the window in screen coordinates.
    /// Returns false when the handle is invalid or the rect cannot be read.
    /// </summary>
    bool TryGetClientBounds(nint hWnd, out Rectangle bounds);

    nint GetForegroundWindow();

    /// <summary>Requests foreground. Windows may refuse; the return value reports whether it accepted.</summary>
    bool SetForegroundWindow(nint hWnd);

    /// <summary>Restores a minimized window.</summary>
    bool RestoreWindow(nint hWnd);

    /// <summary>Bounds of the whole virtual desktop, used to normalize absolute mouse coordinates.</summary>
    Rectangle GetVirtualScreenBounds();

    Point GetCursorPosition();

    /// <summary>Moves the cursor using absolute virtual-desktop coordinates on the 0..65535 grid.</summary>
    void SendMouseMoveAbsolute(int normalizedX, int normalizedY);

    void SendMouseButton(MouseButton button, bool down);

    /// <summary>Scrolls by a signed number of wheel notches. Positive scrolls up.</summary>
    void SendMouseWheel(int notches);

    void SendKey(VirtualKey key, bool down);

    /// <summary>Sends a character as a Unicode keystroke, bypassing keyboard layout.</summary>
    void SendUnicodeCharacter(char character);

    /// <summary>Polled globally, including when the process is not focused.</summary>
    bool IsKeyDown(VirtualKey key);
}
