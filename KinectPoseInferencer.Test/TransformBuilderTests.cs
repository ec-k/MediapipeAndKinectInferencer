using KinectPoseInferencer.Renderers;
using System.Numerics;
using System.Windows.Media.Media3D;


namespace KinectAndInputRecorder.Tests;


[TestClass]
public class TransformBuilderTests
{
    static readonly float delta = 0.0001f;

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

        Assert.AreEqual(0.01f, modelMatrix.M11, delta, "X Scale is incorrect.");
        Assert.AreEqual(0.01f, modelMatrix.M22, delta, "Y Scale is incorrect.");
        Assert.AreEqual(1.0f,  modelMatrix.M33, delta, "Z Scale (length) is incorrect.");
        Assert.AreEqual(0.5f,  modelMatrix.M43, delta, "Z Translation is incorrect.");
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
        // 1. about translation
        Assert.AreEqual(0.0f, modelMatrix.M41, delta, "X Translation is incorrect.");
        Assert.AreEqual(0.0f, modelMatrix.M42, delta, "Y Translation is incorrect.");
        Assert.AreEqual(0.0f, modelMatrix.M43, delta, "Z Translation is incorrect.");

        // 2. about scale
        var xScale = new Vector3(modelMatrix.M11, modelMatrix.M21, modelMatrix.M31);
        var yScale = new Vector3(modelMatrix.M12, modelMatrix.M22, modelMatrix.M32);
        var zScale = new Vector3(modelMatrix.M13, modelMatrix.M23, modelMatrix.M33);

        Assert.AreEqual(0.01f, xScale.Length(), delta, "X Scale is incorrect.");
        Assert.AreEqual(0.01f, yScale.Length(), delta, "Y Scale is incorrect.");
        Assert.AreEqual(2.0f , zScale.Length(), delta, "Z Scale (length) is incorrect.");
    }

    [TestMethod]
    public void Matrix4x4ToTransform3D_ReturnsCorrectTransform()
    {
        // Arrange:
        var rotation = Matrix4x4.CreateRotationY(MathF.PI / 2.0f);
        var translation = Matrix4x4.CreateTranslation(10, 20, 30);
        var sourceMatrix =  rotation * translation;

        // Act
        var transform = Transform3DBuilder.CreateTransform(sourceMatrix);

        // Assert
        Assert.IsNotNull(transform);
        Assert.IsInstanceOfType(transform, typeof(MatrixTransform3D));

        var resultMatrix = transform.Matrix;

        Assert.AreEqual(sourceMatrix.M41, resultMatrix.OffsetX, delta, "Translation X is incorrect.");
        Assert.AreEqual(sourceMatrix.M42, resultMatrix.OffsetY, delta, "Translation Y is incorrect.");
        Assert.AreEqual(sourceMatrix.M43, resultMatrix.OffsetZ, delta, "Translation Z is incorrect.");

        Assert.AreEqual(0.0f, resultMatrix.M11, delta, "Rotation M11 is incorrect.");
        Assert.AreEqual(1.0f, resultMatrix.M13, delta, "Rotation M13 is incorrect.");
    }
}
