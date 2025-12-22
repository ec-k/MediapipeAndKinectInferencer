using HumanLandmarks;
using K4AdotNet.Sensor;
using System.Numerics;


namespace KinectPoseInferencer.PoseInference.Filters;

public class TiltCorrector: ILandmarkFilter
{
    Quaternion _inversedCameraTiltRotation = Quaternion.Identity;

    public void UpdateTiltRotation(ImuSample imuSample, Calibration sensorCalibration)
    {
        var cameraTiltRotation = CalculateTiltRotation(imuSample, sensorCalibration);
        _inversedCameraTiltRotation = Quaternion.Inverse(cameraTiltRotation);
    }

    public void ResetTiltRotation()
    {
        _inversedCameraTiltRotation = Quaternion.Identity;
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

        var cameraTiltRotation = KinectPoseInferencer.Utils.FromToRotation(idealUpVector_DepthSpace, actualSensorUpVector_DepthSpace);
        return cameraTiltRotation;
    }

    public Landmark Apply(in Landmark landmark)
    {
        var pos = new Vector3((float)landmark.Position.X, (float)landmark.Position.Y, (float)landmark.Position.Z);
        var convertedPos = Vector3.Transform(pos, _inversedCameraTiltRotation);

        return new Landmark
        {
            Position = new Position()
            {
                X = convertedPos.X,
                Y = convertedPos.Y,
                Z = convertedPos.Z
            },
            Rotation = landmark.Rotation,
            Confidence = landmark.Confidence
        };
    }
}
