using K4AdotNet.Sensor;
using KinectPoseInferencer.PoseInference;
using System;

namespace KinectPoseInferencer.Input
{
    internal class UserAction
    {
        readonly LandmarkHandler _landmarkHandler;

        public UserAction(LandmarkHandler landmarkHandler)
        {
            _landmarkHandler = landmarkHandler ?? throw new ArgumentNullException(nameof(landmarkHandler));
        }

        public void Calibrate(ImuSample imuSample, Calibration calibration)
        {
            _landmarkHandler.UpdateTiltRotation(imuSample, calibration);
            Console.WriteLine("Calibration setting is updated.");
        }

        public void ResetCalibrationSetting()
        {
            _landmarkHandler.ResetTiltRotation();
            Console.WriteLine("Calibration setting is reset.");
        }
    }
}
