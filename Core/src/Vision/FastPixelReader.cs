using Core.Native;
using System.Drawing;
using System.Drawing.Imaging;

namespace Core.src.Vision;

public sealed unsafe class FastPixelReader : IDisposable
{
    private readonly Bitmap _snapshot;
    private readonly BitmapData _bitmapData;
    private readonly int _stride;
    private readonly byte* _scan0;
    private readonly int _width;
    private readonly int _height;

    public FastPixelReader(IntPtr hwnd, Rectangle bounds)
    {
        _width = bounds.Width;
        _height = bounds.Height;

        _snapshot = new Bitmap(_width, _height, PixelFormat.Format32bppArgb);

        using (Graphics g = Graphics.FromImage(_snapshot))
        {
            IntPtr hdcDest = g.GetHdc();
            IntPtr hdcSrc = NativeMethods.GetDC(hwnd);

            // BitBlt directly into the Graphics object's Hdc
            NativeMethods.BitBlt(hdcDest, 0, 0, _width, _height, hdcSrc, 0, 0, NativeMethods.SRCCOPY);

            NativeMethods.ReleaseDC(hwnd, hdcSrc);
            g.ReleaseHdc(hdcDest);
        }

        _bitmapData = _snapshot.LockBits(
            new Rectangle(0, 0, _width, _height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        _stride = _bitmapData.Stride;
        _scan0 = (byte*)_bitmapData.Scan0;
    }

    public Color GetPixel(int x, int y)
    {
        byte* row = _scan0 + (y * _stride);
        int pixelOffset = x * 4;

        return Color.FromArgb(
            row[pixelOffset + 3], // A
            row[pixelOffset + 2], // R
            row[pixelOffset + 1], // G
            row[pixelOffset + 0]  // B
        );
    }

    public static bool IsColorMatch(Color target, Color actual, int tolerance)
    {
        return Math.Abs(target.R - actual.R) <= tolerance &&
               Math.Abs(target.G - actual.G) <= tolerance &&
               Math.Abs(target.B - actual.B) <= tolerance;
    }

    public bool AnyPixelInRange(Rectangle rect, Color target, int tolerance)
    {
        for (int y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            for (int x = rect.X; x < rect.X + rect.Width; x++)
            {
                if (IsColorMatch(target, GetPixel(x, y), tolerance))
                    return true;
            }
        }
        return false;
    }

    public void Dispose()
    {
        _snapshot.UnlockBits(_bitmapData ?? throw new InvalidDataException("Bitmap data was null"));
        _snapshot.Dispose();
    }
}