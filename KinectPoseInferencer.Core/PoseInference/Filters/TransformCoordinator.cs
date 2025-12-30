using HumanLandmarks;

namespace KinectPoseInferencer.Core.PoseInference.Filters;

public class TransformCoordinator: ILandmarkFilter
{
    public Landmark Apply(in Landmark landmark, float timestamp)
        => new Landmark
        {
            Position = new Position
            {
                X = -landmark.Position.X,
                Y = -landmark.Position.Y,
                Z = -landmark.Position.Z
            },
            Rotation = landmark.Rotation,
            Confidence = landmark.Confidence,
        };
}
