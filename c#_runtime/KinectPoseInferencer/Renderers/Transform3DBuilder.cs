using System.Numerics;
using System.Windows.Media.Media3D;

namespace KinectPoseInferencer.Renderers;


public static class Transform3DBuilder
{
    /// <summary>
    /// Converts a System.Numerics.Matrix4x4 to a WPF System.Windows.Media.Media3D.Transform3D (MatrixTransform3D).
    /// Both Matrix4x4 and Matrix3D are Row-Major, making the conversion straightforward.
    /// </summary>
    /// <param name="matrix">The source Matrix4x4.</param>
    /// <returns>A MatrixTransform3D object.</returns>
    public static MatrixTransform3D CreateTransform(Matrix4x4 matrix)
    {
        // flipped
        //var finalMatrix = new Matrix3D(
        //        matrix.M11, matrix.M12, -matrix.M13, -matrix.M14,
        //        matrix.M21, matrix.M22, -matrix.M23, -matrix.M24,
        //        -matrix.M31, -matrix.M32, -matrix.M33, -matrix.M34, 
        //        matrix.M41, matrix.M42, -matrix.M43, matrix.M44
        //    );

        var finalMatrix = new Matrix3D(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            );

        return new MatrixTransform3D(finalMatrix);
    }
}
