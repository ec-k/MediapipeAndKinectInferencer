using HumanLandmarks;

namespace KinectPoseInferencer.Core.PoseInference.Filters;

internal class SingleExponentialSmoothingFilter
{
    Landmark? _prevResult = null;
    internal Landmark? PrevResult
    {
        get => _prevResult;
        private set { _prevResult = value?.Clone(); }
    }

    internal Landmark Apply(Landmark current, float lerpAmount)
    {
        if (PrevResult is null)
            PrevResult = current;
        else
            current = PrevResult.Lerp(current, lerpAmount);

        PrevResult = current;
        return current;
    }
}
