using Core.Scripting;
using Core.Tests.Fakes;
using Core.Vision;
using Core.Windows;
using System.Drawing;

namespace Core.Tests;

public class ScriptContextTests
{
    private const nint Handle = 0x2222;

    private static readonly Rectangle Bounds = new(300, 200, 640, 480);

    private static (ScriptContext Context, FakeNativeApi Native, FakeScreenCapture Capture, GameWindowProvider Window)
        Build(bool attach = true)
    {
        var native = new FakeNativeApi();
        var finder = new FakeWindowFinder();
        native.AddWindow(Handle, Bounds);
        finder.Register("RuneLite", Handle);

        var window = new GameWindowProvider(native, finder);

        if (attach)
        {
            window.Attach();
        }

        var capture = new FakeScreenCapture();

        var context = new ScriptContext(window, new FakeInputProvider(), capture, new RecordingLogger());

        return (context, native, capture, window);
    }

    [Fact]
    public void Constructor_RejectsNullWindow() =>
        Assert.Throws<ArgumentNullException>(
            () => new ScriptContext(null!, new FakeInputProvider(), new FakeScreenCapture(), new RecordingLogger()));

    [Fact]
    public void Constructor_RejectsNullInput()
    {
        var window = new GameWindowProvider(new FakeNativeApi(), new FakeWindowFinder());

        Assert.Throws<ArgumentNullException>(
            () => new ScriptContext(window, null!, new FakeScreenCapture(), new RecordingLogger()));
    }

    [Fact]
    public void Constructor_RejectsNullCapture()
    {
        var window = new GameWindowProvider(new FakeNativeApi(), new FakeWindowFinder());

        Assert.Throws<ArgumentNullException>(
            () => new ScriptContext(window, new FakeInputProvider(), null!, new RecordingLogger()));
    }

    [Fact]
    public void Constructor_RejectsNullLogger()
    {
        var window = new GameWindowProvider(new FakeNativeApi(), new FakeWindowFinder());

        Assert.Throws<ArgumentNullException>(
            () => new ScriptContext(window, new FakeInputProvider(), new FakeScreenCapture(), null!));
    }

    [Fact]
    public void Constructor_ExposesCollaborators()
    {
        (ScriptContext context, _, FakeScreenCapture capture, GameWindowProvider window) = Build();

        Assert.Same(window, context.Window);
        Assert.Same(capture, context.Capture);
        Assert.NotNull(context.Input);
        Assert.NotNull(context.Logger);
    }

    [Fact]
    public void CaptureWindow_ThrowsWhenNotAttached()
    {
        (ScriptContext context, _, _, _) = Build(attach: false);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(context.CaptureWindow);

        Assert.Contains("not attached", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CaptureWindow_RequestsTheFullClientAreaFromTheWindowOrigin()
    {
        (ScriptContext context, _, FakeScreenCapture capture, _) = Build();

        context.CaptureWindow();

        (nint handle, Rectangle region) = Assert.Single(capture.Captures);

        Assert.Equal(Handle, handle);

        // Capture coordinates are client-relative, so the origin must be zero even
        // though the window sits at (300, 200) on screen.
        Assert.Equal(new Rectangle(0, 0, Bounds.Width, Bounds.Height), region);
    }

    [Fact]
    public void CaptureWindow_ReturnsTheCapturedFrame()
    {
        (ScriptContext context, _, FakeScreenCapture capture, _) = Build();
        Frame expected = new FrameBuilder(8, 8).Fill(Color.Magenta).Build();
        capture.Next = expected;

        Assert.Same(expected, context.CaptureWindow());
    }
}
