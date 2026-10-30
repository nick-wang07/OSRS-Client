using System.Drawing;
using Core.Native;

namespace Core.Input;

/// <summary>
/// Drives mouse and keyboard input. All coordinates are screen coordinates.
/// </summary>
/// <remarks>
/// Movement tuning is deliberately absent: it belongs to a specific movement model
/// (see <see cref="WindMouseOptions"/>) and would otherwise force every implementation
/// to accept parameters it cannot honor.
/// </remarks>
public interface IInputProvider
{
    Task MoveMouse(Point destination, CancellationToken cancellationToken = default);

    Task Click(MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default);

    Task MoveAndClick(
        Point destination,
        MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default);

    Task Drag(
        Point start,
        Point end,
        MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default);

    Task RotateCamera(Point start, Point end, CancellationToken cancellationToken = default);

    Task Scroll(int notches, CancellationToken cancellationToken = default);

    Task PressKey(VirtualKey key, CancellationToken cancellationToken = default);

    Task HoldKey(VirtualKey key, CancellationToken cancellationToken = default);

    Task ReleaseKey(VirtualKey key, CancellationToken cancellationToken = default);

    Task ClickWithModifier(
        Point destination,
        VirtualKey modifier,
        MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default);

    Task TypeText(string text, CancellationToken cancellationToken = default);
}
