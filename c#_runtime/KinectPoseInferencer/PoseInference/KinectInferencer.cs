using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using R3;

namespace KinectPoseInferencer.PoseInference;

public class KinectInferencer
{
    public ReadOnlyReactiveProperty<Skeleton> Result => _result;
    ReactiveProperty<Skeleton> _result = new();

    Tracker _tracker;

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

    public void EnqueueData(Capture capture)
    {
        _tracker.EnqueueCapture(capture);
    }

    public BodyFrame ProcessFrame()
    {
        using var frame = _tracker.PopResult();
        var nullableLandmark = Inference(frame);
        if(nullableLandmark is Skeleton landmark)
        {
            _result.Value = landmark;
        }
        return frame.DuplicateReference();
    }

    Skeleton? Inference(BodyFrame frame)
    {
        if (frame.BodyCount > 0)
        {
            Skeleton skeleton;
            frame.GetBodySkeleton(0, out skeleton);

            return skeleton;
        }
        return null;
    }
}
