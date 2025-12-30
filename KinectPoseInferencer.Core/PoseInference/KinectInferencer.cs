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

    Tracker? _tracker;

    public void Configure(Calibration calibration)
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

    public bool TryEnqueueData(Capture capture)
    {
        if (capture is { DepthImage: not null, IRImage: not null })
        {
            _tracker?.EnqueueCapture(capture);
            return true;
        }
        else
            return false;
    }

    public BodyFrame? ProcessFrame()
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
