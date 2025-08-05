using K4AdotNet.Sensor;
using System;
using System.Numerics;
using HumanLandmarks;
using K4AdotNet;

namespace MpAndKinectPoseSender.PoseInference
{
    internal class TiltCorrector
    {

        ImuSample _imuSample;
        Calibration _sensorCalibration;
        System.Numerics.Quaternion _inversedCameraTiltRotation;

        internal TiltCorrector(ImuSample imuSample, Calibration sensorCalibration)
        {
            _imuSample = imuSample;
            _sensorCalibration = sensorCalibration;
            _inversedCameraTiltRotation = CalculateTiltRotation(imuSample, sensorCalibration);
        }

        internal void UpdateTiltRotation()
        {
            _inversedCameraTiltRotation = CalculateTiltRotation(_imuSample, _sensorCalibration);
            Console.WriteLine("Calibrated");
        }

        internal void ResetTiltRotation()
        {
            _inversedCameraTiltRotation = System.Numerics.Quaternion.Identity;
            Console.WriteLine("Reset");
        }

        Vector3 GetGravityVector(ImuSample imuSample)
        {
            var vector3Result = new Vector3(imuSample.AccelerometerSample.X, imuSample.AccelerometerSample.Y, imuSample.AccelerometerSample.Z);
            return vector3Result;
        }

        System.Numerics.Quaternion CalculateTiltRotation(ImuSample imuSample, Calibration sensorCalibration)
        {
            var gravityVector = GetGravityVector(imuSample);
            var downVector = -Vector3.UnitZ;

            var coordinationTransformationMatrix = sensorCalibration.GetExtrinsics(CalibrationGeometry.Accel, CalibrationGeometry.Depth).Rotation;

            var R_gravity = gravityVector.Transform(coordinationTransformationMatrix);
            var R_down = downVector.Transform(coordinationTransformationMatrix);

            var cameraTiltRotation = Utils.FromToRotation(R_gravity, R_down);

            return System.Numerics.Quaternion.Inverse(cameraTiltRotation);
        }
        
        internal void CorrectLandmarkPosition(ref Landmark landmark)
        {
            var (x, y, z) = CorrectLandmarkPosition(landmark.Position.X, landmark.Position.Y, landmark.Position.Z);

            landmark.Position.X = x;
            landmark.Position.Y = y;
            landmark.Position.Z = z;
        }

        internal (float, float, float) CorrectLandmarkPosition(float x, float y, float z)
        {
            var pos = new Vector3(x, y, z);
            var convertedPos = Vector3.Transform(pos, _inversedCameraTiltRotation);
            return (convertedPos.X, convertedPos.Y, convertedPos.Z);
        }
    }

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
