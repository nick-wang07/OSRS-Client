using Core.Native;
using Core.src.Windows;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Core.src.Input;

public sealed class WindMouseInput(GameWindowProvider windowProvider) : IInputProvider
{
    private static readonly uint ScrollIncrement = 120;

    private static readonly Random rand = new();

    private struct ScreenMetrics
    {
        public int Left, Top, Width, Height;
    }

    private static ScreenMetrics GetCurrentMetrics() => new()
    {
        Left = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
        Top = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
        Width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
        Height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN)
    };

    private async Task PrepareInput(bool skipFocusCheck = false) => await windowProvider.EnsureFocused(skipFocusCheck).ConfigureAwait(false);

    public async Task MoveMouse(Point destination, double gravity = 9, double wind = 3, double maxStep = 30, double targetArea = 12)
    {
        await PrepareInput().ConfigureAwait(false);

        var metrics = GetCurrentMetrics();
        int loopCount = 0;
        const double sqrt3 = 1.7320508075688772;
        const double sqrt5 = 2.23606797749979;

        NativeMethods.GetCursorPos(out var start);

        double curX = start.X;
        double curY = start.Y;

        double vX = 0, vY = 0, wX = 0, wY = 0;
        double distance = Math.Sqrt(Math.Pow(destination.X - curX, 2) + Math.Pow(destination.Y - curY, 2));

        while (distance >= 1)
        {
            double windMag = Math.Min(wind, distance);

            if (distance >= targetArea)
            {
                wX = wX / sqrt3 + (2 * rand.NextDouble() - 1) * windMag / sqrt5;
                wY = wY / sqrt3 + (2 * rand.NextDouble() - 1) * windMag / sqrt5;
            }
            else
            {
                wX /= sqrt3;
                wY /= sqrt3;
                maxStep = (maxStep < 3) ? (3 * rand.NextDouble() + 3) : (maxStep / sqrt5);
            }

            vX += wX + gravity * (destination.X - curX) / distance;
            vY += wY + gravity * (destination.Y - curY) / distance;

            double vMag = Math.Sqrt(vX * vX + vY * vY);

            if (vMag > maxStep)
            {
                double clip = maxStep / 2 + rand.NextDouble() * maxStep / 2;
                vX = (vX / vMag) * clip;
                vY = (vY / vMag) * clip;
            }

            curX += vX;
            curY += vY;

            ExecuteMove((int)Math.Round(curX), (int)Math.Round(curY), metrics);

            distance = Math.Sqrt(Math.Pow(destination.X - curX, 2) + Math.Pow(destination.Y - curY, 2));

            loopCount += 1;

            if (loopCount % 3 == 0)
            {
                await Task.Delay(1).ConfigureAwait(false);
            }
        }
    }

    private static void ExecuteMove(int x, int y, ScreenMetrics metrics)
    {
        // This is to normalize the input coordinates to the virtual desktop
        // Subtracting vLeft/vTop because 'x' and 'y' are global but the Mickey range (0-65535) covers the entire virtual desktop
        var mX = ((x - metrics.Left) * 65535) / metrics.Width;
        var mY = ((y - metrics.Top) * 65535) / metrics.Height;

        NativeMethods.Input[] inputs =
        [
            new()
            {
                Type = NativeMethods.INPUT_MOUSE,
                U = new()
                {
                    Mi = new()
                    {
                        Dx = mX,
                        Dy = mY,
                        DwFlags = NativeMethods.MOUSEEVENTF_ABSOLUTE |
                                  NativeMethods.MOUSEEVENTF_MOVE |
                                  NativeMethods.MOUSEEVENTF_VIRTUALDESK,
                        Time = 0,
                        DwExtraInfo = IntPtr.Zero
                    }
                }
            },
        ];

        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.Input>());
    }

    public async Task Click()
    {
        await PrepareInput().ConfigureAwait(false);

        SendMouseInput(NativeMethods.MOUSEEVENTF_LEFTDOWN);

        await Task.Delay(rand.Next(60, 120)).ConfigureAwait(false);

        SendMouseInput(NativeMethods.MOUSEEVENTF_LEFTUP);
    }

    public async Task RightClick()
    {
        await PrepareInput().ConfigureAwait(false);

        SendMouseInput(NativeMethods.MOUSEEVENTF_RIGHTDOWN);

        await Task.Delay(rand.Next(60, 120)).ConfigureAwait(false);

        SendMouseInput(NativeMethods.MOUSEEVENTF_RIGHTUP);
    }

    public async Task MoveAndClick(Point destination)
    {
        await MoveMouse(destination).ConfigureAwait(false);

        await Task.Delay(rand.Next(20, 50)).ConfigureAwait(false);

        await Click().ConfigureAwait(false);
    }

    public async Task Scroll(bool up, int clicks = 35)
    {
        await PrepareInput().ConfigureAwait(false);

        for (int i = 0; i < clicks; i++)
        {
            uint data = up ? ScrollIncrement : unchecked((uint)-ScrollIncrement);

            SendMouseInput(NativeMethods.MOUSEEVENTF_WHEEL, data);

            await Task.Delay(rand.Next(3, 10)).ConfigureAwait(false);
        }
    }

    public async Task DragAndDrop(Point start, Point end, double speed = 25)
    {
        await PrepareInput().ConfigureAwait(false);

        await MoveMouse(start, speed).ConfigureAwait(false);

        SendMouseInput(NativeMethods.MOUSEEVENTF_LEFTDOWN);

        await Task.Delay(rand.Next(100, 200)).ConfigureAwait(false);

        await MoveMouse(end, speed).ConfigureAwait(false);

        await Task.Delay(rand.Next(50, 150)).ConfigureAwait(false);

        SendMouseInput(NativeMethods.MOUSEEVENTF_LEFTUP);
    }

    public async Task ShiftClick(Point destination)
    {
        await PrepareInput().ConfigureAwait(false);

        await MoveMouse(destination).ConfigureAwait(false);

        SendKeyboardInput(NativeMethods.VK_SHIFT, true);

        await Task.Delay(rand.Next(25, 50)).ConfigureAwait(false);

        await Click().ConfigureAwait(false);

        await Task.Delay(rand.Next(25, 50)).ConfigureAwait(false);

        SendKeyboardInput(NativeMethods.VK_SHIFT, false);
    }

    public async Task PressKey(ushort vk)
    {
        await PrepareInput().ConfigureAwait(false);

        SendKeyboardInput(vk, true);

        await Task.Delay(rand.Next(50, 100)).ConfigureAwait(false);

        SendKeyboardInput(vk, false);
    }

    private static void SendMouseInput(uint flags, uint data = 0)
    {
        NativeMethods.Input[] inputs = [
            new() {
            Type = NativeMethods.INPUT_MOUSE,
            U = new() {
                Mi = new() {
                    DwFlags = flags,
                    MouseData = data,
                    Time = 0,
                    DwExtraInfo = IntPtr.Zero
                }
            }
        }
        ];
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.Input>());
    }

    private static void SendKeyboardInput(ushort vk, bool keyDown)
    {
        NativeMethods.Input[] inputs = [
            new() {
            Type = NativeMethods.INPUT_KEYBOARD,
            U = new() {
                Ki = new() {
                    WVk = vk,
                    DwFlags = keyDown ? NativeMethods.KEYEVENTF_KEYDOWN : NativeMethods.KEYEVENTF_KEYUP,
                    Time = 0,
                    DwExtraInfo = IntPtr.Zero
                }
            }
        }
        ];
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.Input>());
    }
}
