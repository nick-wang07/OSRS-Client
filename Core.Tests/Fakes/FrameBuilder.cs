using Core.Vision;
using System.Drawing;

namespace Core.Tests.Fakes;

/// <summary>Builds <see cref="Frame"/> instances with known pixel content.</summary>
public sealed class FrameBuilder(int width, int height, int stridePadding = 0)
{
    private readonly byte[] _pixels = new byte[((width * 4) + stridePadding) * height];
    private readonly int _stride = (width * 4) + stridePadding;

    public FrameBuilder Fill(Color color)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Set(x, y, color);
            }
        }

        return this;
    }

    public FrameBuilder Set(int x, int y, Color color)
    {
        int offset = (y * _stride) + (x * 4);

        _pixels[offset] = color.B;
        _pixels[offset + 1] = color.G;
        _pixels[offset + 2] = color.R;
        _pixels[offset + 3] = color.A;

        return this;
    }

    public FrameBuilder SetRegion(Rectangle region, Color color)
    {
        for (int y = region.Top; y < region.Bottom; y++)
        {
            for (int x = region.Left; x < region.Right; x++)
            {
                Set(x, y, color);
            }
        }

        return this;
    }

    public Frame Build() => new(_pixels, width, height, _stride);
}
