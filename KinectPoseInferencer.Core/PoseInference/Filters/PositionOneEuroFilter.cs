using System.Numerics;

namespace KinectPoseInferencer.Core.PoseInference.Filters;

/// <summary>
/// One Euro Filter implementation for Vector3 (position) smoothing.
/// Uses velocity magnitude for adaptive cutoff frequency calculation.
/// </summary>
internal class PositionOneEuroFilter
{
    readonly float _minCutoff;
    readonly float _slope;
    readonly float _dCutoff;

    float _prevTime;
    bool _isFirst = true;

    readonly PositionSingleExponentialFilter _positionFilter = new();
    readonly PositionSingleExponentialFilter _velocityFilter = new();

    internal Vector3? PrevResult => _positionFilter.PrevResult;

    internal PositionOneEuroFilter(float minCutoff, float slope, float dCutoff)
    {
        _minCutoff = minCutoff;
        _slope = slope;
        _dCutoff = dCutoff;
    }

    internal Vector3 Apply(Vector3 current, float timestamp)
    {
        var dt = timestamp - _prevTime;
        if (dt <= 1e-5f) return current;

        var updateRate = 1f / dt;
        var velocity = Vector3.Zero;

        if (_isFirst || _positionFilter.PrevResult is null)
        {
            _prevTime = timestamp;
            _isFirst = false;
        }
        else
        {
            // Calculate velocity
            velocity = (current - _positionFilter.PrevResult.Value) * updateRate;
        }

        // Smooth the velocity
        var smoothedVelocity = _velocityFilter.Apply(velocity, Alpha(updateRate, _dCutoff));

        // Calculate adaptive cutoff based on velocity magnitude
        var cutoff = _minCutoff + _slope * smoothedVelocity.Length();

        // Apply smoothing to position
        var result = _positionFilter.Apply(current, Alpha(updateRate, cutoff));

        _prevTime = timestamp;
        return result;
    }

    float Alpha(float updateRate, float cutoffFrequency)
    {
        var timeConstant = 1f / (2f * MathF.PI * cutoffFrequency);
        return 1f / (1f + timeConstant * updateRate);
    }
}
