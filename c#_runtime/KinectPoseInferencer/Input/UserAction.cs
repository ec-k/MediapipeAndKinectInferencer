using KinectPoseInferencer.PoseInference;
using System;

namespace KinectPoseInferencer.Input
{
    internal class UserAction
    {
        readonly LandmarkHandler _landmarkHandler;
        internal UserAction(LandmarkHandler landmarkHandler)
        {
            _landmarkHandler = landmarkHandler;
        }

        public void Calibrate()
        {
            _landmarkHandler.UpdateTiltRotation();
            Console.WriteLine("Calibration setting is updated.");
        }

        public void ResetCalibrationSetting()
        {
            _landmarkHandler.ResetTiltRotation();
            Console.WriteLine("Calibration setting is reset.");
        }
    }
}
