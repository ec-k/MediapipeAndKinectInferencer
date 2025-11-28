using K4AdotNet.BodyTracking;
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

    [TestMethod]
    public void UpdateVisuals_OneBody_GeneratesCorrectNumberOfModels()
    {
        // Arrange
        var dummySkeleton = BodyTrackingTestHelper.CreateDummySkeleton();
        var visualizer = new PlayerVisualizer();

        int expectedModelCount = ExpectedJointCount + ExpectedBoneCount;

        // Act
        // TODO: Create a mock of BodyTrackingFrame
        var dummyFrame = new object();

        var models = visualizer.UpdateVisuals(null);

        // Assert
        Assert.IsTrue(models.Any(), "No models are created");
    }
}
