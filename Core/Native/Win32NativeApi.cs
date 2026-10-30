using System.Drawing;

namespace Core.Native;

/// <summary>
/// Real Win32 implementation of <see cref="INativeApi"/>.
/// Deliberately kept free of branching logic: everything here is a thin translation
/// to a P/Invoke, so the interesting behavior lives in testable callers.
/// </summary>
public sealed unsafe class Win32NativeApi : INativeApi
{
    public bool IsWindow(nint hWnd) => hWnd != 0 && NativeMethods.IsWindow(hWnd);

    public bool IsMinimized(nint hWnd) => NativeMethods.IsIconic(hWnd);

    public bool TryGetClientBounds(nint hWnd, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;

        if (!NativeMethods.GetClientRect(hWnd, out var rect))
        {
            return false;
        }

        NativeMethods.Point origin = default;

        if (!NativeMethods.ClientToScreen(hWnd, ref origin))
        {
            return false;
        }

        // GetClientRect always reports Left/Top as zero, so Right/Bottom are the client extents.
        bounds = new Rectangle(origin.X, origin.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return true;
    }

    public nint GetForegroundWindow() => NativeMethods.GetForegroundWindow();

    public bool SetForegroundWindow(nint hWnd) => NativeMethods.SetForegroundWindow(hWnd);

    public bool RestoreWindow(nint hWnd) => NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);

    public Rectangle GetVirtualScreenBounds() => new(
        NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));

    public Point GetCursorPosition()
    {
        _ = NativeMethods.GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }

    public void SendMouseMoveAbsolute(int normalizedX, int normalizedY) => SendMouse(
        normalizedX,
        normalizedY,
        NativeMethods.MOUSEEVENTF_ABSOLUTE | NativeMethods.MOUSEEVENTF_MOVE | NativeMethods.MOUSEEVENTF_VIRTUALDESK,
        0);

    public void SendMouseButton(MouseButton button, bool down)
    {
        uint flags = button switch
        {
            MouseButton.Left => down ? NativeMethods.MOUSEEVENTF_LEFTDOWN : NativeMethods.MOUSEEVENTF_LEFTUP,
            MouseButton.Right => down ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_RIGHTUP,
            MouseButton.Middle => down ? NativeMethods.MOUSEEVENTF_MIDDLEDOWN : NativeMethods.MOUSEEVENTF_MIDDLEUP,
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        };

        SendMouse(0, 0, flags, 0);
    }

    public void SendMouseWheel(int notches) =>
        SendMouse(0, 0, NativeMethods.MOUSEEVENTF_WHEEL, unchecked((uint)(notches * NativeMethods.WHEEL_DELTA)));

    public void SendKey(VirtualKey key, bool down)
    {
        var input = new NativeMethods.Input()
        {
            Type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                Ki = new NativeMethods.KeyboardInput
                {
                    WVk = (ushort)key,
                    DwFlags = down ? NativeMethods.KEYEVENTF_KEYDOWN : NativeMethods.KEYEVENTF_KEYUP,
                },
            },
        };

        _ = NativeMethods.SendInput(1, &input, sizeof(NativeMethods.Input));
    }

    public void SendUnicodeCharacter(char character)
    {
        // KEYEVENTF_UNICODE ignores WVk and delivers WScan verbatim, so the active
        // keyboard layout cannot mangle the character.
        var inputs = stackalloc NativeMethods.Input[2];

        inputs[0] = new NativeMethods.Input
        {
            Type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                Ki = new NativeMethods.KeyboardInput
                {
                    WScan = character,
                    DwFlags = NativeMethods.KEYEVENTF_UNICODE,
                },
            },
        };

        inputs[1] = new NativeMethods.Input
        {
            Type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                Ki = new NativeMethods.KeyboardInput
                {
                    WScan = character,
                    DwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP,
                },
            },
        };

        _ = NativeMethods.SendInput(2, inputs, sizeof(NativeMethods.Input));
    }

    public bool IsKeyDown(VirtualKey key) => (NativeMethods.GetAsyncKeyState((int)key) & 0x8000) != 0;

    private static void SendMouse(int dx, int dy, uint flags, uint mouseData)
    {
        var input = new NativeMethods.Input()
        {
            Type = NativeMethods.INPUT_MOUSE,
            U = new NativeMethods.InputUnion
            {
                Mi = new NativeMethods.MouseInput
                {
                    Dx = dx,
                    Dy = dy,
                    MouseData = mouseData,
                    DwFlags = flags,
                },
            },
        };

        _ = NativeMethods.SendInput(1, &input, sizeof(NativeMethods.Input));
    }
}
