using K4AdotNet.Sensor;
using KinectPoseInferencer.PoseInference.Filters;
using System;

namespace KinectPoseInferencer.Input
{
    internal class UserAction
    {
        readonly TiltCorrector _tiltCorrector;

        public UserAction(TiltCorrector tiltCorrector)
        {
            _tiltCorrector = tiltCorrector ?? throw new ArgumentNullException(nameof(tiltCorrector));
        }

        public void Calibrate(ImuSample imuSample, Calibration calibration)
        {
            _tiltCorrector.UpdateTiltRotation(imuSample, calibration);
            Console.WriteLine("Calibration setting is updated.");
        }

        public void ResetCalibrationSetting()
        {
            _tiltCorrector.ResetTiltRotation();
            Console.WriteLine("Calibration setting is reset.");
        }
    }
}
