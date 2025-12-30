using HumanLandmarks;
using System.Numerics;


namespace KinectPoseInferencer.Core.PoseInference.Filters;

internal class OneEuroFilter : ILandmarkFilter
{
    float _minCutoff;
    float _slope;
    float _dCutoff;
    float _updateRate = 1f;
    SingleExponentialSmoothingFilter _xFilter;
    SingleExponentialSmoothingFilter _dxFilter;

    bool _isFirst = true;

    internal OneEuroFilter(float minCutoff, float slope, float dCutoff, float frameRate)
    {
        _minCutoff = minCutoff;
        _slope = slope;
        _dCutoff = dCutoff;
        _updateRate = frameRate;
        _xFilter = new();
        _dxFilter = new();
    }

    public Landmark Apply(in Landmark current)
    {
        var result = current;

        var dx = new Landmark()
        {
            Position = Vector3.Zero.ToPosition(),
            Rotation = Quaternion.Identity.ToRotation(),
            Confidence = 1f
        };

        if (_isFirst || _xFilter.PrevResult is null)
            _isFirst = false;
        else
        {
            dx = result.Sub(_xFilter.PrevResult);
            dx.Position = dx.Position!.Multiply(_updateRate);
        }

        var edx = _dxFilter.Apply(dx, Alpha(_updateRate, _dCutoff));
        var cutoff = _minCutoff + _slope * edx.Position.ToVector3().Magnitude();
        result = _xFilter.Apply(result, Alpha(_updateRate, cutoff));

        return result;
    }

    float Alpha(float updateRate, float cutoffFrequency)
    {
        var timeConstant = 1f / (2 * MathF.PI * cutoffFrequency);
        return 1f / (1f + timeConstant * updateRate);
    }
}
