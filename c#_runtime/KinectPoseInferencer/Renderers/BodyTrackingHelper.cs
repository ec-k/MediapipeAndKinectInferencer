// Copyright (c) Microsoft Corporation. All rights reserved.
// Released under the MIT license.
// Source: https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

// The original code has been modified and adapted for the MediapipeAndKinectInferencer project.

using K4AdotNet;
using K4AdotNet.BodyTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Media;

namespace KinectPoseInferencer.Helpers;

public static class BodyTrackingHelper
{
    const float MillimetersToMetersFactor = 1000.0f;

    static readonly Color[] colorSet =
        {
            Colors.Red,
            Colors.Blue,
            Colors.Green,
            Colors.Yellow,
            Colors.Cyan,
            Colors.Magenta,
            Colors.Orange
        };

    /// <summary>
    /// </summary>
    /// <returns>A list of joint paires</returns>
    public static List<(JointType Parent, JointType Child)> GetBoneConnections()
    {
        var boneConnections = new List<(JointType Parent, JointType Child)>();
        var jointTypes = Enum.GetValues(typeof(JointType)).Cast<JointType>();

        foreach (var jointType in jointTypes)
        {
            try
            {
                var parentType = jointType.GetParent();
                    boneConnections.Add((parentType, jointType));
            }
            catch (ArgumentOutOfRangeException)
            {
                // Reach here on the bone is Pelvis

                // do nothing.
            }
        }

        return boneConnections;
    }

    /// <summary>
    /// mm (Float3) -> m (Vector3)
    /// </summary>
    public static Vector3 ConvertPositionToMeters(Float3 positionMm)
    {
        return new Vector3(positionMm.X, positionMm.Y, positionMm.Z) / MillimetersToMetersFactor;
    }

    /// <summary>
    /// Get color according to Body ID
    /// </summary>
    public static Color GetBodyColor(int bodyId)
    {
        return colorSet[(bodyId % colorSet.Length)];
    }
}
