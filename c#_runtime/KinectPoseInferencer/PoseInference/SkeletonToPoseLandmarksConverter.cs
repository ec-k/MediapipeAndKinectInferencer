using HumanLandmarks;
using K4AdotNet.BodyTracking;
using System;
using System.Numerics;
using KinectPoseInferencer.PoseInference.Filters;
using System.Collections.Generic;
using System.Linq;

namespace KinectPoseInferencer.PoseInference
{
    internal class SkeletonToPoseLandmarksConverter
    {
        readonly IEnumerable<IPositionFilter> _positionFilters;

        public SkeletonToPoseLandmarksConverter(IEnumerable<IPositionFilter> positionFilters)
        {
            _positionFilters = positionFilters ?? throw new ArgumentNullException(nameof(positionFilters));
        }

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

            var initialPosition = new Vector3(joint.PositionMm.X, joint.PositionMm.Y, joint.PositionMm.Z);
            var filteredPosition = _positionFilters.Aggregate(initialPosition, (current, filter) => filter.Apply(current));

            lm.Position = new Position()
            {
                X = filteredPosition.X,
                Y = filteredPosition.Y,
                Z = filteredPosition.Z
            };

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

        
    }
}
