using HelixToolkit.Wpf;
using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Renderers;


namespace KinectPoseInferencer.Tests;

internal static class BodyTrackingTestHelper
{
    public static Skeleton CreateDummySkeleton()
    {
        var skeleton = new Skeleton();
        var jointTypes = Enum.GetValues(typeof(JointType));

        for (int i = 0; i < jointTypes.Length; i++)
        {
            var joint = new Joint
            {
                PositionMm = new K4AdotNet.Float3(1000, 1000, 1000),
                Orientation = K4AdotNet.Quaternion.Identity,
                ConfidenceLevel = JointConfidenceLevel.High
            };
            skeleton[i] = joint;
        }
        return skeleton;
    }

    public static int GetExpectedBoneCount()
    {
        var jointTypes = Enum.GetValues(typeof(JointType)).Cast<JointType>();
        int boneCount = 0;
        foreach (var jointType in jointTypes)
        {
            if (jointType != JointType.Pelvis)
            {
                boneCount++;
            }
        }
        return boneCount;
    }
}

[TestClass]
public class PlayerVisualizerTests
{
    const int ExpectedJointCount = 32;
    readonly int ExpectedBoneCount = BodyTrackingTestHelper.GetExpectedBoneCount();

    Calibration _dummyCalibration;

    [TestInitialize]
    public void TestInitialize()
    {
        _dummyCalibration = new();
    }

    [TestMethod]
    public void UpdateVisuals_OneBody_GeneratesCorrectNumberOfModels()
    {
        // Arrange
        var dummySkeleton = BodyTrackingTestHelper.CreateDummySkeleton();
        var expectedModelCount = ExpectedJointCount + ExpectedBoneCount;
        using var visualizer = new PlayerVisualizer(_dummyCalibration);

        // Act
        // TODO: Create a mock of BodyTrackingFrame
        BodyFrame? dummyFrame = null;
        Image? dummyDepthImage = null;

        var models = visualizer.UpdateVisuals(dummyFrame, dummyDepthImage);

        // Assert
        Assert.AreEqual(0, models.Count, "The frame must not be null.");
        Assert.IsTrue(models.Any(), "No models are created");
    }

    [TestMethod]
    public void UpdateVisuals_WithFrame_GeneratesPointCloudModel()
    {
        using var visualizer = new PlayerVisualizer(_dummyCalibration);

        BodyFrame? dummyFrame = null;
        Image? dummyDepthImage = null;
        var models = visualizer.UpdateVisuals(dummyFrame, dummyDepthImage);

        Assert.IsNotNull(models);
        Assert.IsFalse(models.Any(m => m is PointsVisual3D), "PointCloud model is generated while a null image is fed.");
        Assert.IsTrue(models.Any(m => m is PointsVisual3D), "PointsVisual3D models are not contained in the list.");
    }
}
