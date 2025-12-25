using HumanLandmarks;
using K4AdotNet.BodyTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using ZLinq;


namespace KinectPoseInferencer.Core.PoseInference.Utils;

public class SkeletonToPoseLandmarksConverter
{
    public KinectPoseLandmarks Convert(Skeleton skeleton)
    {
        var kinectBodyLandmarks = new KinectPoseLandmarks();
        var packedLandmarks = Enum.GetValues(typeof(JointType))
            .AsValueEnumerable()
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

    Landmark PackLandmark(Joint joint)
    {
        var lm = new Landmark
        {
            Position = new Position
            {
                X = joint.PositionMm.X,
                Y = joint.PositionMm.Y,
                Z = joint.PositionMm.Z
            },
            Rotation = new Rotation
            {
                W = joint.Orientation.W,
                X = joint.Orientation.X,
                Y = joint.Orientation.Y,
                Z = joint.Orientation.Z
            },
            Confidence = ConfidenceLevelToFloat(joint.ConfidenceLevel)
        };

        return lm;
    }

    float ConfidenceLevelToFloat(JointConfidenceLevel level)
    {
        return level switch
        {
            JointConfidenceLevel.None => 0f,
            JointConfidenceLevel.Low => 0.3f,
            JointConfidenceLevel.Medium => 0.6f,
            JointConfidenceLevel.High => 0.9f,
            _ => throw new InvalidOperationException()
        };
    }
}
