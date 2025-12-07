using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using System;
using KinectPoseInferencer.Playback;
using R3;

namespace KinectPoseInferencer;

public class RecordDataBroker : IDisposable
{
    public event Action<IInputLogEvent>? OnNewInputLogEvent;

    public ReadOnlyReactiveProperty<Capture> Capture => _capture;
    public ReadOnlyReactiveProperty<BodyFrame> Frame => _frame;

    ReactiveProperty<Capture> _capture = new();
    ReactiveProperty<BodyFrame> _frame = new();

    /// <summary>
    /// Assign duplicated reference of the given capture to the broker.
    /// </summary>
    /// <param name="capture"></param>
    public void UpdateCapture(Capture capture)
    {
        // Dispose existing capture if any
        _capture?.CurrentValue?.Dispose();
        _capture.Value = capture.DuplicateReference();
    }

    /// <summary>
    /// Assign duplicated reference of the given body frame to the broker.
    /// </summary>
    /// <param name="bodyFrame"></param>
    public void UpdateBodyFrame(BodyFrame bodyFrame)
    {
        // Dispose existing body frame if any
        _frame?.CurrentValue?.Dispose();
        _frame.Value = bodyFrame.DuplicateReference();
    }

    public void ProcessNewInputLogEvent(IInputLogEvent inputLogEvent)
    {
        OnNewInputLogEvent?.Invoke(inputLogEvent);
    }

    public void Dispose()
    {
        _capture?.Dispose();
        _frame?.Dispose();
        _capture = null;
        _frame = null;
    }
}
