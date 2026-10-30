using Core.Tests.Fakes;
using Core.Windows;
using System.Drawing;

namespace Core.Tests;

public class GameWindowProviderTests
{
    private const nint Handle = 0x1234;

    private static readonly Rectangle Bounds = new(100, 50, 800, 600);

    private static (GameWindowProvider Provider, FakeNativeApi Native, FakeWindowFinder Finder) Build(
        bool registerWindow = true)
    {
        var native = new FakeNativeApi();
        var finder = new FakeWindowFinder();

        if (registerWindow)
        {
            native.AddWindow(Handle, Bounds);
            finder.Register("RuneLite", Handle);
        }

        var provider = new GameWindowProvider(native, finder)
        {
            FocusTimeout = TimeSpan.FromMilliseconds(200),
            FocusPollInterval = TimeSpan.FromMilliseconds(1),
        };

        return (provider, native, finder);
    }

    [Fact]
    public void Attach_StoresHandleWhenWindowExists()
    {
        (GameWindowProvider provider, _, FakeWindowFinder finder) = Build();

        Assert.True(provider.Attach());
        Assert.Equal(Handle, provider.Handle);
        Assert.True(provider.IsAttached);
        Assert.Equal("RuneLite", Assert.Single(finder.Queries));
    }

    [Fact]
    public void Attach_UsesTheSuppliedProcessName()
    {
        (GameWindowProvider provider, FakeNativeApi native, FakeWindowFinder finder) = Build(false);
        native.AddWindow(Handle, Bounds);
        finder.Register("osclient", Handle);

        Assert.True(provider.Attach("osclient"));
        Assert.Equal("osclient", Assert.Single(finder.Queries));
    }

    [Fact]
    public void Attach_ReturnsFalseWhenNoWindowFound()
    {
        (GameWindowProvider provider, _, _) = Build(registerWindow: false);

        Assert.False(provider.Attach());
        Assert.Equal(0, provider.Handle);
        Assert.False(provider.IsAttached);
    }

    [Fact]
    public void Attach_RejectsHandleThatIsNotALiveWindow()
    {
        (GameWindowProvider provider, FakeNativeApi native, FakeWindowFinder finder) = Build(false);

        // Finder reports a handle, but the window does not actually exist.
        finder.Register("RuneLite", Handle);
        native.LiveWindows.Remove(Handle);

        Assert.False(provider.Attach());
        Assert.Equal(0, provider.Handle);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Attach_RejectsBlankProcessName(string? processName)
    {
        (GameWindowProvider provider, _, _) = Build();

        Assert.ThrowsAny<ArgumentException>(() => provider.Attach(processName!));
    }

    [Fact]
    public void IsAttached_BecomesFalseWhenTheWindowCloses()
    {
        (GameWindowProvider provider, FakeNativeApi native, _) = Build();
        provider.Attach();

        Assert.True(provider.IsAttached);

        // The client exits: the cached handle must not be reported as usable.
        native.LiveWindows.Remove(Handle);

        Assert.False(provider.IsAttached);
    }

    [Fact]
    public void Detach_ClearsTheHandle()
    {
        (GameWindowProvider provider, _, _) = Build();
        provider.Attach();

        provider.Detach();

        Assert.Equal(0, provider.Handle);
        Assert.False(provider.IsAttached);
    }

    [Fact]
    public void TryGetWindowBounds_ReturnsClientAreaInScreenCoordinates()
    {
        (GameWindowProvider provider, _, _) = Build();
        provider.Attach();

        Assert.True(provider.TryGetWindowBounds(out Rectangle bounds));
        Assert.Equal(Bounds, bounds);
    }

    [Fact]
    public void TryGetWindowBounds_ReturnsFalseWhenNotAttached()
    {
        (GameWindowProvider provider, _, _) = Build();

        Assert.False(provider.TryGetWindowBounds(out Rectangle bounds));
        Assert.Equal(Rectangle.Empty, bounds);
    }

    [Fact]
    public void TryGetWindowBounds_ReturnsFalseWhenTheWindowHasNoBounds()
    {
        (GameWindowProvider provider, FakeNativeApi native, _) = Build();
        provider.Attach();
        native.ClientBounds.Remove(Handle);

        Assert.False(provider.TryGetWindowBounds(out _));
    }

    [Fact]
    public void ToScreen_OffsetsByWindowOrigin() =>
        Assert.Equal(
            new Point(150, 90),
            GameWindowProvider.ToScreen(new Point(50, 40), new Rectangle(100, 50, 800, 600)));

    [Fact]
    public void ToScreen_HandlesNegativeWindowOrigin() =>
        Assert.Equal(
            new Point(-70, -10),
            GameWindowProvider.ToScreen(new Point(30, 40), new Rectangle(-100, -50, 800, 600)));

    [Fact]
    public void TryToScreen_ConvertsWhenAttached()
    {
        (GameWindowProvider provider, _, _) = Build();
        provider.Attach();

        Assert.True(provider.TryToScreen(new Point(10, 20), out Point screen));
        Assert.Equal(new Point(110, 70), screen);
    }

    [Fact]
    public void TryToScreen_ReturnsFalseWhenNotAttached()
    {
        (GameWindowProvider provider, _, _) = Build();

        Assert.False(provider.TryToScreen(new Point(10, 20), out Point screen));
        Assert.Equal(Point.Empty, screen);
    }

    [Fact]
    public async Task EnsureFocusedAsync_ReturnsFalseWhenNotAttached()
    {
        (GameWindowProvider provider, _, _) = Build();

        Assert.False(await provider.EnsureFocused());
    }

    [Fact]
    public async Task EnsureFocusedAsync_ShortCircuitsWhenAlreadyForeground()
    {
        (GameWindowProvider provider, FakeNativeApi native, _) = Build();
        provider.Attach();
        native.ForegroundWindow = Handle;

        Assert.True(await provider.EnsureFocused());
        Assert.Equal(0, native.SetForegroundCallCount);
    }

    [Fact]
    public async Task EnsureFocusedAsync_BringsWindowToForeground()
    {
        (GameWindowProvider provider, FakeNativeApi native, _) = Build();
        provider.Attach();
        native.ForegroundWindow = 0x999;

        Assert.True(await provider.EnsureFocused());
        Assert.Equal(1, native.SetForegroundCallCount);
        Assert.Equal(Handle, native.ForegroundWindow);
    }

    [Fact]
    public async Task EnsureFocusedAsync_RestoresMinimizedWindow()
    {
        (GameWindowProvider provider, FakeNativeApi native, _) = Build();
        provider.Attach();
        native.ForegroundWindow = 0x999;
        native.MinimizedWindows.Add(Handle);

        Assert.True(await provider.EnsureFocused());

        // SW_SHOW alone leaves a minimized window minimized, so an explicit restore is required.
        Assert.Equal(1, native.RestoreCallCount);
    }

    [Fact]
    public async Task EnsureFocusedAsync_DoesNotRestoreWindowThatIsNotMinimized()
    {
        (GameWindowProvider provider, FakeNativeApi native, _) = Build();
        provider.Attach();
        native.ForegroundWindow = 0x999;

        await provider.EnsureFocused();

        Assert.Equal(0, native.RestoreCallCount);
    }

    [Fact]
    public async Task EnsureFocusedAsync_PollsUntilForegroundIsGranted()
    {
        (GameWindowProvider provider, FakeNativeApi native, _) = Build();
        provider.Attach();
        native.ForegroundWindow = 0x999;
        native.ForegroundDelayPolls = 3;

        Assert.True(await provider.EnsureFocused());
    }

    [Fact]
    public async Task EnsureFocusedAsync_ReturnsFalseWhenWindowsRefusesForeground()
    {
        (GameWindowProvider provider, FakeNativeApi native, _) = Build();
        provider.Attach();
        native.ForegroundWindow = 0x999;
        native.RefuseForeground = true;

        // Windows restricts foreground stealing; the caller must learn it failed rather
        // than proceeding to click into whatever window actually has focus.
        Assert.False(await provider.EnsureFocused());
    }

    [Fact]
    public async Task EnsureFocusedAsync_ObservesCancellation()
    {
        (GameWindowProvider provider, FakeNativeApi native, _) = Build();
        provider.Attach();
        native.ForegroundWindow = 0x999;
        native.RefuseForeground = true;

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.EnsureFocused(cts.Token));
    }
}
