using Core.Vision;
using System.Drawing;

namespace Core.Tests.Fakes;

public sealed class FakeScreenCapture : IScreenCapture
{
    public List<(nint Handle, Rectangle Region)> Captures { get; } = [];

    /// <summary>Frame handed back by <see cref="Capture"/>. Defaults to a small blank frame.</summary>
    public Frame? Next { get; set; }

    public bool IsDisposed { get; private set; }

    public Frame Capture(nint hWnd, Rectangle region)
    {
        Captures.Add((hWnd, region));

        return Next ?? new FrameBuilder(Math.Max(region.Width, 1), Math.Max(region.Height, 1)).Build();
    }

    public void Dispose() => IsDisposed = true;
}
