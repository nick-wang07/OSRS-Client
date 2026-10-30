using Core.Input;
using Core.Native;
using System.Drawing;

namespace Core.Tests.Fakes;

/// <summary>Records requested actions without touching the desktop.</summary>
public sealed class FakeInputProvider : IInputProvider
{
    public List<string> Actions { get; } = [];

    public Task MoveMouse(Point destination, CancellationToken cancellationToken = default) =>
        Record($"move:{destination.X},{destination.Y}", cancellationToken);

    public Task Click(MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default) =>
        Record($"click:{button}", cancellationToken);

    public Task MoveAndClick(
        Point destination,
        MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default) =>
        Record($"moveclick:{destination.X},{destination.Y}:{button}", cancellationToken);

    public Task Drag(
        Point start,
        Point end,
        MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default) =>
        Record($"drag:{start.X},{start.Y}->{end.X},{end.Y}:{button}", cancellationToken);

    public Task RotateCamera(Point start, Point end, CancellationToken cancellationToken = default) =>
        Record($"camera:{start.X},{start.Y}->{end.X},{end.Y}", cancellationToken);

    public Task Scroll(int notches, CancellationToken cancellationToken = default) =>
        Record($"scroll:{notches}", cancellationToken);

    public Task PressKey(VirtualKey key, CancellationToken cancellationToken = default) =>
        Record($"press:{key}", cancellationToken);

    public Task HoldKey(VirtualKey key, CancellationToken cancellationToken = default) =>
        Record($"hold:{key}", cancellationToken);

    public Task ReleaseKey(VirtualKey key, CancellationToken cancellationToken = default) =>
        Record($"release:{key}", cancellationToken);

    public Task ClickWithModifier(
        Point destination,
        VirtualKey modifier,
        MouseButton button = MouseButton.Left,
        CancellationToken cancellationToken = default) =>
        Record($"modclick:{destination.X},{destination.Y}:{modifier}:{button}", cancellationToken);

    public Task TypeText(string text, CancellationToken cancellationToken = default) =>
        Record($"type:{text}", cancellationToken);

    private Task Record(string action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Actions.Add(action);
        return Task.CompletedTask;
    }
}
