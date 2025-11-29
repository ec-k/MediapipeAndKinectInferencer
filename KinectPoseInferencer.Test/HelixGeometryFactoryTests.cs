using KinectPoseInferencer.Renderers;

namespace KinectPoseInferencer.Tests;


[TestClass]
public class HelixGeometryFactoryTests
{
    [TestMethod]
    public void CreateSphereMesh_ReturnsValidMeshGeometry()
    {
        // Arrange
        var sectorCount = 10;
        var stackCount = 5;
        SphereGeometryBuilder.Build(sectorCount, stackCount, out var rawVertices, out var rawIndices);

        var expectedPositionCount = (stackCount + 1) * (sectorCount + 1);
        var expectedIndexCount = 6 * sectorCount * (stackCount - 1);

        // Act
        var mesh = HelixGeometryFactory.CreateMesh(rawVertices, rawIndices);

        // Assert
        Assert.IsNotNull(mesh);
        Assert.IsNotNull(mesh.Positions);
        Assert.IsNotNull(mesh.TriangleIndices);

        Assert.AreEqual(expectedPositionCount, mesh.Positions.Count);
        Assert.AreEqual(expectedIndexCount, mesh.TriangleIndices.Count);
    }
}
