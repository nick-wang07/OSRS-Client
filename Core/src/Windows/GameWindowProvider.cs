using Core.Native;
using Core.src.Vision;
using System.Diagnostics;
using System.Drawing;

namespace Core.src.Windows;

public sealed class GameWindowProvider
{
    private IntPtr _hwnd;

    public bool IsAttached => _hwnd != IntPtr.Zero;

    public void Attach(string processName = "RuneLite")
    {
        var proc = Process.GetProcessesByName(processName).FirstOrDefault();
        _hwnd = proc?.MainWindowHandle ?? IntPtr.Zero;
    }

    public Rectangle GetCanvasRect()
    {
        if (!IsAttached)
        {
            return Rectangle.Empty;
        }

        NativeMethods.GetClientRect(_hwnd, out var rect);
        NativeMethods.Point topLeft = new() { X = 0, Y = 0 };
        NativeMethods.ClientToScreen(_hwnd, ref topLeft);

        return new(topLeft.X, topLeft.Y, rect.Right, rect.Bottom);
    }

    public async Task EnsureFocused(bool skipFocusCheck = false)
    {
        if (!IsAttached || skipFocusCheck)
        {
            return;
        }

        IntPtr currentForeground = NativeMethods.GetForegroundWindow();

        if (currentForeground != _hwnd)
        {
            NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);

            NativeMethods.SetForegroundWindow(_hwnd);

            await Task.Delay(150).ConfigureAwait(false);
        }
    }

    public Point ToGlobal(Point relativePoint)
    {
        Rectangle canvas = GetCanvasRect();

        return canvas == Rectangle.Empty
            ? Point.Empty
            : new(canvas.X + relativePoint.X, canvas.Y + relativePoint.Y);
    }

    public FastPixelReader TakeSnapshot() => new(_hwnd, GetCanvasRect());

    public IntPtr GetHWND() => _hwnd;
}
