using K4AdotNet.Sensor;
using KinectPoseInferencer.PoseInference;
using System;

namespace KinectPoseInferencer.Input
{
    internal class UserAction
    {
        readonly SkeletonToPoseLandmarksConverter _converter;

        public UserAction(SkeletonToPoseLandmarksConverter converter)
        {
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public void Calibrate(ImuSample imuSample, Calibration calibration)
        {
            _converter.UpdateTiltRotation(imuSample, calibration);
            Console.WriteLine("Calibration setting is updated.");
        }

        public void ResetCalibrationSetting()
        {
            _converter.ResetTiltRotation();
            Console.WriteLine("Calibration setting is reset.");
        }
    }
}
