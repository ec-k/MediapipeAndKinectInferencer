using HumanLandmarks;
using System.ComponentModel;
using System.Numerics;


namespace KinectPoseInferencer.Core.PoseInference.Filters;

internal class OneEuroFilter : ILandmarkFilter
{
    float _minCutoff;
    float _slope;
    float _dCutoff;
    float _prevTime;
    SingleExponentialSmoothingFilter _xFilter;
    SingleExponentialSmoothingFilter _dxFilter;

    bool _isFirst = true;

    internal OneEuroFilter(float minCutoff, float slope, float dCutoff)
    {
        _minCutoff = minCutoff;
        _slope = slope;
        _dCutoff = dCutoff;
        _xFilter = new();
        _dxFilter = new();
    }

    public Landmark Apply(in Landmark current, float timestamp)
    {
        var result = current.Clone();

        var dt = timestamp - _prevTime;
        if (dt <= 1e-5f) return result; // Do nothing if the time difference is too small.

        var updateRate = 1f / dt;
        var dx = new Landmark()
        {
            Position = Vector3.Zero.ToPosition(),
            Rotation = Quaternion.Identity.ToRotation(),
            Confidence = 1f
        };

        if (_isFirst || _xFilter.PrevResult is null)
        {
            _prevTime = timestamp;
            _isFirst = false;
        }
        else
        {
            dx = result.Sub(_xFilter.PrevResult);
            dx.Position = dx.Position!.Multiply(updateRate);
        }

        var edx = _dxFilter.Apply(dx, Alpha(updateRate, _dCutoff));
        var cutoff = _minCutoff + _slope * edx.Position.ToVector3().Magnitude();
        result = _xFilter.Apply(result, Alpha(updateRate, cutoff));

        _prevTime = timestamp;
        return result;
    }

    float Alpha(float updateRate, float cutoffFrequency)
    {
        var timeConstant = 1f / (2 * MathF.PI * cutoffFrequency);
        return 1f / (1f + timeConstant * updateRate);
    }
}
