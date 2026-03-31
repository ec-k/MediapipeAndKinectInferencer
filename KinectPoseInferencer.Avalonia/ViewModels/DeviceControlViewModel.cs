using KinectPoseInferencer.Core.InputHook;
using KinectPoseInferencer.Renderers;

namespace KinectPoseInferencer.Avalonia.ViewModels;

public partial class MainWindowViewModel
{
    void DeviceStop()
    {
        if (_kinectDeviceController.KinectDevice.CurrentValue is not null)
        {
            _kinectDeviceController.Pause();
            _kinectDeviceController.Close();

            if (GlobalInputHook.IsHookActive)
            {
                GlobalInputHook.StopProcessingEvents();
                GlobalInputHook.StopHooks();
            }
        }
    }

    void DevicePlayOrPause()
    {
        if (_kinectDeviceController.KinectDevice.CurrentValue is null)
        {
            _kinectDeviceController.Open();

            // Setup visualization
            if (_kinectDeviceController?.KinectDevice is null) return;

            var calibration = _kinectDeviceController.GetCalibration();
            if (calibration.HasValue)
            {
                PointCloud.ComputePointCloudCache(calibration.Value);
            }

            _kinectDeviceController.StartCamera();

            if (!GlobalInputHook.IsHookActive)
            {
                GlobalInputHook.StartHooks();
                GlobalInputHook.StartProcessingEvents();
            }
        }
        else
        {
            if (_kinectDeviceController.IsReading.CurrentValue)
                _kinectDeviceController.Pause();
            else
                _kinectDeviceController.Play();


            if (!GlobalInputHook.IsHookActive)
            {
                GlobalInputHook.StartHooks();
                GlobalInputHook.StartProcessingEvents();
            }
            else
            {
                GlobalInputHook.StopProcessingEvents();
                GlobalInputHook.StopHooks();
            }
        }
    }
}
