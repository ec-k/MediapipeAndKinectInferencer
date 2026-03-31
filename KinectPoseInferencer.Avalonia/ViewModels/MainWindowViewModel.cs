using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using KinectPoseInferencer.Avalonia.Messages;
using KinectPoseInferencer.Core.PoseInference;
using R3;
using System;

namespace KinectPoseInferencer.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    readonly IMessenger _messenger;

    [ObservableProperty] OperationMode _selectedMode = OperationMode.Playback;
    [ObservableProperty] bool _isPlaybackMode = true;
    [ObservableProperty] bool _isKinectInferenceEnabled = true;

    DisposableBag _disposables = new();

    public DisplayViewModel Display { get; }
    public PlaybackControlViewModel PlaybackControl { get; }
    public DeviceControlViewModel DeviceControl { get; }
    public MediaControlViewModel MediaControl { get; }

    public MainWindowViewModel(
        DisplayViewModel displayViewModel,
        PlaybackControlViewModel playbackControlViewModel,
        DeviceControlViewModel deviceControlViewModel,
        MediaControlViewModel mediaControlViewModel,
        LandmarkPresenter landmarkPresenter,
        IMessenger messenger)
    {
        Display = displayViewModel;
        PlaybackControl = playbackControlViewModel;
        DeviceControl = deviceControlViewModel;
        MediaControl = mediaControlViewModel;
        _messenger = messenger;

        this.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedMode))
            {
                IsPlaybackMode = SelectedMode == OperationMode.Playback;
                _messenger.Send(new OperationModeChangedMessage(SelectedMode));
            }
        };

        this.ObservePropertyChanged(x => x.IsKinectInferenceEnabled)
            .Subscribe(isEnabled => landmarkPresenter.IsKinectEnabled = isEnabled)
            .AddTo(ref _disposables);
    }

    public void Dispose()
    {
        DeviceControl?.Dispose();
        PlaybackControl?.Dispose();
        Display?.Dispose();
        _disposables.Dispose();
    }
}
