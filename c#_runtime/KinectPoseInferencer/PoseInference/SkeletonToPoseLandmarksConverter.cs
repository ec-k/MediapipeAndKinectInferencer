using HumanLandmarks;
using K4AdotNet.Sensor;
using K4AdotNet.BodyTracking;
using System;

namespace KinectPoseInferencer.PoseInference
{
    internal class SkeletonToPoseLandmarksConverter
    {
        readonly TiltCorrector _tiltCorrector;

        public SkeletonToPoseLandmarksConverter(TiltCorrector tiltCorrector)
        {
            _tiltCorrector = tiltCorrector ?? throw new ArgumentNullException(nameof(tiltCorrector));
        }

        internal void UpdateTiltRotation(ImuSample imuSample, Calibration calibration) => _tiltCorrector.UpdateTiltRotation(imuSample, calibration);
        internal void ResetTiltRotation() => _tiltCorrector.ResetTiltRotation();

        internal KinectPoseLandmarks Convert(Skeleton skeleton)
        {
            var kinectBodyLandmarks = new KinectPoseLandmarks();
            const int length = (int)KinectPoseLandmarks.Types.LandmarkIndex.Length;
            var poseLandmarks = new Landmark[length];

            var enumArr = Enum.GetValues(typeof(JointType));
            for (var jointId = 0; jointId < enumArr.Length; jointId++)
            {
                var joint = skeleton[(JointType)jointId];
                var lm = PackLandmark(joint);
                poseLandmarks[jointId] = lm;
            }
            for (var i = 0; i < poseLandmarks.Length; i++)
            {
                if (poseLandmarks[i] == null)
                    poseLandmarks[i] = new Landmark();
            }
            if (poseLandmarks != null && poseLandmarks.Length > 0)
                kinectBodyLandmarks.Landmarks.AddRange(poseLandmarks);

            return kinectBodyLandmarks;
        }

        Landmark PackLandmark(Joint joint)
        {
            var lm = new Landmark();
            var position = new Position();

            // Convert position from millimeters to meters
            position.X = joint.PositionMm.X / 1000;
            position.Y = joint.PositionMm.Y / 1000;
            position.Z = joint.PositionMm.Z / 1000;

            (position.X, position.Y, position.Z) = _tiltCorrector.CorrectLandmarkPosition(position.X, position.Y, position.Z);
            (position.X, position.Y, position.Z) = TransformCoordination(position.X, position.Y, position.Z);

            lm.Position = position;
            lm.Confidence = joint.ConfidenceLevel switch
            {
                JointConfidenceLevel.None => 0f,
                JointConfidenceLevel.Low => 0.3f,
                JointConfidenceLevel.Medium => 0.6f,
                JointConfidenceLevel.High => 0.9f,
                _ => throw new InvalidOperationException()
            };

            return lm;
        }

        (float, float, float) TransformCoordination(float x, float y, float z)
            => (-x, -y, -z);
    }
}
