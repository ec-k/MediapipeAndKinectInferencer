using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using System;
using R3;

namespace KinectPoseInferencer;

public class RecordDataBroker : IDisposable
{
    public ReadOnlyReactiveProperty<DeviceInputData> InputEvents => _inputEvents;
    public ReadOnlyReactiveProperty<Capture> Capture => _capture;
    public ReadOnlyReactiveProperty<BodyFrame> Frame => _frame;
    public ReadOnlyReactiveProperty<ImuSample> Imu => _imu;

    ReactiveProperty<DeviceInputData> _inputEvents = new();
    ReactiveProperty<Capture> _capture = new();
    ReactiveProperty<BodyFrame> _frame = new();
    ReactiveProperty<ImuSample> _imu = new();

    /// <summary>
    /// Assign duplicated reference of the given capture to the broker.
    /// </summary>
    /// <param name="capture"></param>
    public void UpdateCapture(Capture capture)
    {
        // Dispose existing capture if any
        _capture.CurrentValue?.Dispose();
        _capture.Value = capture.DuplicateReference();
    }

    /// <summary>
    /// Assign duplicated reference of the given body frame to the broker.
    /// </summary>
    /// <param name="bodyFrame"></param>
    public void UpdateBodyFrame(BodyFrame bodyFrame)
    {
        // Dispose existing body frame if any
        _frame.CurrentValue?.Dispose();
        _frame.Value = bodyFrame.DuplicateReference();
    }

    public void UpdateImu(ImuSample imuSample)
    {
        _imu.Value = imuSample;
    }

    public void PushInputLogEvent(DeviceInputData inputEvent)
    {
        _inputEvents.Value = inputEvent;
    }

    public void Dispose()
    {
        _capture?.Dispose();
        _frame?.Dispose();
    }
}
