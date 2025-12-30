using HumanLandmarks;
using System.Numerics;

namespace KinectPoseInferencer.Core.PoseInference.Filters;

internal static class LandmarkExtensions
{
    internal static Landmark Lerp(this Landmark value1, Landmark value2, float lerpAmount)
    {
        var clampedLerpAmount = float.Clamp(lerpAmount, 0f, 1f);

        var position   = Vector3.Lerp(value1.Position.ToVector3(), value2.Position.ToVector3(), clampedLerpAmount);
        var rotation   = Quaternion.Slerp(value1.Rotation.ToQuaternion(), value2.Rotation.ToQuaternion(), clampedLerpAmount);
        var confidence = float.Lerp(value1.Confidence, value2.Confidence, clampedLerpAmount);

        return new Landmark()
        {
            Position   = position.ToPosition(),
            Rotation   = rotation.ToRotation(),
            Confidence = confidence
        };
    }

    internal static Landmark Sub(this Landmark value1, Landmark value2)
        => new Landmark()
        {
            Position = (value1.Position.ToVector3() - value2.Position.ToVector3()).ToPosition(),
            Rotation = (Quaternion.Inverse(value2.Rotation.ToQuaternion()) * value1.Rotation.ToQuaternion()).ToRotation(),
            Confidence = value1.Confidence - value2.Confidence
        };

    internal static Position Multiply(this Position position, float num)
        => (position.ToVector3() * num).ToPosition();

    internal static Position ToPosition(this Vector3 position)
        => new Position()
        {
            X = position.X,
            Y = position.Y,
            Z = position.Z,
        };

    internal static Vector3 ToVector3(this Position position)
        => new Vector3(
            position.X,
            position.Y,
            position.Z
            );

    internal static Rotation ToRotation(this Quaternion rotation)
        => new Rotation()
        {
            X = rotation.X,
            Y = rotation.Y,
            Z = rotation.Z,
            W = rotation.W
        };

    internal static Quaternion ToQuaternion(this Rotation rotation)
        => new Quaternion(
            rotation.X,
            rotation.Y,
            rotation.Z,
            rotation.W
            );
}
