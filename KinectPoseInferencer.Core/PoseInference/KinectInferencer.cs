using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using R3;

namespace KinectPoseInferencer.Core.PoseInference;

public record KinectInferenceResult(
    Skeleton Skeleton,
    float Timestamp
    );

public class KinectInferencer
{
    public ReadOnlyReactiveProperty<KinectInferenceResult> Result => _result;
    ReactiveProperty<KinectInferenceResult> _result = new(new(new Skeleton(), 0f));

    readonly object _lock = new();
    Tracker? _tracker;

    public void Configure(Calibration calibration)
    {
        lock (_lock)
        {
            // Initialize the tracker
            _tracker?.Dispose(); // Prevent duplicated initialization.
            var trackerConfig = new TrackerConfiguration()
            {
                SensorOrientation = SensorOrientation.Default,
                ProcessingMode = TrackerProcessingMode.Gpu,
            };
            _tracker = new(calibration, trackerConfig);
        }
    }

    public bool TryEnqueueData(Capture capture)
    {
        if (capture is not { DepthImage: not null, IRImage: not null })
            return false;

        lock (_lock)
        {
            if (_tracker is null) return false;
            // EnqueueCapture is asynchronous. Pass a duplicated reference so the SDK
            // can manage its own copy and the caller can safely dispose the original.
            _tracker.EnqueueCapture(capture.DuplicateReference());
            return true;
        }
    }

    public BodyFrame? ProcessFrame()
    {
        lock (_lock)
        {
            if (_tracker is null) return null;

            if (!_tracker.TryPopResult(out var frame))
                return null;

            using (frame)
            {
                var result = Inference(frame);
                if (result is not null)
                    _result.Value = result;
                return frame.DuplicateReference();
            }
        }
    }

    KinectInferenceResult? Inference(BodyFrame frame)
    {
        if (frame.BodyCount > 0)
        {
            var timestamp = (float)frame.DeviceTimestamp.TotalSeconds;
            frame.GetBodySkeleton(0, out var nullableLandmark);

            if(nullableLandmark is Skeleton skeleton)
                return new(skeleton, timestamp);
        }
        return null;
    }
}
