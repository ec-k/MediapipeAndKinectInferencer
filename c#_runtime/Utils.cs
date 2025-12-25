using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using K4AdotNet.Sensor;

namespace KinectPoseInferencer.WPF;

public static class Utils
{
    public static WriteableBitmap? ToWritableBitmap(this K4AdotNet.Sensor.Image image, WriteableBitmap? writeableBitmap = null)
    {
        if (image == null || image.Format != ImageFormat.ColorBgra32)
        {
            // Handle unsupported format or null image
            return null;
        }

        var width = image.WidthPixels;
        var height = image.HeightPixels;

        if (writeableBitmap?.PixelWidth != width || writeableBitmap?.PixelHeight != height)
        {
            writeableBitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);
        }

        writeableBitmap.Lock();

        try
        {
            // ピクセルデータを WriteableBitmap に書き込む
            // WritePixels は Lock()/Unlock() の間に呼び出される必要がある
            writeableBitmap.WritePixels(
                new Int32Rect(0, 0, width, height),
                image.Buffer,
                (int)image.SizeBytes,
                image.StrideBytes
            );
        }
        finally
        {
            writeableBitmap.Unlock();
        }

        return writeableBitmap;
    }
}
