using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using K4AdotNet.Sensor;
using System;
using System.Runtime.InteropServices;

namespace KinectPoseInferencer.Avalonia;

internal static class Utils
{
    public static WriteableBitmap? ToWriteableBitmap(this K4AdotNet.Sensor.Image kinectImage, WriteableBitmap? writeableBitmap = null)
    {
        if (kinectImage is null || kinectImage.Format != ImageFormat.ColorBgra32)
        {
            // Handle unsupported format or null image
            return null;
        }

        var width = kinectImage.WidthPixels;
        var height = kinectImage.HeightPixels;
        var stride = kinectImage.StrideBytes;
        var size = kinectImage.SizeBytes;
        var kinectBufferPtr = kinectImage.Buffer;

        var pixelSize = new PixelSize(width, height);
        var dpi = new global::Avalonia.Vector(96, 96);
        var pixelFormat = PixelFormat.Bgra8888;
        var alphaFormat = AlphaFormat.Premul;

        if (writeableBitmap is null || writeableBitmap.PixelSize != pixelSize)
        {
            writeableBitmap = new WriteableBitmap(
                pixelSize,
                dpi,
                pixelFormat,
                alphaFormat
            );
        }

        if (kinectBufferPtr == IntPtr.Zero) return writeableBitmap;

        var buffer = new byte[size];
        Marshal.Copy(kinectBufferPtr, buffer, 0, size);

        using (var lockedBitmap = writeableBitmap.Lock())
        {
            Marshal.Copy(buffer, 0, lockedBitmap.Address, size);
        }

        return writeableBitmap;
    }
}
