using System.Numerics;
using HumanLandmarks;
using KinectLandmarkIndex = HumanLandmarks.KinectPoseLandmarks.Types.LandmarkIndex;

namespace KinectPoseInferencer.Core.PoseInference.Filters;

/// <summary>
/// Converts Kinect skeleton rotations to Unity Humanoid-compatible rotations
/// by applying joint basis corrections.
/// Based on Azure Kinect Sample: PuppetAvatar.cs
/// </summary>
public class JointBasisCorrector : ILandmarkFilter
{
    readonly Quaternion _basisInverse;

    public JointBasisCorrector(int jointIndex)
    {
        var basis = GetJointBasis((KinectLandmarkIndex)jointIndex);
        _basisInverse = Quaternion.Inverse(basis);
    }

    public Landmark Apply(in Landmark landmark, float timestamp)
    {
        if (landmark.Rotation is null or { X: 0, Y: 0, Z: 0, W: 0 })
            return landmark;

        var input = landmark.Rotation.ToQuaternion();
        var result = input * _basisInverse;

        return new Landmark
        {
            Position = landmark.Position,
            Rotation = result.ToRotation(),
            Confidence = landmark.Confidence
        };
    }

    static Quaternion GetJointBasis(KinectLandmarkIndex index)
    {
        return index switch
        {
            // Spine and head
            KinectLandmarkIndex.Pelvis or
            KinectLandmarkIndex.SpineNaval or
            KinectLandmarkIndex.SpineChest or
            KinectLandmarkIndex.Neck or
            KinectLandmarkIndex.Head or
            KinectLandmarkIndex.Nose or
            KinectLandmarkIndex.EyeLeft or
            KinectLandmarkIndex.EarLeft or
            KinectLandmarkIndex.EyeRight or
            KinectLandmarkIndex.EarRight
                => LookRotation(Vector3.UnitY, Vector3.UnitX),

            // Left arm (clavicle, shoulder, elbow) and thumb
            KinectLandmarkIndex.ClavicleLeft or
            KinectLandmarkIndex.ShoulderLeft or
            KinectLandmarkIndex.ElbowLeft or
            KinectLandmarkIndex.ThumbLeft
                => LookRotation(Vector3.UnitY, -Vector3.UnitZ),

            // Left hand
            KinectLandmarkIndex.WristLeft or
            KinectLandmarkIndex.HandLeft or
            KinectLandmarkIndex.HandtipLeft
                => LookRotation(Vector3.UnitZ, Vector3.UnitY),

            // Right arm (clavicle, shoulder, elbow) and thumb
            KinectLandmarkIndex.ClavicleRight or
            KinectLandmarkIndex.ShoulderRight or
            KinectLandmarkIndex.ElbowRight or
            KinectLandmarkIndex.ThumbRight
                => LookRotation(-Vector3.UnitY, Vector3.UnitZ),

            // Right hand
            KinectLandmarkIndex.WristRight or
            KinectLandmarkIndex.HandRight or
            KinectLandmarkIndex.HandtipRight
                => LookRotation(-Vector3.UnitZ, -Vector3.UnitY),

            // Left leg (hip, knee, ankle, foot)
            KinectLandmarkIndex.HipLeft or
            KinectLandmarkIndex.KneeLeft or
            KinectLandmarkIndex.AnkleLeft or
            KinectLandmarkIndex.FootLeft
                => LookRotation(Vector3.UnitY, Vector3.UnitX),

            // Right leg (hip, knee, ankle, foot)
            KinectLandmarkIndex.HipRight or
            KinectLandmarkIndex.KneeRight or
            KinectLandmarkIndex.AnkleRight or
            KinectLandmarkIndex.FootRight
                => LookRotation(-Vector3.UnitY, -Vector3.UnitX),

            // Default to identity for unknown joints
            _ => Quaternion.Identity
        };
    }

    /// <summary>
    /// Computes quaternion equivalent to Unity's Quaternion.LookRotation(forward, up)
    /// </summary>
    static Quaternion LookRotation(Vector3 forward, Vector3 up)
    {
        forward = Vector3.Normalize(forward);
        var right = Vector3.Normalize(Vector3.Cross(up, forward));
        up = Vector3.Cross(forward, right);

        // Build rotation matrix and convert to quaternion
        var matrix = new Matrix4x4(
            right.X, up.X, forward.X, 0,
            right.Y, up.Y, forward.Y, 0,
            right.Z, up.Z, forward.Z, 0,
            0, 0, 0, 1);

        return Quaternion.CreateFromRotationMatrix(matrix);
    }
}
