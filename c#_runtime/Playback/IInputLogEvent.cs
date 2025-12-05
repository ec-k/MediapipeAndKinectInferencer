namespace KinectPoseInferencer.Playback;

// Common interface for input events to allow sorting and generic handling
public interface IInputLogEvent
{
    long RawStopwatchTimestamp { get; }
}
