using K4AdotNet;
using System;
using System.Numerics;

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
    }
}
