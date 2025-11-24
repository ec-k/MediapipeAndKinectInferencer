using System.Numerics;
using System.Windows.Media.Media3D;


namespace KinectAndInputRecorder.Tests;


public static class BoneMatrixBuilder
{
    private const float BaseScale = 0.01f;

    public static Matrix4x4 Build(Vector3 start, Vector3 end)
    {
        // implement later
        throw new System.NotImplementedException("BoneMatrixBuilder is not yet implemented.");
    }
}

[TestClass]
public class TransformBuilderTests
{
    private static readonly float Delta = 0.0001f;

    [TestMethod]
    public void BoneMatrixBuilder_ZeroVector_ReturnsIdentityScale()
    {
        // Arrange
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(0, 0, 1);

        // Act
        var modelMatrix = BoneMatrixBuilder.Build(start, end);

        // Assert
        // expected: (0.01,   0,   0, 0)
        //           (0,   0.01,   0, 0)
        //           (0,      0,   1, 0)
        //           (0,      0, 0.5, 1) <--- Translation(0, 0, 0.5)

        Assert.AreEqual(0.01f, modelMatrix.M11, Delta, "X Scale is incorrect.");
        Assert.AreEqual(0.01f, modelMatrix.M22, Delta, "Y Scale is incorrect.");
        Assert.AreEqual(1.0f,  modelMatrix.M33, Delta, "Z Scale (length) is incorrect.");
        Assert.AreEqual(0.5f,  modelMatrix.M43, Delta, "Z Translation is incorrect.");
    }

    [TestMethod]
    public void BoneMatrixBuilder_RotatedVector_HasCorrectTranslation()
    {
        // Arrange
        var start = new Vector3(1, 0, 0);
        var end = new Vector3(-1, 0, 0);

        // Act
        var modelMatrix = BoneMatrixBuilder.Build(start, end);

        // Assert
        Assert.AreEqual(0.0f, modelMatrix.M41, Delta, "X Translation is incorrect.");
        Assert.AreEqual(0.0f, modelMatrix.M42, Delta, "Y Translation is incorrect.");
        Assert.AreEqual(0.0f, modelMatrix.M43, Delta, "Z Translation is incorrect.");

        Assert.AreEqual(2.0f, modelMatrix.M33, Delta, "Z Scale (length) is incorrect.");
    }

    [TestMethod]
    public void Matrix4x4ToTransform3D_ReturnsCorrectTransform()
    {
        // Arrange:
        var rotation = Matrix4x4.CreateRotationY(MathF.PI / 2.0f);
        var translation = Matrix4x4.CreateTranslation(10, 20, 30);
        var sourceMatrix = rotation * translation;

        // Act
        var transform = Transform3DBuilder.CreateTransform(sourceMatrix);

        // Assert
        Assert.IsNotNull(transform);
        Assert.IsInstanceOfType(transform, typeof(MatrixTransform3D));

        var resultMatrix = transform.Matrix;

        Assert.AreEqual(sourceMatrix.M41, resultMatrix.OffsetX, Delta, "Translation X is incorrect.");
        Assert.AreEqual(sourceMatrix.M42, resultMatrix.OffsetY, Delta, "Translation Y is incorrect.");
        Assert.AreEqual(sourceMatrix.M43, resultMatrix.OffsetZ, Delta, "Translation Z is incorrect.");

        Assert.AreEqual(0.0f, resultMatrix.M11, Delta, "Rotation M11 is incorrect.");
        Assert.AreEqual(1.0f, resultMatrix.M13, Delta, "Rotation M13 is incorrect.");
    }
}
