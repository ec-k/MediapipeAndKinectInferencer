using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using System;
using KinectPoseInferencer.Playback;

namespace KinectPoseInferencer;

public class FrameCaptureBroker : IDisposable
{
    public event Action<Capture, BodyFrame> OnNewFrameReady;
    public event Action<IInputLogEvent>? OnNewInputLogEvent;

    Capture _currentCapture;
    BodyFrame _currentBodyFrame;

    public void ProcessNewFrame(Capture capture, BodyFrame bodyFrame)
    {
        // Dispose existing frames if any
        _currentCapture?.Dispose();
        _currentBodyFrame?.Dispose();

        // Hold onto the new frames
        // It's the responsibility of the caller (PlaybackReader) to ensure these are DuplicateReference() if needed
        // for internal processing after passing them to the broker.
        _currentCapture = capture;
        _currentBodyFrame = bodyFrame;

        // Notify subscribers that a new frame pair is ready
        OnNewFrameReady?.Invoke(_currentCapture, _currentBodyFrame);
    }

    public void ProcessNewInputLogEvent(IInputLogEvent inputLogEvent) // Add InputLogEvent processing
    {
        OnNewInputLogEvent?.Invoke(inputLogEvent);
    }

    /// <summary>
    /// Provides a DuplicateReference of the current Capture to the consumer.
    /// The consumer is responsible for disposing the returned Capture.
    /// </summary>
    public Capture GetCurrentCaptureDuplicate()
    {
        return _currentCapture?.DuplicateReference();
    }

    /// <summary>
    /// Provides a DuplicateReference of the current BodyFrame to the consumer.
    /// The consumer is responsible for disposing the returned BodyFrame.
    /// </summary>
    public BodyFrame GetCurrentBodyFrameDuplicate()
    {
        return _currentBodyFrame?.DuplicateReference();
    }

    public void Dispose()
    {
        _currentCapture?.Dispose();
        _currentBodyFrame?.Dispose();
        _currentCapture = null;
        _currentBodyFrame = null;
    }
}
