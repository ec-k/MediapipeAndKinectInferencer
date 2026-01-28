using System.Numerics;

namespace KinectPoseInferencer.Core.PoseInference.Filters;

/// <summary>
/// One Euro Filter implementation for Quaternion (rotation) smoothing.
/// Uses angular velocity for adaptive cutoff frequency calculation.
/// </summary>
internal class RotationOneEuroFilter
{
    readonly float _minCutoff;
    readonly float _slope;
    readonly float _dCutoff;

    float _prevTime;
    bool _isFirst = true;

    readonly RotationSingleExponentialFilter _rotationFilter = new();
    readonly ScalarSmoothingFilter _angularVelocityFilter = new();

    internal Quaternion? PrevResult => _rotationFilter.PrevResult;

    internal RotationOneEuroFilter(float minCutoff, float slope, float dCutoff)
    {
        _minCutoff = minCutoff;
        _slope = slope;
        _dCutoff = dCutoff;
    }

    internal Quaternion Apply(Quaternion current, float timestamp)
    {
        var dt = timestamp - _prevTime;
        if (dt <= 1e-5f) return current;

        var updateRate = 1f / dt;
        var angularVelocity = 0f;

        if (_isFirst || _rotationFilter.PrevResult is null)
        {
            _prevTime = timestamp;
            _isFirst = false;
        }
        else
            angularVelocity = CalculateAngularVelocity(_rotationFilter.PrevResult.Value, current, dt);

        var smoothedAngularVelocity = _angularVelocityFilter.Apply(angularVelocity, Alpha(updateRate, _dCutoff));
        var cutoff = _minCutoff + _slope * Math.Abs(smoothedAngularVelocity);

        // Apply smoothing to rotation
        var result = _rotationFilter.Apply(current, Alpha(updateRate, cutoff));

        _prevTime = timestamp;
        return result;
    }

    /// <summary>
    /// Calculate angular velocity between two quaternions.
    /// Returns the angular speed in radians per second.
    /// </summary>
    static float CalculateAngularVelocity(Quaternion prev, Quaternion current, float dt)
    {
        // Ensure we take the shortest path
        var dot = Quaternion.Dot(prev, current);
        if (dot < 0)
        {
            current = Quaternion.Negate(current);
            dot = -dot;
        }

        // Clamp dot product to valid range for acos
        dot = Math.Clamp(dot, -1f, 1f);

        // Calculate angle between quaternions (full rotation angle)
        var angle = 2f * MathF.Acos(dot);

        // Return angular velocity (radians per second)
        return angle / dt;
    }

    float Alpha(float updateRate, float cutoffFrequency)
    {
        var timeConstant = 1f / (2f * MathF.PI * cutoffFrequency);
        return 1f / (1f + timeConstant * updateRate);
    }
}

/// <summary>
/// Simple exponential smoothing filter for scalar values.
/// </summary>
internal class ScalarSmoothingFilter
{
    float? _prevResult = null;
    internal float? PrevResult => _prevResult;

    internal float Apply(float current, float lerpAmount)
    {
        if (_prevResult is null)
        {
            _prevResult = current;
            return current;
        }

        var result = float.Lerp(_prevResult.Value, current, lerpAmount);
        _prevResult = result;
        return result;
    }
}
