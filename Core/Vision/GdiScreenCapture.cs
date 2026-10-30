using Core.Native;
using System.Drawing;

namespace Core.Vision;

/// <summary>
/// Captures window pixels via GDI, reusing its memory DC and bitmap between frames
/// so a polling loop does not allocate per capture.
/// </summary>
/// <remarks>
/// Reads the on-screen composite of the window, so the window must be visible and
/// unoccluded. RuneLite's GPU plugin can also defeat this and yield black frames —
/// see the README's known limitations.
/// </remarks>
public sealed unsafe class GdiScreenCapture : IScreenCapture
{
    private nint _memoryDc;
    private nint _bitmap;
    private nint _previousBitmap;
    private Size _bitmapSize;
    private bool _disposed;

    public Frame Capture(nint hWnd, Rectangle region)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentException($"Capture region must be non-empty, got {region}.", nameof(region));
        }

        nint windowDc = NativeMethods.GetDC(hWnd);

        if (windowDc == 0)
        {
            throw new InvalidOperationException($"Could not obtain a device context for window 0x{hWnd:X}.");
        }

        try
        {
            EnsureSurface(windowDc, region.Size);

            // region.X/Y are the source offset within the window's client area, so a
            // sub-region capture returns the pixels actually asked for.
            if (!NativeMethods.BitBlt(
                    _memoryDc, 0, 0, region.Width, region.Height,
                    windowDc, region.X, region.Y, NativeMethods.SRCCOPY))
            {
                throw new InvalidOperationException("BitBlt failed while capturing the window.");
            }

            return ReadPixels(windowDc, region.Size);
        }
        finally
        {
            NativeMethods.ReleaseDC(hWnd, windowDc);
        }
    }

    private void EnsureSurface(nint windowDc, Size size)
    {
        if (_memoryDc != 0 && _bitmapSize == size)
        {
            return;
        }

        ReleaseSurface();

        _memoryDc = NativeMethods.CreateCompatibleDC(windowDc);

        if (_memoryDc == 0)
        {
            throw new InvalidOperationException("Could not create a compatible device context.");
        }

        _bitmap = NativeMethods.CreateCompatibleBitmap(windowDc, size.Width, size.Height);

        if (_bitmap == 0)
        {
            ReleaseSurface();
            throw new InvalidOperationException("Could not create a compatible bitmap.");
        }

        _previousBitmap = NativeMethods.SelectObject(_memoryDc, _bitmap);
        _bitmapSize = size;
    }

    private Frame ReadPixels(nint windowDc, Size size)
    {
        int stride = size.Width * 4;

        // A fresh buffer per frame: the expensive part (DC and bitmap) is reused, while
        // each Frame owns its pixels so a later capture cannot mutate one already handed out.
        byte[] pixels = new byte[stride * size.Height];

        var header = new NativeMethods.BitmapInfoHeader()
        {
            Size = (uint)sizeof(NativeMethods.BitmapInfoHeader),
            Width = size.Width,

            // Negative height requests top-down rows, matching Frame's addressing.
            Height = -size.Height,
            Planes = 1,
            BitCount = 32,
            Compression = NativeMethods.BI_RGB,
        };

        fixed (byte* buffer = pixels)
        {
            int copied = NativeMethods.GetDIBits(
                windowDc, _bitmap, 0, (uint)size.Height, buffer, &header, NativeMethods.DIB_RGB_COLORS);

            if (copied == 0)
            {
                throw new InvalidOperationException("GetDIBits returned no scan lines.");
            }
        }

        return new Frame(pixels, size.Width, size.Height, stride);
    }

    private void ReleaseSurface()
    {
        if (_memoryDc != 0 && _previousBitmap != 0)
        {
            NativeMethods.SelectObject(_memoryDc, _previousBitmap);
            _previousBitmap = 0;
        }

        if (_bitmap != 0)
        {
            NativeMethods.DeleteObject(_bitmap);
            _bitmap = 0;
        }

        if (_memoryDc != 0)
        {
            NativeMethods.DeleteDC(_memoryDc);
            _memoryDc = 0;
        }

        _bitmapSize = Size.Empty;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseSurface();
        _disposed = true;
    }
}
