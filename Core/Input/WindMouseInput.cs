using System.Drawing;
using Core.Native;
using Core.Windows;

namespace Core.Input;

/// <summary>
/// Input driven through SendInput, with cursor motion shaped by <see cref="WindMousePath"/>.
/// </summary>
/// <remarks>
/// Not safe for concurrent use: two callers driving the mouse at once would interleave
/// events into meaningless gestures. Serialize access, which the script runner does by
/// only running one script at a time.
/// </remarks>
public sealed class WindMouseInput(
    INativeApi native,
    IFocusTarget focusTarget,
    WindMouseOptions? movement = null,
    InputTimings? timings = null,
    Random? random = null) : IInputProvider
{
    private readonly WindMouseOptions _movement = movement ?? WindMouseOptions.Default;
    private readonly InputTimings _timings = timings ?? InputTimings.Default;
    private readonly Random _random = random ?? Random.Shared;

    public async Task MoveMouse(Point destination, CancellationToken cancellationToken = default)
    {
        await Prepare(cancellationToken);

        var virtualScreen = native.GetVirtualScreenBounds();
        var start = native.GetCursorPosition();

        var path = WindMousePath.Generate(start, destination, _movement, _random);

        for (int i = 0; i < path.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var absolute = ScreenCoordinates.ToAbsolute(path[i], virtualScreen);
            native.SendMouseMoveAbsolute(absolute.X, absolute.Y);

            if (_timings.MoveStepDelayMs > 0
                && _timings.MoveStepsPerDelay > 0
                && (i + 1) % _timings.MoveStepsPerDelay == 0)
            {
                await Task.Delay(_timings.MoveStepDelayMs, cancellationToken);
            }
        }
    }

    public async Task Click(
        MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default)
    {
        await Prepare(cancellationToken);

        native.SendMouseButton(button, down: true);

        await Delay(_timings.ClickHoldMs, cancellationToken);

        native.SendMouseButton(button, down: false);
    }

    public async Task MoveAndClick(
        Point destination,
        MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default)
    {
        await MoveMouse(destination, cancellationToken);
        await Delay(_timings.BeforeClickMs, cancellationToken);
        await Click(button, cancellationToken);
    }

    public async Task Drag(
        Point start,
        Point end,
        MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default)
    {
        await MoveMouse(start, cancellationToken);

        native.SendMouseButton(button, down: true);

        try
        {
            await Delay(_timings.DragSettleMs, cancellationToken);
            await MoveMouse(end, cancellationToken);
            await Delay(_timings.DragSettleMs, cancellationToken);
        }
        finally
        {
            // Release even when canceled, otherwise the button stays stuck down.
            native.SendMouseButton(button, down: false);
        }
    }

    public Task RotateCamera(Point start, Point end, CancellationToken cancellationToken = default) =>
        Drag(start, end, MouseButton.Middle, cancellationToken);

    public async Task Scroll(int notches, CancellationToken cancellationToken = default)
    {
        await Prepare(cancellationToken);

        int direction = Math.Sign(notches);
        int count = Math.Abs(notches);

        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            native.SendMouseWheel(direction);

            await Delay(_timings.ScrollNotchMs, cancellationToken);
        }
    }

    public async Task PressKey(VirtualKey key, CancellationToken cancellationToken = default)
    {
        await Prepare(cancellationToken);

        native.SendKey(key, down: true);

        try
        {
            await Delay(_timings.KeyPressMs, cancellationToken);
        }
        finally
        {
            native.SendKey(key, down: false);
        }
    }

    public async Task HoldKey(VirtualKey key, CancellationToken cancellationToken = default)
    {
        await Prepare(cancellationToken);

        native.SendKey(key, down: true);
    }

    public async Task ReleaseKey(VirtualKey key, CancellationToken cancellationToken = default)
    {
        await Prepare(cancellationToken);

        native.SendKey(key, down: false);
    }

    public async Task ClickWithModifier(
        Point destination,
        VirtualKey modifier,
        MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default)
    {
        await MoveMouse(destination, cancellationToken);

        native.SendKey(modifier, down: true);

        try
        {
            await Delay(_timings.ModifierMs, cancellationToken);
            await Click(button, cancellationToken);
            await Delay(_timings.ModifierMs, cancellationToken);
        }
        finally
        {
            // Leaving a modifier latched would corrupt every later keystroke.
            native.SendKey(modifier, down: false);
        }
    }

    public async Task TypeText(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        await Prepare(cancellationToken);

        foreach (char character in text)
        {
            cancellationToken.ThrowIfCancellationRequested();

            native.SendUnicodeCharacter(character);

            await Delay(_timings.KeystrokeMs, cancellationToken);
        }
    }

    private async Task Prepare(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _ = await focusTarget.EnsureFocused(cancellationToken);
    }

    private Task Delay((int Min, int Max) range, CancellationToken cancellationToken)
    {
        int milliseconds = InputTimings.PickDelay(range, _random);

        return milliseconds <= 0 ? Task.CompletedTask : Task.Delay(milliseconds, cancellationToken);
    }
}
