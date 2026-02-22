using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using R3;
using K4ATimeout = K4AdotNet.Timeout;

namespace KinectPoseInferencer.Core.PoseInference;

public record KinectInferenceResult(
    SkeletonData[] Bodies,
    float Timestamp
    )
{
    /// <summary>
    /// First skeleton for backward compatibility.
    /// </summary>
    public Skeleton Skeleton => Bodies.Length > 0 ? Bodies[0].Skeleton : default;
};

/// <summary>
/// Kinect body tracking inferencer - single thread model.
/// Based on K4AdotNet.Samples.Console.BodyTrackingSpeed.SingleThreadProcessor pattern.
/// Call TryEnqueueData then TryProcessFrame from the same thread.
/// IMPORTANT: Tracker must be created and used on the same persistent thread (not ThreadPool).
/// </summary>
public class KinectInferencer : IDisposable
{
    public ReadOnlyReactiveProperty<KinectInferenceResult?> Result => _result;

    readonly ReactiveProperty<KinectInferenceResult?> _result = new();

    Tracker? _tracker;
    Calibration? _pendingCalibration;
    Calibration? _currentCalibration;
    bool _needsInitialization = false;
    TaskCompletionSource? _initializationTcs;

    public int QueueSize => _tracker?.QueueSize ?? 0;
    public bool IsInitialized => _tracker is not null;

    /// <summary>
    /// Store calibration for later initialization.
    /// Call this from any thread - Tracker will be created on first EnsureInitialized call.
    /// </summary>
    public void SetCalibration(Calibration calibration)
    {
        _pendingCalibration = calibration;
        _needsInitialization = true;
        _initializationTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Wait for Tracker initialization to complete.
    /// Call this after SetCalibration and after starting the thread that calls EnsureInitialized.
    /// </summary>
    public Task WaitForInitializationAsync()
    {
        return _initializationTcs?.Task ?? Task.CompletedTask;
    }

    /// <summary>
    /// Initialize Tracker on the calling thread if needed.
    /// Call this from the thread where you want Tracker operations to run.
    /// IMPORTANT: Must be called from a persistent thread (not ThreadPool) due to CUDA context affinity.
    /// </summary>
    public void EnsureInitialized()
    {
        if (!_needsInitialization || _pendingCalibration is not Calibration calibration)
            return;

        _tracker?.Dispose();
        _currentCalibration = calibration;

        var trackerConfig = new TrackerConfiguration()
        {
            SensorOrientation = SensorOrientation.Default,
            ProcessingMode = TrackerProcessingMode.Gpu,
        };
        _tracker = new Tracker(calibration, trackerConfig);
        _needsInitialization = false;

        _initializationTcs?.TrySetResult();
    }

    /// <summary>
    /// Configure and immediately create Tracker.
    /// WARNING: This creates Tracker on the calling thread.
    /// Prefer SetCalibration + EnsureInitialized for thread control.
    /// </summary>
    [Obsolete("Use SetCalibration + EnsureInitialized for proper thread control")]
    public void Configure(Calibration calibration)
    {
        SetCalibration(calibration);
        EnsureInitialized();
    }

    /// <summary>
    /// Mark tracker for reset. Actual reset happens on next EnsureInitialized call.
    /// This ensures Tracker disposal and creation happen on the same thread.
    /// </summary>
    public void Reset()
    {
        if (_currentCalibration is not Calibration calibration) return;

        _pendingCalibration = calibration;
        _needsInitialization = true;
    }

    /// <summary>
    /// Enqueue capture for body tracking.
    /// Caller can dispose the capture immediately after this call.
    /// </summary>
    public bool TryEnqueueData(Capture capture)
    {
        if (capture is null || capture.IsDisposed)
            return false;

        if (capture.DepthImage is null || capture.IRImage is null)
            return false;

        if (_tracker is null) return false;

        return _tracker.TryEnqueueCapture(capture, K4ATimeout.Infinite);
    }

    /// <summary>
    /// Try to pop and process a result frame. Call this after TryEnqueueData.
    /// </summary>
    /// <param name="wait">If true, wait for result. If false, return immediately if no result.</param>
    /// <returns>True if a frame was processed.</returns>
    public bool TryProcessFrame(bool wait = false)
    {
        if (_tracker is null) return false;

        var timeout = wait ? K4ATimeout.Infinite : K4ATimeout.NoWait;
        if (!_tracker.TryPopResult(out var frame, timeout))
            return false;

        using (frame)
        {
            ProcessFrame(frame);
        }

        return true;
    }

    /// <summary>
    /// Process remaining frames in the queue until empty.
    /// Call this at EOF or before Reset.
    /// </summary>
    public void ProcessQueueTail()
    {
        while (QueueSize > 0)
        {
            TryProcessFrame(wait: true);
        }
    }

    void ProcessFrame(BodyFrame frame)
    {
        if (frame.BodyCount > 0)
        {
            var timestamp = (float)frame.DeviceTimestamp.TotalSeconds;
            var bodies = new SkeletonData[frame.BodyCount];

            for (int i = 0; i < frame.BodyCount; i++)
            {
                frame.GetBodySkeleton(i, out var skeleton);
                var bodyId = frame.GetBodyId(i);
                bodies[i] = new SkeletonData(skeleton, bodyId.Value);
            }

            _result.Value = new KinectInferenceResult(bodies, timestamp);
        }
    }

    public void Dispose()
    {
        _tracker?.Dispose();
        _tracker = null;

        _result.Dispose();
    }
}
