using System.Numerics;

namespace KinectPoseInferencer.Core.PoseInference.Filters;

internal class PositionSingleExponentialFilter
{
    Vector3? _prevResult = null;
    internal Vector3? PrevResult
    {
        get => _prevResult;
        private set { _prevResult = value; }
    }

    internal Vector3 Apply(Vector3 current, float lerpAmount)
    {
        if (PrevResult is null)
        {
            PrevResult = current;
            return current;
        }

        var result = Vector3.Lerp(PrevResult.Value, current, lerpAmount);
        PrevResult = result;
        return result;
    }
}
