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
            var axis = Vector3.Cross(from, to);

            if (axis == Vector3.Zero) return System.Numerics.Quaternion.Identity;

            var radAngle = MathF.Acos(Vector3.Dot(from, to) / (from.Magnitude() * to.Magnitude()));

            return System.Numerics.Quaternion.CreateFromAxisAngle(axis, radAngle);
        }

        internal static float Magnitude(this Vector3 v)
        {
            return Vector3.Distance(Vector3.Zero, v);
        }
    }
}
