using HumanLandmarks;
using System.Numerics;

namespace KinectPoseInferencer.Core.PoseInference.Filters;

public class TransformCoordinator: ILandmarkFilter
{
    static readonly Quaternion _upsideDownCorrection
        = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)Math.PI);
    static readonly Quaternion _frontsideBackCorrection
        = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Math.PI);

    public Landmark Apply(in Landmark landmark, float timestamp)
    {
        var rotationConverted = new Rotation
        {
            X = landmark.Rotation.X,
            Y = -landmark.Rotation.Y,
            Z = -landmark.Rotation.Z,
            W = landmark.Rotation.W
        }.ToQuaternion();
        rotationConverted = _frontsideBackCorrection * _upsideDownCorrection * rotationConverted;

        return new Landmark
           {
               Position = new Position
               {
                   X = -landmark.Position.X,
                   Y = -landmark.Position.Y,
                   Z = -landmark.Position.Z
               },
               // Negate the imaginary part of the quaternion to reflect the axis inversion
               Rotation = rotationConverted.ToRotation(),
               Confidence = landmark.Confidence,
           };
    }
}