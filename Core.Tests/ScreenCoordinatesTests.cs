using Core.Native;
using System.Drawing;

namespace Core.Tests;

public class ScreenCoordinatesTests
{
    private static readonly Rectangle SingleMonitor = new(0, 0, 1920, 1080);

    [Fact]
    public void ToAbsolute_MapsOriginToZero() =>
        Assert.Equal(new Point(0, 0), ScreenCoordinates.ToAbsolute(Point.Empty, SingleMonitor));

    [Fact]
    public void ToAbsolute_MapsCenterToRoughlyHalfTheRange()
    {
        Point absolute = ScreenCoordinates.ToAbsolute(new Point(960, 540), SingleMonitor);

        Assert.InRange(absolute.X, 32000, 33000);
        Assert.InRange(absolute.Y, 32000, 33000);
    }

    [Fact]
    public void ToAbsolute_MapsFarCornerNearTheTopOfTheRange()
    {
        Point absolute = ScreenCoordinates.ToAbsolute(new Point(1919, 1079), SingleMonitor);

        Assert.InRange(absolute.X, 65400, 65535);
        Assert.InRange(absolute.Y, 65400, 65535);
    }

    [Fact]
    public void ToAbsolute_SubtractsVirtualScreenOrigin()
    {
        // A second monitor placed left of and above the primary gives the virtual
        // desktop a negative origin. Without subtracting it the cursor lands elsewhere.
        var virtualScreen = new Rectangle(-1920, -1080, 3840, 2160);

        Point absolute = ScreenCoordinates.ToAbsolute(new Point(-1920, -1080), virtualScreen);

        Assert.Equal(new Point(0, 0), absolute);
    }

    [Fact]
    public void ToAbsolute_MapsPrimaryOriginOnAMultiMonitorDesktop()
    {
        var virtualScreen = new Rectangle(-1920, 0, 3840, 1080);

        Point absolute = ScreenCoordinates.ToAbsolute(Point.Empty, virtualScreen);

        // The primary origin sits halfway across a desktop that extends to its left.
        Assert.InRange(absolute.X, 32000, 33000);
        Assert.Equal(0, absolute.Y);
    }

    [Fact]
    public void ToAbsolute_ScalesXAndYIndependently()
    {
        var wide = new Rectangle(0, 0, 1000, 100);

        Point absolute = ScreenCoordinates.ToAbsolute(new Point(500, 50), wide);

        Assert.InRange(absolute.X, 32000, 33000);
        Assert.InRange(absolute.Y, 32000, 33000);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-1, 1080)]
    [InlineData(1920, -1)]
    public void ToAbsolute_RejectsDegenerateVirtualScreen(int width, int height) =>
        Assert.Throws<ArgumentException>(
            () => ScreenCoordinates.ToAbsolute(Point.Empty, new Rectangle(0, 0, width, height)));

    [Fact]
    public void ToAbsolute_DoesNotOverflowOnLargeCoordinates()
    {
        // The intermediate multiplication by 65535 overflows int for large desktops,
        // so the calculation must widen before multiplying.
        var huge = new Rectangle(0, 0, 100_000, 100_000);

        Point absolute = ScreenCoordinates.ToAbsolute(new Point(99_999, 99_999), huge);

        Assert.InRange(absolute.X, 0, 65535);
        Assert.InRange(absolute.Y, 0, 65535);
    }
}
