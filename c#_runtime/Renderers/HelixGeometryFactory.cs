// Copyright (c) Microsoft Corporation. All rights reserved.
// Released under the MIT license.
// Source: https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

// The original code has been modified and adapted for the MediapipeAndKinectInferencer project.

using KinectPoseInferencer.Renderers.Unused;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace KinectPoseInferencer.Renderers;

/// <summary>
/// Converts raw geometry data (Vertex/int lists) into HelixToolkit-compatible MeshGeometry3D objects.
/// </summary>
public static class HelixGeometryFactory
{
    /// <summary>
    /// Converts a list of custom Vertices and indices into a MeshGeometry3D.
    /// </summary>
    /// <param name="vertices">The list of custom vertices containing Position and Normal.</param>
    /// <param name="indices">The list of triangle indices.</param>
    /// <returns>A WPF MeshGeometry3D instance.</returns>
    public static MeshGeometry3D CreateMesh(List<Vertex> vertices, List<int> indices)
    {
        var mesh = new MeshGeometry3D();

        var positions = new Point3DCollection(vertices.Count);
        var normals = new Vector3DCollection(vertices.Count);

        foreach (var v in vertices)
        {
            positions.Add(new Point3D(v.Position.X, v.Position.Y, v.Position.Z));
            normals.Add(new Vector3D(v.Normal.X, v.Normal.Y, v.Normal.Z));
        }

        mesh.Positions = positions;
        mesh.Normals = normals;

        mesh.TriangleIndices = new Int32Collection(indices);

        return mesh;
    }
}
