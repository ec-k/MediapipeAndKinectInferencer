using HumanLandmarks;
using System.Numerics;


namespace KinectPoseInferencer.Core.PoseInference.Filters;

internal class OneEuroFilter : ILandmarkFilter
{
    readonly PositionOneEuroFilter _positionFilter;
    readonly RotationOneEuroFilter _rotationFilter;

    internal OneEuroFilter(float minCutoff, float slope, float dCutoff)
        : this(minCutoff, slope, dCutoff, minCutoff, slope, dCutoff)
    {
    }

    internal OneEuroFilter(
        float positionMinCutoff, float positionSlope, float positionDCutoff,
        float rotationMinCutoff, float rotationSlope, float rotationDCutoff)
    {
        _positionFilter = new PositionOneEuroFilter(positionMinCutoff, positionSlope, positionDCutoff);
        _rotationFilter = new RotationOneEuroFilter(rotationMinCutoff, rotationSlope, rotationDCutoff);
    }

    public Landmark Apply(in Landmark current, float timestamp)
    {
        var filteredPosition = _positionFilter.Apply(current.Position.ToVector3(), timestamp);
        var filteredRotation = _rotationFilter.Apply(current.Rotation.ToQuaternion(), timestamp);

        return new Landmark
        {
            Position = filteredPosition.ToPosition(),
            Rotation = filteredRotation.ToRotation(),
            Confidence = current.Confidence
        };
    }
}
