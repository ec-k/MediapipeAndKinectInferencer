using K4AdotNet;
using K4AdotNet.Sensor;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KinectPoseInferencer
{
    internal static class Utils
    {
        internal static Vector3 Transform(this Vector3 v, Float3x3 rotationMatrix)
        {
            var Rx = new Vector3(rotationMatrix[0], rotationMatrix[1], rotationMatrix[2]);
            var Ry = new Vector3(rotationMatrix[3], rotationMatrix[4], rotationMatrix[5]);
            var Rz = new Vector3(rotationMatrix[6], rotationMatrix[7], rotationMatrix[8]);

            return new Vector3(Vector3.Dot(v, Rx), Vector3.Dot(v, Ry), Vector3.Dot(v, Rz));
        }

        internal static System.Numerics.Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            // Normalize the vectors to ensure they are unit vectors
            from = Vector3.Normalize(from);
            to = Vector3.Normalize(to);
            var dot = Vector3.Dot(from, to);

            // If the vectors' directions are approximately the same, no rotation is needed.
            if (dot > 0.999999f)
                return System.Numerics.Quaternion.Identity;

            // If the vectors are nearly opposite, return a rotation of 180 degrees around an orthogonal axis.
            if (dot < -0.999999f)
            {
                var orthogonalAxis =
                    Math.Abs(from.X) > Math.Abs(from.Y) ?
                    Vector3.UnitY : Vector3.UnitX;

                var axis = Vector3.Cross(from, orthogonalAxis);
                if (axis.LengthSquared() < 0.000001f)
                    axis = Vector3.Cross(from, Vector3.UnitZ);
                return System.Numerics.Quaternion.CreateFromAxisAngle(axis, MathF.PI); // 180 degrees rotation
            }

            // Otherwise, calculate the rotation axis and angle
            {
                var axis = Vector3.Cross(from, to);
                var radAngle = MathF.Acos(dot);
                return System.Numerics.Quaternion.CreateFromAxisAngle(axis, radAngle);
            }
        }

        internal static float Magnitude(this Vector3 v)
        {
            return Vector3.Distance(Vector3.Zero, v);
        }

        internal static int IntCameraFps(FrameRate frameRate) => frameRate switch
        {
            FrameRate.Five => 5,
            FrameRate.Fifteen => 15,
            FrameRate.Thirty => 30,
            _ => throw new ArgumentOutOfRangeException(nameof(frameRate), frameRate, null)
        };

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
}
