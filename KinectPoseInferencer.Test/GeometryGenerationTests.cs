// NOTE: 
//  The original OpenGL renderer classes contain mesh generation logic (BuildVertices/BuildIndices)
//  which is essential but non-testable due to its private nature and tight coupling with OpenGL state.
//  To follow TDD, we will first extract this core geometry calculation logic into a new,
//  public static class (e.g., SphereGeometryBuilder) in the next step.
//  This TestClass uses a private static stub (SphereGeometryBuilder) to create Red tests
//  that verify the counts of generated vertices and indices against expected values.

using System.Numerics;
using KinectPoseInferencer.Renderers;

namespace KinectPoseInferencer.Tests;


[TestClass]
public class GeometryGenerationTests
{
    [TestMethod]
    public void BuildSphere_DefaultSegments_ReturnsCorrectCounts()
    {
        // Default values of SphereRenderer: sectorCount=36, stackCount=18
        const int sectorCount = 36;
        const int stackCount = 18;

        const int expectedVertexCount = (18 + 1) * (36 + 1);
        const int expectedIndexCount = 6 * 36 * (18 - 1);

        // Act
        SphereGeometryBuilder.Build(sectorCount, stackCount, out var vertices, out var indices);

        // Assert
        Assert.IsNotNull(vertices);
        Assert.IsNotNull(indices);
        Assert.AreEqual(expectedVertexCount, vertices.Count);
        Assert.AreEqual(expectedIndexCount, indices.Count);
    }

    [TestMethod]
    public void BuildSphere_MinimumSegments_ReturnsCorrectCounts()
    {
        // Default values of SphereRenderer: sectorCount=3, stackCount=2
        const int sectorCount = 3;
        const int stackCount = 2;

        const int expectedVertexCount = (2 + 1) * (3 + 1);
        const int expectedIndexCount = 6 * 3 * (2 - 1);

        // Act
        SphereGeometryBuilder.Build(sectorCount, stackCount, out var vertices, out var indices);

        // Assert
        Assert.IsNotNull(vertices);
        Assert.IsNotNull(indices);
        Assert.AreEqual(expectedVertexCount, vertices.Count);
        Assert.AreEqual(expectedIndexCount, indices.Count);
    }
}
