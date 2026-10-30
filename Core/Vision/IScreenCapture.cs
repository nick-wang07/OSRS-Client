using System.Drawing;

namespace Core.Vision;

/// <summary>Acquires frames from a window.</summary>
public interface IScreenCapture : IDisposable
{
    /// <summary>
    /// Captures a region expressed in window-client coordinates.
    /// </summary>
    /// <param name="hWnd">Window to read from.</param>
    /// <param name="region">
    /// Area to capture, relative to the window's client origin. Unlike the previous
    /// implementation the origin is honored, so sub-region captures return the
    /// requested pixels rather than the top-left of the window.
    /// </param>
    Frame Capture(nint hWnd, Rectangle region);
}
