using System.Drawing;

namespace Core.Native;

/// <summary>Conversions between screen pixels and the absolute coordinate grid SendInput expects.</summary>
public static class ScreenCoordinates
{
    /// <summary>
    /// Maps a screen pixel onto the 0..65535 grid that <c>MOUSEEVENTF_ABSOLUTE</c> uses.
    /// The grid spans the entire virtual desktop, so the virtual screen origin is
    /// subtracted first — otherwise multi-monitor setups with a monitor left of or
    /// above the primary produce negative offsets and the cursor lands in the wrong place.
    /// </summary>
    public static Point ToAbsolute(Point screenPoint, Rectangle virtualScreen)
    {
        if (virtualScreen.Width <= 0 || virtualScreen.Height <= 0)
        {
            throw new ArgumentException("Virtual screen must have positive dimensions.", nameof(virtualScreen));
        }

        long x = (long)(screenPoint.X - virtualScreen.X) * NativeMethods.AbsoluteCoordinateRange / virtualScreen.Width;
        long y = (long)(screenPoint.Y - virtualScreen.Y) * NativeMethods.AbsoluteCoordinateRange / virtualScreen.Height;

        return new Point((int)x, (int)y);
    }
}
