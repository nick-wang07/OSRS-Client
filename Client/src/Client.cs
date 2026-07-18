using Core.src.Input;
using Core.src.Windows;
using System.Drawing;

namespace Client.src;

public static class Client
{
    public static async Task Main()
    {
        Console.Title = "My Console App";

        await Run().ConfigureAwait(false);

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static async Task Run()
    {
        Initialize();

        bool isRunning = true;

        while (isRunning)
        {
            ShowMenu();

            string? input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    await TestMouseMovement().ConfigureAwait(false);
                    break;
                case "2":
                    await RunWindowTrackerTest().ConfigureAwait(false);
                    break;
                case "3":
                    Console.Write("Enter X: ");
                    int tx = int.Parse(Console.ReadLine() ?? "0");
                    Console.Write("Enter Y: ");
                    int ty = int.Parse(Console.ReadLine() ?? "0");
                    await TestPixelDetection(tx, ty).ConfigureAwait(false);
                    break;
                case "4":
                    Console.Write("Enter X: ");
                    int tx2 = int.Parse(Console.ReadLine() ?? "0");
                    Console.Write("Enter Y: ");
                    int ty2 = int.Parse(Console.ReadLine() ?? "0");
                    await TestColorDetection(tx2, ty2).ConfigureAwait(false);
                    break;
                case "0":
                    isRunning = false;
                    break;
                default:
                    Console.WriteLine("Invalid selection.");
                    break;
            }
        }
        Cleanup();
    }

    private static void Initialize() => Console.WriteLine("Application starting...");

    private static void Cleanup() => Console.WriteLine("Cleaning up...");

    private static void ShowMenu()
    {
        Console.WriteLine("\n=== Main Menu ===");
        Console.WriteLine("1 - Move Mouse and Click Test");
        Console.WriteLine("2 - Window Tracker Tests");
        Console.WriteLine("3 - Pixel Detection Test");
        Console.WriteLine("4 - Color Detection Test");
        Console.WriteLine("0 - Exit");
        Console.Write("Select an option: ");
    }

    public static async Task RunWindowTrackerTest()
    {
        var windowProvider = new GameWindowProvider();

        Console.WriteLine("Searching for RuneLite...");

        windowProvider.Attach("RuneLite");

        if (!windowProvider.IsAttached)
        {
            Console.WriteLine("RuneLite not found! Please open the game.");
            return;
        }

        Console.WriteLine("Tracking started. Press Ctrl+C to stop.");

        while (true)
        {
            Rectangle rect = windowProvider.GetCanvasRect();
            Console.Clear();
            Console.WriteLine($"[RuneLite Found]");
            Console.WriteLine($"Canvas X: {rect.X}");
            Console.WriteLine($"Canvas Y: {rect.Y}");
            Console.WriteLine($"Canvas Size: {rect.Width}x{rect.Height}");

            Point globalOrigin = windowProvider.ToGlobal(new Point(0, 0));
            Console.WriteLine($"Global Origin (0,0): {globalOrigin.X}, {globalOrigin.Y}");

            await Task.Delay(500);
        }
    }

    private static async Task TestMouseMovement()
    {
        var windowProvider = new GameWindowProvider();
        var mouse = new WindMouseInput(windowProvider);

        Console.WriteLine("Searching for RuneLite...");

        windowProvider.Attach("RuneLite");

        if (!windowProvider.IsAttached)
        {
            Console.WriteLine("Error: RuneLite is not open.");
            return;
        }

        await windowProvider.EnsureFocused().ConfigureAwait(false);

        var canvas = windowProvider.GetCanvasRect();
        Point relativeCenter = new(canvas.Width / 2, canvas.Height / 2);
        var globalTarget = windowProvider.ToGlobal(relativeCenter);

        Console.WriteLine($"Moving to center: {globalTarget.X}, {globalTarget.Y}");

        await mouse.MoveAndClick(globalTarget).ConfigureAwait(false);

        Console.WriteLine("Movement complete.");
    }

    private static async Task TestPixelDetection(int x, int y)
    {
        var windowProvider = new GameWindowProvider();
        windowProvider.Attach("RuneLite");

        Console.WriteLine($"--- Pixel Diagnostic at ({x}, {y}) ---");

        using var eye = windowProvider.TakeSnapshot();

        Color pixelColor = eye.GetPixel(x, y);

        Console.WriteLine($"Hex: #{pixelColor.R:X2}{pixelColor.G:X2}{pixelColor.B:X2}");
        Console.WriteLine($"RGB: {pixelColor.R}, {pixelColor.G}, {pixelColor.B}");

        if (pixelColor.R > 60 && pixelColor.R < 70 && pixelColor.G < 60)
            Console.WriteLine("Status: Likely an empty inventory slot.");
        else
            Console.WriteLine("Status: Other color detected.");
    }

    private static async Task TestColorDetection(int x, int y)
    {
        var windowProvider = new GameWindowProvider();
        windowProvider.Attach("RuneLite");

        Console.WriteLine($"--- Color Diagnostic at ({x}, {y}) ---");

        using var eye = windowProvider.TakeSnapshot();

        Color prayerOrbGlow = Color.FromArgb(222, 184, 9);

        var result = eye.AnyPixelInRange(new Rectangle(x - 5, y - 5, 10, 10), prayerOrbGlow, 10);

        Console.WriteLine($"Prayer Orb Glow Detected: {result}");
    }
}
