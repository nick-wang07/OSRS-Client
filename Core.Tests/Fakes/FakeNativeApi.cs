using Core.Native;
using System.Drawing;

namespace Core.Tests.Fakes;

/// <summary>
/// Records the input it is asked to send and serves scripted window state, so behavior
/// can be asserted without touching the real desktop.
/// </summary>
public sealed class FakeNativeApi : INativeApi
{
    public HashSet<nint> LiveWindows { get; } = [];

    public HashSet<nint> MinimizedWindows { get; } = [];

    public Dictionary<nint, Rectangle> ClientBounds { get; } = [];

    public HashSet<VirtualKey> KeysDown { get; } = [];

    public nint ForegroundWindow { get; set; }

    public Rectangle VirtualScreen { get; set; } = new(0, 0, 1920, 1080);

    public Point CursorPosition { get; set; }

    /// <summary>When set, foreground requests are refused, mimicking Windows' foreground lock.</summary>
    public bool RefuseForeground { get; set; }

    /// <summary>Number of polls before a granted foreground request takes effect.</summary>
    public int ForegroundDelayPolls { get; set; }

    private int _pendingForegroundPolls = -1;
    private nint _pendingForegroundWindow;

    public List<Point> MouseMoves { get; } = [];

    public List<(MouseButton Button, bool Down)> ButtonEvents { get; } = [];

    public List<int> WheelEvents { get; } = [];

    public List<(VirtualKey Key, bool Down)> KeyEvents { get; } = [];

    public List<char> TypedCharacters { get; } = [];

    /// <summary>
    /// Hooks fired as input is emitted. Tests use these to cancel partway through an
    /// operation deterministically, rather than racing a timer against it.
    /// </summary>
    public Action<Point>? OnMouseMove { get; set; }

    public Action<MouseButton, bool>? OnButtonEvent { get; set; }

    public Action<VirtualKey, bool>? OnKeyEvent { get; set; }

    public int RestoreCallCount { get; private set; }

    public int SetForegroundCallCount { get; private set; }

    public bool IsWindow(nint hWnd) => hWnd != 0 && LiveWindows.Contains(hWnd);

    public bool IsMinimized(nint hWnd) => MinimizedWindows.Contains(hWnd);

    public bool TryGetClientBounds(nint hWnd, out Rectangle bounds) =>
        ClientBounds.TryGetValue(hWnd, out bounds);

    public nint GetForegroundWindow()
    {
        if (_pendingForegroundPolls >= 0 && _pendingForegroundPolls-- == 0)
        {
            ForegroundWindow = _pendingForegroundWindow;
        }

        return ForegroundWindow;
    }

    public bool SetForegroundWindow(nint hWnd)
    {
        SetForegroundCallCount++;

        if (RefuseForeground)
        {
            return false;
        }

        if (ForegroundDelayPolls > 0)
        {
            _pendingForegroundPolls = ForegroundDelayPolls;
            _pendingForegroundWindow = hWnd;
        }
        else
        {
            ForegroundWindow = hWnd;
        }

        return true;
    }

    public bool RestoreWindow(nint hWnd)
    {
        RestoreCallCount++;
        MinimizedWindows.Remove(hWnd);
        return true;
    }

    public Rectangle GetVirtualScreenBounds() => VirtualScreen;

    public Point GetCursorPosition() => CursorPosition;

    public void SendMouseMoveAbsolute(int normalizedX, int normalizedY)
    {
        var point = new Point(normalizedX, normalizedY);
        MouseMoves.Add(point);
        OnMouseMove?.Invoke(point);
    }

    public void SendMouseButton(MouseButton button, bool down)
    {
        ButtonEvents.Add((button, down));
        OnButtonEvent?.Invoke(button, down);
    }

    public void SendMouseWheel(int notches) => WheelEvents.Add(notches);

    public void SendKey(VirtualKey key, bool down)
    {
        KeyEvents.Add((key, down));
        OnKeyEvent?.Invoke(key, down);
    }

    public void SendUnicodeCharacter(char character) => TypedCharacters.Add(character);

    public bool IsKeyDown(VirtualKey key) => KeysDown.Contains(key);

    /// <summary>Registers a live window with the given client bounds.</summary>
    public nint AddWindow(nint handle, Rectangle bounds)
    {
        LiveWindows.Add(handle);
        ClientBounds[handle] = bounds;
        return handle;
    }
}
