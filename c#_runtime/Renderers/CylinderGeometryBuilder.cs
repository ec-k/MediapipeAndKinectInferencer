// Copyright (c) Microsoft Corporation. All rights reserved.
// Released under the MIT license.
// Source: https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

// The original code has been modified and adapted for the MediapipeAndKinectInferencer project.

using KinectPoseInferencer.Renderers.Unused;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace KinectPoseInferencer.Renderers;

public static class CylinderGeometryBuilder
{
    const float BaseRadius = 1.0f;
    const float Height = 1.0f;
    const int MinSectorCount = 3;

    /// <summary>
    /// Creates the mesh geometry (vertices and indices) for a cylinder side surface.
    /// (Ported from original CylinderRenderer.cs BuildVertices logic)
    /// Note: This logic only builds the side mesh, not the top/bottom caps.
    /// </summary>
    /// <param name="sectorCount">Number of sectors (slices) around the Z-axis. Min 3.</param>
    /// <param name="vertices">The generated list of vertices.</param>
    /// <param name="indices">The generated list of indices.</param>
    public static void Build(int sectorCount, out List<Vertex> vertices, out List<int> indices)
    {
        sectorCount = Math.Max(sectorCount, MinSectorCount);

        var radiusInv = 1.0f / BaseRadius;
        var sectorStep = (float)(2 * Math.PI / sectorCount);

        vertices = new List<Vertex>();
        for (int circleIndex = 0; circleIndex < 2; ++circleIndex)
        {
            float z = Height / 2 * (1 - 2 * circleIndex);

            for (int j = 0; j <= sectorCount; ++j)
            {
                var sectorAngle = j * sectorStep;

                Vector3 position = new Vector3();
                position.X = (float)(BaseRadius * Math.Cos(sectorAngle));
                position.Y = (float)(BaseRadius * Math.Sin(sectorAngle));
                position.Z = z;

                Vector3 normal = new Vector3(position.X, position.Y, 0.0f) * radiusInv;

                vertices.Add(new Vertex { Position = position, Normal = normal });
            }
        }

        indices = new List<int>();
        var k1 = 0;
        var k2 = k1 + sectorCount + 1;

        for (int j = 0; j < sectorCount; ++j, ++k1, ++k2)
        {
            indices.Add(k1);
            indices.Add(k2);
            indices.Add(k1 + 1);

            indices.Add(k1 + 1);
            indices.Add(k2);
            indices.Add(k2 + 1);
        }
    }
}
