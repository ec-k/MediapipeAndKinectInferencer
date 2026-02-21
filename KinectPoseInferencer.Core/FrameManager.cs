namespace KinectPoseInferencer.Core;

/// <summary>
/// Thread-safe manager for skeleton data.
/// Replaces the previous BodyFrame-based FrameManager.
/// </summary>
public class FrameManager
{
    private SkeletonData[]? _skeletons;
    private readonly object _lock = new();

    /// <summary>
    /// Set skeleton data. Thread-safe.
    /// </summary>
    public void SetSkeletons(SkeletonData[] skeletons)
    {
        lock (_lock)
        {
            _skeletons = skeletons;
        }
    }

    /// <summary>
    /// Take skeleton data. Returns null if no data available.
    /// </summary>
    public SkeletonData[]? TakeSkeletons()
    {
        lock (_lock)
        {
            var result = _skeletons;
            _skeletons = null;
            return result;
        }
    }
}
