using Core.Input;
using Core.Native;
using Core.Tests.Fakes;
using Core.Windows;
using System.Drawing;

namespace Core.Tests;

public class WindMouseInputTests
{
    private sealed class FakeFocusTarget : IFocusTarget
    {
        public int CallCount { get; private set; }

        public bool Result { get; set; } = true;

        public Task<bool> EnsureFocused(CancellationToken cancellationToken = default)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result);
        }
    }

    private static (WindMouseInput Input, FakeNativeApi Native, FakeFocusTarget Focus) Build()
    {
        var native = new FakeNativeApi();
        var focus = new FakeFocusTarget();

        var input = new WindMouseInput(
            native,
            focus,
            WindMouseOptions.Default,
            InputTimings.Instant,
            new Random(4242));

        return (input, native, focus);
    }

    [Fact]
    public async Task MoveMouseAsync_FocusesTheWindowFirst()
    {
        (WindMouseInput input, _, FakeFocusTarget focus) = Build();

        await input.MoveMouse(new Point(100, 100));

        Assert.Equal(1, focus.CallCount);
    }

    [Fact]
    public async Task MoveMouseAsync_EmitsAPathOfMoves()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();
        native.CursorPosition = Point.Empty;

        await input.MoveMouse(new Point(500, 400));

        Assert.True(native.MouseMoves.Count > 5, $"Expected a path, got {native.MouseMoves.Count} moves.");
    }

    [Fact]
    public async Task MoveMouseAsync_SendsAbsoluteCoordinatesWithinRange()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.MoveMouse(new Point(1000, 500));

        Assert.All(native.MouseMoves, move =>
        {
            Assert.InRange(move.X, 0, 65535);
            Assert.InRange(move.Y, 0, 65535);
        });
    }

    [Fact]
    public async Task MoveMouseAsync_FinishesOnTheDestination()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();
        var destination = new Point(640, 360);

        await input.MoveMouse(destination);

        Point expected = ScreenCoordinates.ToAbsolute(destination, native.VirtualScreen);

        Assert.Equal(expected, native.MouseMoves[^1]);
    }

    [Fact]
    public async Task MoveMouseAsync_ThrowsWhenAlreadyCanceled()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => input.MoveMouse(new Point(10, 10), cts.Token));

        Assert.Empty(native.MouseMoves);
    }

    [Fact]
    public async Task ClickAsync_SendsDownThenUp()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.Click();

        Assert.Equal([(MouseButton.Left, true), (MouseButton.Left, false)], native.ButtonEvents);
    }

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    [InlineData(MouseButton.Middle)]
    public async Task ClickAsync_HonorsTheButton(MouseButton button)
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.Click(button);

        Assert.All(native.ButtonEvents, evt => Assert.Equal(button, evt.Button));
    }

    [Fact]
    public async Task MoveAndClickAsync_MovesBeforeClicking()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.MoveAndClick(new Point(200, 200));

        Assert.NotEmpty(native.MouseMoves);
        Assert.Equal([(MouseButton.Left, true), (MouseButton.Left, false)], native.ButtonEvents);
    }

    [Fact]
    public async Task DragAsync_PressesMovesThenReleases()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.Drag(new Point(10, 10), new Point(200, 200));

        Assert.Equal([(MouseButton.Left, true), (MouseButton.Left, false)], native.ButtonEvents);
        Assert.NotEmpty(native.MouseMoves);
    }

    [Fact]
    public async Task DragAsync_ReleasesTheButtonEvenWhenCanceledMidDrag()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();
        using var cts = new CancellationTokenSource();

        // Cancel the instant the button goes down, so cancellation lands mid-drag rather
        // than racing the (deliberately instant) test timings.
        native.OnButtonEvent = (_, down) =>
        {
            if (down)
            {
                cts.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => input.Drag(new Point(0, 0), new Point(900, 700), MouseButton.Left, cts.Token));

        // A stuck mouse button would leave the desktop unusable for the user.
        Assert.Contains((MouseButton.Left, false), native.ButtonEvents);
    }

    [Fact]
    public async Task RotateCameraAsync_DragsWithTheMiddleButton()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.RotateCamera(new Point(400, 300), new Point(500, 300));

        Assert.All(native.ButtonEvents, evt => Assert.Equal(MouseButton.Middle, evt.Button));
    }

    [Fact]
    public async Task ScrollAsync_SendsOneEventPerNotchUpwards()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.Scroll(3);

        Assert.Equal([1, 1, 1], native.WheelEvents);
    }

    [Fact]
    public async Task ScrollAsync_SendsNegativeNotchesDownwards()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.Scroll(-2);

        Assert.Equal([-1, -1], native.WheelEvents);
    }

    [Fact]
    public async Task ScrollAsync_DoesNothingForZero()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.Scroll(0);

        Assert.Empty(native.WheelEvents);
    }

    [Fact]
    public async Task PressKeyAsync_SendsDownThenUp()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.PressKey(VirtualKey.Space);

        Assert.Equal([(VirtualKey.Space, true), (VirtualKey.Space, false)], native.KeyEvents);
    }

    [Fact]
    public async Task HoldAndReleaseKey_SendSingleEvents()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.HoldKey(VirtualKey.Shift);
        await input.ReleaseKey(VirtualKey.Shift);

        Assert.Equal([(VirtualKey.Shift, true), (VirtualKey.Shift, false)], native.KeyEvents);
    }

    [Fact]
    public async Task ClickWithModifierAsync_WrapsTheClickInTheModifier()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.ClickWithModifier(new Point(100, 100), VirtualKey.Shift);

        Assert.Equal([(VirtualKey.Shift, true), (VirtualKey.Shift, false)], native.KeyEvents);
        Assert.Equal([(MouseButton.Left, true), (MouseButton.Left, false)], native.ButtonEvents);
    }

    [Fact]
    public async Task ClickWithModifierAsync_ReleasesModifierWhenCanceled()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();
        using var cts = new CancellationTokenSource();

        // Cancel as soon as the modifier is held, so the click that follows is abandoned.
        native.OnKeyEvent = (_, down) =>
        {
            if (down)
            {
                cts.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => input.ClickWithModifier(
                new Point(900, 700), VirtualKey.Shift, MouseButton.Left, cts.Token));

        // A latched modifier would corrupt every subsequent keystroke.
        Assert.Contains((VirtualKey.Shift, false), native.KeyEvents);
    }

    [Fact]
    public async Task TypeAsync_SendsEachCharacter()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.TypeText("yew logs");

        Assert.Equal("yew logs", new string([.. native.TypedCharacters]));
    }

    [Fact]
    public async Task TypeAsync_AcceptsEmptyString()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();

        await input.TypeText("");

        Assert.Empty(native.TypedCharacters);
    }

    [Fact]
    public async Task TypeAsync_RejectsNull()
    {
        (WindMouseInput input, _, _) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => input.TypeText(null!));
    }

    [Fact]
    public async Task TypeAsync_StopsWhenCanceled()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => input.TypeText("abc", cts.Token));

        Assert.Empty(native.TypedCharacters);
    }

    [Fact]
    public async Task MoveMouseAsync_PacesThePathWhenStepDelaysAreConfigured()
    {
        var native = new FakeNativeApi();
        var focus = new FakeFocusTarget();

        // Real (if tiny) pacing, exercising the delay branch that Instant timings skip.
        InputTimings paced = InputTimings.Instant with
        {
            MoveStepDelayMs = 1,
            MoveStepsPerDelay = 2,
        };

        var input = new WindMouseInput(native, focus, WindMouseOptions.Default, paced, new Random(11));

        await input.MoveMouse(new Point(120, 90));

        Assert.NotEmpty(native.MouseMoves);
    }

    [Fact]
    public async Task MoveMouseAsync_SkipsPacingWhenStepsPerDelayIsZero()
    {
        var native = new FakeNativeApi();
        var focus = new FakeFocusTarget();

        // A zero interval would otherwise be a modulo-by-zero on every step.
        InputTimings unpaced = InputTimings.Instant with
        {
            MoveStepDelayMs = 5,
            MoveStepsPerDelay = 0,
        };

        var input = new WindMouseInput(native, focus, WindMouseOptions.Default, unpaced, new Random(11));

        await input.MoveMouse(new Point(120, 90));

        Assert.NotEmpty(native.MouseMoves);
    }

    [Fact]
    public async Task MoveMouseAsync_StopsPartwayWhenCanceledDuringThePath()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();
        using var cts = new CancellationTokenSource();

        int moves = 0;
        native.OnMouseMove = _ =>
        {
            if (++moves == 3)
            {
                cts.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => input.MoveMouse(new Point(1500, 1000), cts.Token));

        // Cancellation must halt the path rather than running it to completion.
        Assert.Equal(3, native.MouseMoves.Count);
    }

    [Fact]
    public async Task ScrollAsync_StopsWhenCanceledPartway()
    {
        (WindMouseInput input, FakeNativeApi native, _) = Build();
        using var cts = new CancellationTokenSource();

        await input.Scroll(2);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => input.Scroll(50, cts.Token));

        Assert.Equal(2, native.WheelEvents.Count);
    }

    [Fact]
    public async Task Constructor_DefaultsAreUsedWhenNoOptionsSupplied()
    {
        var native = new FakeNativeApi();
        var focus = new FakeFocusTarget();

        // Null options must fall back to the defaults rather than throwing.
        var input = new WindMouseInput(native, focus);

        await input.Click();

        Assert.Equal(2, native.ButtonEvents.Count);
    }

    [Fact]
    public async Task Constructor_HonorsSuppliedMovementOptions()
    {
        var coarseNative = new FakeNativeApi();
        var fineNative = new FakeNativeApi();

        var coarse = new WindMouseInput(
            coarseNative, new FakeFocusTarget(),
            WindMouseOptions.Default with { MaxStep = 60 }, InputTimings.Instant, new Random(5));

        var fine = new WindMouseInput(
            fineNative, new FakeFocusTarget(),
            WindMouseOptions.Default with { MaxStep = 5 }, InputTimings.Instant, new Random(5));

        await coarse.MoveMouse(new Point(800, 600));
        await fine.MoveMouse(new Point(800, 600));

        // Same seed and destination, so ignoring the supplied options would make these
        // two runs identical. (Step count is not compared: a larger MaxStep overshoots
        // the target area and oscillates, so it is not monotonic in either direction.)
        Assert.NotEqual(coarseNative.MouseMoves, fineNative.MouseMoves);
    }

    [Fact]
    public async Task Constructor_HonorsSuppliedRandom()
    {
        var first = new FakeNativeApi();
        var second = new FakeNativeApi();

        var a = new WindMouseInput(
            first, new FakeFocusTarget(), WindMouseOptions.Default, InputTimings.Instant, new Random(777));
        var b = new WindMouseInput(
            second, new FakeFocusTarget(), WindMouseOptions.Default, InputTimings.Instant, new Random(777));

        await a.MoveMouse(new Point(400, 300));
        await b.MoveMouse(new Point(400, 300));

        // Falling back to Random.Shared would desynchronize these two runs.
        Assert.Equal(first.MouseMoves, second.MouseMoves);
    }

    [Fact]
    public async Task Constructor_HonorsSuppliedTimings()
    {
        var native = new FakeNativeApi();

        // Distinctively slower than the default click hold of 60-120ms, so falling back
        // to the defaults would finish measurably sooner.
        InputTimings slow = InputTimings.Instant with { ClickHoldMs = (250, 251) };

        var input = new WindMouseInput(
            native, new FakeFocusTarget(), WindMouseOptions.Default, slow, new Random(1));

        long start = Environment.TickCount64;
        await input.Click();
        long elapsed = Environment.TickCount64 - start;

        Assert.True(elapsed >= 200, $"Click held for only {elapsed}ms; supplied timings were ignored.");
    }

    [Fact]
    public async Task Operations_FocusTheWindowBeforeActing()
    {
        (WindMouseInput input, _, FakeFocusTarget focus) = Build();

        await input.Click();
        await input.Scroll(1);
        await input.PressKey(VirtualKey.F1);

        Assert.Equal(3, focus.CallCount);
    }
}
