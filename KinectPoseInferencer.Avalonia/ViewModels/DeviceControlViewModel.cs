using CommunityToolkit.Mvvm.Messaging;
using KinectPoseInferencer.Avalonia.Messages;
using KinectPoseInferencer.Core;
using KinectPoseInferencer.Core.InputHook;
using KinectPoseInferencer.Renderers;
using R3;
using System;

namespace KinectPoseInferencer.Avalonia.ViewModels;

public class DeviceControlViewModel : ViewModelBase,
    IRecipient<OperationModeChangedMessage>,
    IRecipient<PlayPauseRequestedMessage>,
    IRecipient<SecondaryActionRequestedMessage>,
    IDisposable
{
    readonly KinectDeviceController _kinectDeviceController;
    readonly IMessenger _messenger;

    OperationMode _currentMode = OperationMode.Playback;
    DisposableBag _disposables = new();

    public DeviceControlViewModel(KinectDeviceController kinectDeviceController, IMessenger messenger)
    {
        _kinectDeviceController = kinectDeviceController;
        _messenger = messenger;
        _messenger.RegisterAll(this);

        _kinectDeviceController.IsReading
            .Subscribe(isReading =>
            {
                if (_currentMode == OperationMode.Device)
                    _messenger.Send(new PlaybackStateChangedMessage(isReading));
            })
            .AddTo(ref _disposables);
    }

    public void Receive(OperationModeChangedMessage message)
    {
        _currentMode = message.Mode;

        if (_currentMode == OperationMode.Device)
        {
            var isReading = _kinectDeviceController.IsReading.CurrentValue;
            _messenger.Send(new PlaybackStateChangedMessage(isReading));
        }
    }

    public void Receive(PlayPauseRequestedMessage message)
    {
        if (_currentMode != OperationMode.Device) return;
        DevicePlayOrPause();
    }

    public void Receive(SecondaryActionRequestedMessage message)
    {
        if (_currentMode != OperationMode.Device) return;
        DeviceStop();
    }

    void DeviceStop()
    {
        if (_kinectDeviceController.KinectDevice.CurrentValue is not null)
        {
            _kinectDeviceController.Pause();
            _kinectDeviceController.StopCamera();

            if (GlobalInputHook.IsHookActive)
            {
                GlobalInputHook.StopProcessingEvents();
                GlobalInputHook.StopHooks();
            }

            _messenger.Send(new MediaSourceClearedMessage());
        }
    }

    void DevicePlayOrPause()
    {
        if (_kinectDeviceController.KinectDevice.CurrentValue is null)
        {
            _kinectDeviceController.Open();

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

    public void Dispose()
    {
        _kinectDeviceController?.Dispose();
        _disposables.Dispose();

        if (GlobalInputHook.IsHookActive)
        {
            GlobalInputHook.StopProcessingEvents();
            GlobalInputHook.StopHooks();
        }
    }
}
