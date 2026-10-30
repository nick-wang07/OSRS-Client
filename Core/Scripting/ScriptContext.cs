using Core.Input;
using Core.Vision;
using Core.Windows;

namespace Core.Scripting;

/// <summary>
/// Everything a script needs to observe and act on the game, bundled so scripts take a
/// single parameter and can be exercised against fakes.
/// </summary>
public sealed class ScriptContext(
    GameWindowProvider window,
    IInputProvider input,
    IScreenCapture capture,
    IScriptLogger logger)
{
    public GameWindowProvider Window { get; } = window ?? throw new ArgumentNullException(nameof(window));

    public IInputProvider Input { get; } = input ?? throw new ArgumentNullException(nameof(input));

    public IScreenCapture Capture { get; } = capture ?? throw new ArgumentNullException(nameof(capture));

    public IScriptLogger Logger { get; } = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Captures the full RuneLite client area.</summary>
    public Frame CaptureWindow()
    {
        if (!Window.TryGetWindowBounds(out var bounds))
        {
            throw new InvalidOperationException("Cannot capture: the game window is not attached.");
        }

        // Capture coordinates are client-relative, so the origin is zero regardless of
        // where the window sits on screen.
        return Capture.Capture(Window.Handle, new System.Drawing.Rectangle(0, 0, bounds.Width, bounds.Height));
    }
}
