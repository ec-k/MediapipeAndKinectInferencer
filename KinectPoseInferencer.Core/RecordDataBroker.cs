using K4AdotNet.Sensor;
using R3;

namespace KinectPoseInferencer.Core;

public class RecordDataBroker : IDisposable
{
    public ReadOnlyReactiveProperty<DeviceInputData> DeviceInputData => _deviceInputData;
    public ReadOnlyReactiveProperty<Capture> Capture => _capture;
    public ReadOnlyReactiveProperty<ImuSample> Imu => _imu;

    ReactiveProperty<DeviceInputData> _deviceInputData = new();
    ReactiveProperty<Capture> _capture = new();
    ReactiveProperty<ImuSample> _imu = new();

    /// <summary>
    /// Assign capture to the broker.
    /// </summary>
    /// <param name="capture"></param>
    public void SetCapture(Capture capture)
    {
        _capture.Value = capture;
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
        _capture.Dispose();
    }
}
