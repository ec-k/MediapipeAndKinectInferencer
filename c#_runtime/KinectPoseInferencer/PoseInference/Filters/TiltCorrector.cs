using K4AdotNet.Sensor;
using System;
using System.Numerics;

namespace KinectPoseInferencer.PoseInference.Filters
{
    internal class TiltCorrector: IPositionFilter
    {
        Quaternion _inversedCameraTiltRotation;

        internal void UpdateTiltRotation(ImuSample imuSample, Calibration sensorCalibration)
        {
            var cameraTiltRotation = CalculateTiltRotation(imuSample, sensorCalibration);
            _inversedCameraTiltRotation = Quaternion.Inverse(cameraTiltRotation);
            Console.WriteLine("Calibrated");
        }

        internal void ResetTiltRotation()
        {
            _inversedCameraTiltRotation = Quaternion.Identity;
            Console.WriteLine("Reset");
        }

        Vector3 GetAccelerometerMeasurement(ImuSample imuSample)
        {
            var measuredAccelVector = new Vector3(imuSample.AccelerometerSample.X, imuSample.AccelerometerSample.Y, imuSample.AccelerometerSample.Z);
            return measuredAccelVector;
        }

        Quaternion CalculateTiltRotation(ImuSample imuSample, Calibration sensorCalibration)
        {
            var measuredSensorUpVector_AccelSpace = GetAccelerometerMeasurement(imuSample);
            var idealUpVector_AccelSpace = -Vector3.UnitZ;

            var coordTransform_AccelToDepth = sensorCalibration.GetExtrinsics(CalibrationGeometry.Accel, CalibrationGeometry.Depth).Rotation;
            var actualSensorUpVector_DepthSpace = measuredSensorUpVector_AccelSpace.Transform(coordTransform_AccelToDepth);
            var idealUpVector_DepthSpace = idealUpVector_AccelSpace.Transform(coordTransform_AccelToDepth);

            var cameraTiltRotation = Utils.FromToRotation(idealUpVector_DepthSpace, actualSensorUpVector_DepthSpace);
            return cameraTiltRotation;
        }

        public Vector3 Apply(Vector3 position)
        {
            var pos = new Vector3(position.X, position.Y, position.Z);
            var convertedPos = Vector3.Transform(pos, _inversedCameraTiltRotation);
            return pos;
        }
    }
}
