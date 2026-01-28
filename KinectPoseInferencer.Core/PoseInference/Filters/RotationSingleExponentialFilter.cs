using System.Numerics;

namespace KinectPoseInferencer.Core.PoseInference.Filters;

internal class RotationSingleExponentialFilter
{
    Quaternion? _prevResult = null;
    internal Quaternion? PrevResult
    {
        get => _prevResult;
        private set { _prevResult = value; }
    }

    internal Quaternion Apply(Quaternion current, float slerpAmount)
    {
        if (PrevResult is null)
        {
            PrevResult = current;
            return current;
        }

        var result = Quaternion.Slerp(PrevResult.Value, current, slerpAmount);
        PrevResult = result;
        return result;
    }
}
