using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using R3;

namespace KinectPoseInferencer.Core;

public class RecordDataBroker : IDisposable
{
    public ReadOnlyReactiveProperty<DeviceInputData> DeviceInputData => _deviceInputData;
    public ReadOnlyReactiveProperty<Capture> Capture => _capture;
    public ReadOnlyReactiveProperty<BodyFrame> Frame => _frame;
    public ReadOnlyReactiveProperty<ImuSample> Imu => _imu;

    ReactiveProperty<DeviceInputData> _deviceInputData = new();
    ReactiveProperty<Capture> _capture = new();
    ReactiveProperty<BodyFrame> _frame = new();
    ReactiveProperty<ImuSample> _imu = new();

    /// <summary>
    /// Assign duplicated reference of the given capture to the broker.
    /// </summary>
    /// <param name="capture"></param>
    public void SetCapture(Capture capture)
    {
        // Dispose existing capture if any
        _capture.CurrentValue?.Dispose();
        _capture.Value = capture.DuplicateReference();
    }

    /// <summary>
    /// Assign duplicated reference of the given body frame to the broker.
    /// </summary>
    /// <param name="bodyFrame"></param>
    public void SetBodyFrame(BodyFrame bodyFrame)
    {
        // Dispose existing body frame if any
        _frame.CurrentValue?.Dispose();
        _frame.Value = bodyFrame.DuplicateReference();
    }

    public void SetImu(ImuSample imuSample)
    {
        _imu.Value = imuSample;
    }
        
    public void SetDeviceInputData(DeviceInputData inputEvent)
    {
        _deviceInputData.Value = inputEvent;
    }

    public void Dispose()
    {
        _capture.Value?.Dispose();
        _frame.Value?.Dispose();

        _capture.Dispose();
        _frame.Dispose();
    }
}
