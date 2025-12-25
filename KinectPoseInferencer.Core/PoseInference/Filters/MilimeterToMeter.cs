using HumanLandmarks;

namespace KinectPoseInferencer.Core.PoseInference.Filters;

public class MilimeterToMeter: ILandmarkFilter
{
    /// <summary>
    /// // Convert position from millimeters to meters
    /// </summary>
    /// <param name="landmark"></param>
    /// <returns></returns>
    public Landmark Apply(in Landmark landmark)
        => new Landmark
        {
            Position = new Position
            {
                X = landmark.Position.X / 1000f,
                Y = landmark.Position.Y / 1000f,
                Z = landmark.Position.Z / 1000f,
            },
            Rotation = landmark.Rotation,
            Confidence = landmark.Confidence
        };
}
