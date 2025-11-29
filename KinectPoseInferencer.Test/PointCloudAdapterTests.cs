using System.Numerics;

using KinectPoseInferencer.Renderers;

namespace KinectPoseInferencer.Tests
{

    [TestClass]
    public class PointCloudAdapterTests
    {
        [TestMethod]
        public void CreatePointsVisual_WithData_ReturnsCorrectCount()
        {
            // Arrange
            const int expectedPointCount = 100;
            var dummyPoints = new List<Vector3>();

            for (int i = 0; i < expectedPointCount; i++)
                dummyPoints.Add(new Vector3(i, i * 2, 1.0f + i / 100.0f));

            // Act
            var pointsVisual = PointCloudAdapter.CreatePointsVisual(dummyPoints);

            // Assert
            Assert.IsNotNull(pointsVisual);
            var points = pointsVisual.Points;

            Assert.IsNotNull(points);
            Assert.AreEqual(expectedPointCount, points.Count);
        }
    }
}
