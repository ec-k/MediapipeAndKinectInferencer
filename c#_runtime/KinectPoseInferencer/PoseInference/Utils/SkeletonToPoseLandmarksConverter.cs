using HumanLandmarks;
using K4AdotNet.BodyTracking;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;


using KinectPoseInferencer.PoseInference.Filters;

namespace KinectPoseInferencer.PoseInference.Utils;

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
        var packedLandmarks = Enum.GetValues(typeof(JointType))
            .Cast<JointType>()
            .Select(jointType => skeleton[jointType])
            .Select(joint => PackLandmark(joint))
            .ToList();

        var length = (int)KinectPoseLandmarks.Types.LandmarkIndex.Length;
        var poseLandmarks = new List<Landmark>(length);
        for (var i = 0; i < length; i++)
        {
            var currentLandmark = i < packedLandmarks.Count ? packedLandmarks[i] : new Landmark();
            poseLandmarks.Add(currentLandmark);
        }

        kinectBodyLandmarks.Landmarks.AddRange(poseLandmarks);
        return kinectBodyLandmarks;
    }

    // TODO: SkeletonToPoseLandmarksConverter should NOT have this method.
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
