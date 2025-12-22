// Copyright (c) Microsoft Corporation. All rights reserved.
// Released under the MIT license.
// Source: https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

// The original code has been modified and adapted for the MediapipeAndKinectInferencer project.

using HelixToolkit.Wpf;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using ZLinq;

namespace KinectPoseInferencer.Renderers
{
    /// <summary>
    /// Converts a list of System.Numerics.Vector3 points into a HelixToolkit.Wpf.PointsVisual3D element.
    /// </summary>
    public static class PointCloudAdapter
    {
        const double DefaultPointSize = 0.003;

        /// <summary>
        /// Creates a PointsVisual3D from a list of Vector3 positions.
        /// </summary>
        public static PointsVisual3D CreatePointsVisual(List<Vector3> points)
        {
            var pointsVisual = new PointsVisual3D
            {
                Color = Colors.White,
                Size = DefaultPointSize,
            };

            var point3DCollection = new Point3DCollection(
                    points
                    .AsValueEnumerable()
                    .Select(v => new Point3D(v.X, v.Y, v.Z)).ToList() // convert points: Vector3 -> Point3D
                );

            pointsVisual.Points = point3DCollection;

            return pointsVisual;
        }
    }
}
