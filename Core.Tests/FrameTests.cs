using Core.Tests.Fakes;
using Core.Vision;
using System.Drawing;

namespace Core.Tests;

public class FrameTests
{
    private static readonly Color Red = Color.FromArgb(255, 255, 0, 0);
    private static readonly Color Green = Color.FromArgb(255, 0, 255, 0);

    [Fact]
    public void Constructor_RejectsNullBuffer() =>
        Assert.Throws<ArgumentNullException>(() => new Frame(null!, 2, 2, 8));

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    [InlineData(-1, 2)]
    [InlineData(2, -1)]
    public void Constructor_RejectsNonPositiveDimensions(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Frame(new byte[256], width, height, 64));

    [Fact]
    public void Constructor_RejectsStrideNarrowerThanFourBytesPerPixel() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Frame(new byte[256], 4, 2, 15));

    [Fact]
    public void Constructor_RejectsBufferSmallerThanStrideTimesHeight()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => new Frame(new byte[31], 4, 2, 16));

        Assert.Contains("32", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_AcceptsBufferExactlyLargeEnough()
    {
        var frame = new Frame(new byte[32], 4, 2, 16);

        Assert.Equal(4, frame.Width);
        Assert.Equal(2, frame.Height);
        Assert.Equal(16, frame.Stride);
    }

    [Fact]
    public void Bounds_StartsAtOriginWithFrameDimensions()
    {
        Frame frame = new FrameBuilder(6, 4).Build();

        Assert.Equal(new Rectangle(0, 0, 6, 4), frame.Bounds);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3, 2, true)]
    [InlineData(4, 2, false)]
    [InlineData(3, 3, false)]
    [InlineData(-1, 0, false)]
    [InlineData(0, -1, false)]
    public void Contains_MatchesFrameExtent(int x, int y, bool expected)
    {
        Frame frame = new FrameBuilder(4, 3).Build();

        Assert.Equal(expected, frame.Contains(x, y));
    }

    [Fact]
    public void GetPixel_ReturnsStoredColor()
    {
        Frame frame = new FrameBuilder(4, 3).Fill(Green).Set(2, 1, Red).Build();

        Assert.Equal(Red.ToArgb(), frame.GetPixel(2, 1).ToArgb());
        Assert.Equal(Green.ToArgb(), frame.GetPixel(0, 0).ToArgb());
    }

    [Fact]
    public void GetPixel_ForcesAlphaOpaqueBecauseBlittingLeavesItUndefined()
    {
        // Alpha deliberately zeroed, as GDI leaves it.
        Frame frame = new FrameBuilder(2, 2).Set(0, 0, Color.FromArgb(0, 10, 20, 30)).Build();

        Color pixel = frame.GetPixel(0, 0);

        Assert.Equal(255, pixel.A);
        Assert.Equal(10, pixel.R);
        Assert.Equal(20, pixel.G);
        Assert.Equal(30, pixel.B);
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(0, 3)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void GetPixel_ThrowsOutsideTheFrameRatherThanReadingAdjacentMemory(int x, int y)
    {
        Frame frame = new FrameBuilder(4, 3).Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => frame.GetPixel(x, y));
    }

    [Fact]
    public void GetPixel_RespectsRowPadding()
    {
        // Padded stride: reading row 1 must skip the padding, not run into it.
        Frame frame = new FrameBuilder(3, 2, stridePadding: 8).Fill(Green).Set(0, 1, Red).Build();

        Assert.Equal(Red.ToArgb(), frame.GetPixel(0, 1).ToArgb());
        Assert.Equal(Green.ToArgb(), frame.GetPixel(1, 1).ToArgb());
    }

    [Fact]
    public void TryGetPixel_ReturnsFalseOutsideTheFrame()
    {
        Frame frame = new FrameBuilder(2, 2).Build();

        Assert.False(frame.TryGetPixel(5, 5, out Color color));
        Assert.Equal(default, color);
    }

    [Fact]
    public void TryGetPixel_ReturnsTrueAndColorInsideTheFrame()
    {
        Frame frame = new FrameBuilder(2, 2).Fill(Red).Build();

        Assert.True(frame.TryGetPixel(1, 1, out Color color));
        Assert.Equal(Red.ToArgb(), color.ToArgb());
    }

    [Theory]
    [InlineData(100, 100, 100, 0, true)]
    [InlineData(105, 100, 100, 5, true)]
    [InlineData(106, 100, 100, 5, false)]
    [InlineData(95, 100, 100, 5, true)]
    [InlineData(94, 100, 100, 5, false)]
    public void ColorsMatch_TreatsToleranceAsInclusive(int r, int g, int b, int tolerance, bool expected)
    {
        Color actual = Color.FromArgb(r, g, b);
        Color target = Color.FromArgb(100, 100, 100);

        Assert.Equal(expected, Frame.ColorsMatch(target, actual, tolerance));
    }

    [Fact]
    public void ColorsMatch_IgnoresAlpha()
    {
        Color opaque = Color.FromArgb(255, 10, 20, 30);
        Color transparent = Color.FromArgb(0, 10, 20, 30);

        Assert.True(Frame.ColorsMatch(opaque, transparent, 0));
    }

    [Fact]
    public void ColorsMatch_RejectsNegativeTolerance() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Frame.ColorsMatch(Red, Red, -1));

    [Fact]
    public void ColorsMatch_RequiresEveryChannelWithinTolerance()
    {
        // Red and blue match; green is far off, so the color must not match.
        Color target = Color.FromArgb(100, 100, 100);
        Color actual = Color.FromArgb(100, 200, 100);

        Assert.False(Frame.ColorsMatch(target, actual, 5));
    }

    [Fact]
    public void Clamp_TrimsRegionToTheFrame()
    {
        Frame frame = new FrameBuilder(10, 10).Build();

        Assert.Equal(new Rectangle(5, 5, 5, 5), frame.Clamp(new Rectangle(5, 5, 50, 50)));
        Assert.Equal(new Rectangle(0, 0, 4, 4), frame.Clamp(new Rectangle(-4, -4, 8, 8)));
    }

    [Fact]
    public void Clamp_ReturnsEmptyForFullyOutsideRegion()
    {
        Frame frame = new FrameBuilder(10, 10).Build();

        Assert.True(frame.Clamp(new Rectangle(20, 20, 5, 5)).IsEmpty);
    }

    [Fact]
    public void FindFirstColor_ReturnsMatchInRowMajorOrder()
    {
        Frame frame = new FrameBuilder(5, 5).Fill(Green)
            .Set(4, 1, Red)
            .Set(1, 3, Red)
            .Build();

        Assert.Equal(new Point(4, 1), frame.FindFirstColor(frame.Bounds, Red, 0));
    }

    [Fact]
    public void FindFirstColor_ReturnsNullWhenAbsent()
    {
        Frame frame = new FrameBuilder(4, 4).Fill(Green).Build();

        Assert.Null(frame.FindFirstColor(frame.Bounds, Red, 0));
    }

    [Fact]
    public void FindFirstColor_SearchesOnlyWithinTheRegion()
    {
        Frame frame = new FrameBuilder(10, 10).Fill(Green).Set(0, 0, Red).Build();

        Assert.Null(frame.FindFirstColor(new Rectangle(5, 5, 5, 5), Red, 0));
        Assert.Equal(new Point(0, 0), frame.FindFirstColor(new Rectangle(0, 0, 2, 2), Red, 0));
    }

    [Fact]
    public void FindFirstColor_ClampsOversizedRegionInsteadOfReadingOutOfBounds()
    {
        Frame frame = new FrameBuilder(4, 4).Fill(Green).Set(3, 3, Red).Build();

        Assert.Equal(new Point(3, 3), frame.FindFirstColor(new Rectangle(0, 0, 500, 500), Red, 0));
    }

    [Fact]
    public void FindFirstColor_HonorsTolerance()
    {
        Frame frame = new FrameBuilder(3, 3).Fill(Color.FromArgb(100, 100, 100))
            .Set(1, 1, Color.FromArgb(108, 100, 100))
            .Build();

        Color target = Color.FromArgb(108, 100, 100);

        Assert.Equal(new Point(0, 0), frame.FindFirstColor(frame.Bounds, target, 10));
        Assert.Equal(new Point(1, 1), frame.FindFirstColor(frame.Bounds, target, 0));
    }

    [Fact]
    public void FindFirstColor_RejectsNegativeTolerance()
    {
        Frame frame = new FrameBuilder(2, 2).Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => frame.FindFirstColor(frame.Bounds, Red, -1));
    }

    [Fact]
    public void ContainsColor_ReflectsPresence()
    {
        Frame frame = new FrameBuilder(4, 4).Fill(Green).Set(2, 2, Red).Build();

        Assert.True(frame.ContainsColor(frame.Bounds, Red, 0));
        Assert.False(frame.ContainsColor(frame.Bounds, Color.Blue, 0));
    }

    [Fact]
    public void CountColor_CountsEveryMatchInRegion()
    {
        Frame frame = new FrameBuilder(6, 6).Fill(Green)
            .SetRegion(new Rectangle(1, 1, 3, 2), Red)
            .Build();

        Assert.Equal(6, frame.CountColor(frame.Bounds, Red, 0));
    }

    [Fact]
    public void CountColor_CountsOnlyWithinRegion()
    {
        Frame frame = new FrameBuilder(6, 6).Fill(Green)
            .SetRegion(new Rectangle(0, 0, 6, 6), Red)
            .Build();

        Assert.Equal(4, frame.CountColor(new Rectangle(0, 0, 2, 2), Red, 0));
    }

    [Fact]
    public void CountColor_ReturnsZeroWhenNothingMatches()
    {
        Frame frame = new FrameBuilder(4, 4).Fill(Green).Build();

        Assert.Equal(0, frame.CountColor(frame.Bounds, Red, 0));
    }

    [Fact]
    public void CountColor_RejectsNegativeTolerance()
    {
        Frame frame = new FrameBuilder(2, 2).Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => frame.CountColor(frame.Bounds, Red, -1));
    }

    [Fact]
    public void CountColor_DistinguishesEachColorChannel()
    {
        // One pixel per channel offset: a match must require all three channels, and
        // each must be read from its own byte rather than a neighboring one.
        Frame frame = new FrameBuilder(4, 1)
            .Set(0, 0, Color.FromArgb(100, 100, 100))
            .Set(1, 0, Color.FromArgb(200, 100, 100))
            .Set(2, 0, Color.FromArgb(100, 200, 100))
            .Set(3, 0, Color.FromArgb(100, 100, 200))
            .Build();

        Assert.Equal(1, frame.CountColor(frame.Bounds, Color.FromArgb(100, 100, 100), 0));
        Assert.Equal(1, frame.CountColor(frame.Bounds, Color.FromArgb(200, 100, 100), 0));
        Assert.Equal(1, frame.CountColor(frame.Bounds, Color.FromArgb(100, 200, 100), 0));
        Assert.Equal(1, frame.CountColor(frame.Bounds, Color.FromArgb(100, 100, 200), 0));
    }

    [Fact]
    public void FindFirstColor_DistinguishesEachColorChannel()
    {
        Frame frame = new FrameBuilder(3, 1)
            .Set(0, 0, Color.FromArgb(10, 20, 30))
            .Set(1, 0, Color.FromArgb(30, 20, 10))
            .Set(2, 0, Color.FromArgb(20, 30, 10))
            .Build();

        // Byte order is BGRA, so a swapped index here would silently match the wrong pixel.
        Assert.Equal(new Point(1, 0), frame.FindFirstColor(frame.Bounds, Color.FromArgb(30, 20, 10), 0));
        Assert.Equal(new Point(2, 0), frame.FindFirstColor(frame.Bounds, Color.FromArgb(20, 30, 10), 0));
    }

    [Fact]
    public void CountColor_RespectsToleranceBoundary()
    {
        Frame frame = new FrameBuilder(3, 1)
            .Set(0, 0, Color.FromArgb(100, 100, 100))
            .Set(1, 0, Color.FromArgb(105, 100, 100))
            .Set(2, 0, Color.FromArgb(106, 100, 100))
            .Build();

        Color target = Color.FromArgb(100, 100, 100);

        Assert.Equal(2, frame.CountColor(frame.Bounds, target, 5));
        Assert.Equal(3, frame.CountColor(frame.Bounds, target, 6));
    }

    [Fact]
    public void CountColor_ReadsEveryRowWhenStrideIsPadded()
    {
        Frame frame = new FrameBuilder(2, 3, stridePadding: 12).Fill(Red).Build();

        Assert.Equal(6, frame.CountColor(frame.Bounds, Red, 0));
    }
}
