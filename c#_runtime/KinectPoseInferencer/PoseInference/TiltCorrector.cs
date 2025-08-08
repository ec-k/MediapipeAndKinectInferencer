using K4AdotNet.Sensor;
using System;
using System.Numerics;
using HumanLandmarks;
using K4AdotNet;

namespace KinectPoseInferencer.PoseInference
{
    internal class TiltCorrector: IDisposable
    {
        System.Numerics.Quaternion _inversedCameraTiltRotation;

        internal void UpdateTiltRotation(ImuSample imuSample, Calibration sensorCalibration)
        {
            var cameraTiltRotation = CalculateTiltRotation(imuSample, sensorCalibration);
            _inversedCameraTiltRotation = System.Numerics.Quaternion.Inverse(cameraTiltRotation);
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

            var cameraTiltRotation = Utils.FromToRotation(R_down, R_gravity);

            return cameraTiltRotation;
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

        public void Dispose() { }
    }
}
