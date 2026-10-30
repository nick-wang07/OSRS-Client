using System.Drawing;

namespace Core.Vision;

/// <summary>
/// An immutable captured image in 32bpp BGRA order (the layout GDI produces).
/// Pure data plus pixel queries — holds no unmanaged resources, so it can be
/// constructed directly from a byte array in tests.
/// </summary>
public sealed class Frame
{
    private readonly byte[] _pixels;

    public int Width { get; }

    public int Height { get; }

    /// <summary>Bytes per row, which may exceed <see cref="Width"/> * 4 because of row padding.</summary>
    public int Stride { get; }

    public Rectangle Bounds => new(0, 0, Width, Height);

    public Frame(byte[] pixels, int width, int height, int stride)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (stride < width * 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stride), stride, "Stride must be at least four bytes per pixel.");
        }

        if (pixels.Length < stride * height)
        {
            throw new ArgumentException(
                $"Buffer holds {pixels.Length} bytes but {stride * height} are required.", nameof(pixels));
        }

        _pixels = pixels;
        Width = width;
        Height = height;
        Stride = stride;
    }

    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>Reads a pixel. Alpha is forced opaque because GDI blitting leaves it undefined.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The coordinate lies outside the frame.</exception>
    public Color GetPixel(int x, int y)
    {
        if (!Contains(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x), $"({x}, {y}) lies outside the {Width}x{Height} frame.");
        }

        int offset = (y * Stride) + (x * 4);

        return Color.FromArgb(255, _pixels[offset + 2], _pixels[offset + 1], _pixels[offset]);
    }

    public bool TryGetPixel(int x, int y, out Color color)
    {
        if (!Contains(x, y))
        {
            color = default;
            return false;
        }

        color = GetPixel(x, y);
        return true;
    }

    /// <summary>Per-channel comparison against an inclusive tolerance. Alpha is ignored.</summary>
    public static bool ColorsMatch(Color first, Color second, int tolerance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);

        return Math.Abs(first.R - second.R) <= tolerance
            && Math.Abs(first.G - second.G) <= tolerance
            && Math.Abs(first.B - second.B) <= tolerance;
    }

    /// <summary>Intersects a region with the frame so callers cannot read out of bounds.</summary>
    public Rectangle Clamp(Rectangle region) => Rectangle.Intersect(region, Bounds);

    public bool ContainsColor(Rectangle region, Color target, int tolerance) =>
        FindFirstColor(region, target, tolerance) is not null;

    /// <summary>First matching pixel in row-major order, or null when the region holds no match.</summary>
    public Point? FindFirstColor(Rectangle region, Color target, int tolerance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);

        var area = Clamp(region);

        for (int y = area.Top; y < area.Bottom; y++)
        {
            int rowStart = y * Stride;

            for (int x = area.Left; x < area.Right; x++)
            {
                int offset = rowStart + (x * 4);

                if (Math.Abs(_pixels[offset + 2] - target.R) <= tolerance
                    && Math.Abs(_pixels[offset + 1] - target.G) <= tolerance
                    && Math.Abs(_pixels[offset] - target.B) <= tolerance)
                {
                    return new Point(x, y);
                }
            }
        }

        return null;
    }

    /// <summary>Counts matching pixels, which is how "is this orb lit" style checks stay robust to noise.</summary>
    public int CountColor(Rectangle region, Color target, int tolerance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);

        var area = Clamp(region);
        int count = 0;

        for (int y = area.Top; y < area.Bottom; y++)
        {
            int rowStart = y * Stride;

            for (int x = area.Left; x < area.Right; x++)
            {
                int offset = rowStart + (x * 4);

                if (Math.Abs(_pixels[offset + 2] - target.R) <= tolerance
                    && Math.Abs(_pixels[offset + 1] - target.G) <= tolerance
                    && Math.Abs(_pixels[offset] - target.B) <= tolerance)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
