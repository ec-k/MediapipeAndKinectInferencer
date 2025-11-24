using System;
using System.Collections.Generic;
using System.Numerics;

namespace KinectPoseInferencer.Renderers;

public static class SphereGeometryBuilder
{
    private const float Radius = 1.0f;
    private const int MinSectorCount = 3;
    private const int MinStackCount = 2;

    /// <summary>
    /// Creates the mesh geometry (vertices and indices) for a sphere.
    /// (Ported from original SphereRenderer.cs BuildVertices logic)
    /// </summary>
    /// <param name="sectorCount">Number of sectors (slices) around the Z-axis. Min 3.</param>
    /// <param name="stackCount">Number of stacks (layers) along the Z-axis. Min 2.</param>
    /// <param name="vertices">The generated list of vertices.</param>
    /// <param name="indices">The generated list of indices.</param>
    public static void Build(int sectorCount, int stackCount, out List<Vertex> vertices, out List<int> indices)
    {
        sectorCount = Math.Max(sectorCount, MinSectorCount);
        stackCount = Math.Max(stackCount, MinStackCount);

        var radiusInv = 1.0f / Radius;
        var sectorStep = (float)(2 * Math.PI / sectorCount);
        var stackStep = Math.PI / stackCount;

        vertices = new List<Vertex>();
        for (int i = 0; i <= stackCount; ++i)
        {
            var stackAngle = (float)(Math.PI / 2 - i * stackStep);
            var xy = Radius * Math.Cos(stackAngle);
            float z = (float)(Radius * Math.Sin(stackAngle));

            for (int j = 0; j <= sectorCount; ++j)
            {
                var sectorAngle = j * sectorStep;

                Vector3 position = new Vector3();
                position.X = (float)(xy * Math.Cos(sectorAngle));
                position.Y = (float)(xy * Math.Sin(sectorAngle));
                position.Z = z;

                Vector3 normal = new Vector3(position.X, position.Y, position.Z) * radiusInv;

                vertices.Add(new Vertex { Position = position, Normal = normal });
            }
        }

        indices = new List<int>();
        for (int i = 0; i < stackCount; ++i)
        {
            var k1 = i * (sectorCount + 1);
            var k2 = k1 + sectorCount + 1;

            for (int j = 0; j < sectorCount; ++j, ++k1, ++k2)
            {
                // Triangles for the upper hemisphere excluding the top pole (i != 0)
                if (i != 0)
                {
                    indices.Add(k1);
                    indices.Add(k2);
                    indices.Add(k1 + 1);
                }
                // Triangles for the lower hemisphere excluding the bottom pole (i != stackCount - 1)
                if (i != stackCount - 1)
                {
                    indices.Add(k1 + 1);
                    indices.Add(k2);
                    indices.Add(k2 + 1);
                }
            }
        }
    }
}
