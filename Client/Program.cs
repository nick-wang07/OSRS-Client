using System.Drawing;
using Client.Logging;
using Core.Input;
using Core.Native;
using Core.Safety;
using Core.Scripting;
using Core.Vision;
using Core.Windows;

namespace Client;

public static class Program
{
    private const string ProcessName = "RuneLite";

    public static async Task Main()
    {
        Console.Title = "OSRS Client";

        var native = new Win32NativeApi();
        var window = new GameWindowProvider(native, new ProcessWindowFinder());

        using var killSwitch = new KillSwitch();
        using var hotkey = new HotkeyKillSwitchMonitor(native, killSwitch);
        using var capture = new GdiScreenCapture();

        var input = new WindMouseInput(native, window);
        var logger = new ConsoleScriptLogger();
        using var runner = new ScriptRunner(killSwitch, logger);

        killSwitch.Triggered += (_, reason) =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[KILL SWITCH] {reason}. All scripts canceled.");
            Console.ResetColor();
        };

        hotkey.Start();

        Console.WriteLine($"Kill switch armed: press {hotkey.Hotkey} at any time to stop everything.\n");

        await RunMenu(window, input, capture, runner, killSwitch);

        await runner.Stop();

        Console.WriteLine("Exiting.");
    }

    private static async Task RunMenu(
        GameWindowProvider window,
        IInputProvider input,
        IScreenCapture capture,
        ScriptRunner runner,
        KillSwitch killSwitch)
    {
        while (true)
        {
            ShowMenu(window, runner, killSwitch);

            switch (Console.ReadLine())
            {
                case "1":
                    Attach(window);
                    break;
                case "2":
                    await ShowWindowInfo(window);
                    break;
                case "3":
                    await MoveToCenter(window, input, killSwitch);
                    break;
                case "4":
                    InspectPixel(window, capture);
                    break;
                case "5":
                    await runner.Stop();
                    Console.WriteLine("Stopped.");
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Unrecognized option.");
                    break;
            }
        }
    }

    private static void ShowMenu(GameWindowProvider window, ScriptRunner runner, KillSwitch killSwitch)
    {
        Console.WriteLine();
        Console.WriteLine("=== OSRS Client ===");
        Console.WriteLine($"  Window: {(window.IsAttached ? $"attached (0x{window.Handle:X})" : "not attached")}");
        Console.WriteLine($"  Script: {runner.State}{(runner.Current is null ? "" : $" ({runner.Current.Name})")}");

        if (killSwitch.IsTriggered)
        {
            Console.WriteLine($"  KILL SWITCH TRIGGERED: {killSwitch.Reason}");
        }

        Console.WriteLine();
        Console.WriteLine("  1 - Attach to RuneLite");
        Console.WriteLine("  2 - Show window bounds");
        Console.WriteLine("  3 - Move mouse to window center");
        Console.WriteLine("  4 - Inspect a pixel");
        Console.WriteLine("  5 - Stop running script");
        Console.WriteLine("  0 - Exit");
        Console.Write("Select: ");
    }

    private static void Attach(GameWindowProvider window)
    {
        Console.WriteLine(window.Attach(ProcessName)
            ? $"Attached to {ProcessName} (handle 0x{window.Handle:X})."
            : $"Could not find a {ProcessName} window. Is the game running?");
    }

    private static async Task ShowWindowInfo(GameWindowProvider window)
    {
        if (!window.TryGetWindowBounds(out var bounds))
        {
            Console.WriteLine("Not attached, or the window has closed.");
            return;
        }

        Console.WriteLine($"Client area: {bounds.Width}x{bounds.Height} at ({bounds.X}, {bounds.Y})");
        Console.WriteLine("Note: this is the whole RuneLite client area, including its sidebar,");
        Console.WriteLine("      not the game canvas. Canvas detection arrives with the Game layer.");

        bool focused = await window.EnsureFocused();

        Console.WriteLine($"Focus acquired: {focused}");
    }

    private static async Task MoveToCenter(
        GameWindowProvider window,
        IInputProvider input,
        KillSwitch killSwitch)
    {
        if (!window.TryGetWindowBounds(out var bounds))
        {
            Console.WriteLine("Not attached.");
            return;
        }

        var center = GameWindowProvider.ToScreen(new Point(bounds.Width / 2, bounds.Height / 2), bounds);

        Console.WriteLine($"Moving to ({center.X}, {center.Y})...");

        try
        {
            await input.MoveAndClick(center, cancellationToken: killSwitch.Token);

            Console.WriteLine("Done.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Canceled.");
        }
    }

    private static void InspectPixel(GameWindowProvider window, IScreenCapture capture)
    {
        if (!window.TryGetWindowBounds(out var bounds))
        {
            Console.WriteLine("Not attached.");
            return;
        }

        Console.Write("X (window-relative): ");
        if (!int.TryParse(Console.ReadLine(), out int x))
        {
            Console.WriteLine("Not a number.");
            return;
        }

        Console.Write("Y (window-relative): ");
        if (!int.TryParse(Console.ReadLine(), out int y))
        {
            Console.WriteLine("Not a number.");
            return;
        }

        var frame = capture.Capture(window.Handle, new Rectangle(0, 0, bounds.Width, bounds.Height));

        if (!frame.TryGetPixel(x, y, out var color))
        {
            Console.WriteLine($"({x}, {y}) is outside the {frame.Width}x{frame.Height} frame.");
            return;
        }

        Console.WriteLine($"RGB: {color.R}, {color.G}, {color.B}   Hex: #{color.R:X2}{color.G:X2}{color.B:X2}");
    }
}
