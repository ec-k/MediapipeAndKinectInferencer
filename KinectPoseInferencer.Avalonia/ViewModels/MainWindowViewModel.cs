using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using K4AdotNet;
using K4AdotNet.BodyTracking;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Core;
using KinectPoseInferencer.Core.InputHook;
using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.PoseInference;
using KinectPoseInferencer.Core.Settings;
using KinectPoseInferencer.Renderers;
using Microsoft.Extensions.Logging;
using R3;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;


namespace KinectPoseInferencer.Avalonia.ViewModels;

public enum OperationMode
{
    Playback,
    Device
}

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    readonly IPlaybackController _playbackController;
    readonly KinectDeviceController _kinectDeviceController;
    readonly SettingsManager _settingsManager;

    [ObservableProperty] string _playPauseIconUnicode = PlayIconUnicode;
    [ObservableProperty] string _videoFilePath = "";
    [ObservableProperty] string _inputLogFilePath = "";
    [ObservableProperty] string _metaFilePath = "";
    [ObservableProperty] bool _isKinectInferenceEnabled = true;
    [ObservableProperty] WriteableBitmap? _colorBitmap;
    [ObservableProperty] OperationMode _selectedMode = OperationMode.Playback;
    [ObservableProperty] bool _isPlaybackMode = true;
    [ObservableProperty] string _secondaryButtonIconUnicode = RewindIconUnicode;

    [ObservableProperty] bool _isLoading = false;
    [ObservableProperty] TimeSpan _playbackLength;
    [ObservableProperty] double _seekSliderPosition;
    [ObservableProperty] TimeSpan _currentTime;

    bool _isInternalUpdating = false;
    const string PlayIconUnicode = "\uE768";
    const string PauseIconUnicode = "\uE769";
    const string RewindIconUnicode = "\uE892";
    const string StopIconUnicode = "\uE71A";

    readonly int MaxSeekFramesForColorImage = 100;

    public ObservableCollection<UIElement> BodyVisualElements { get; } = new();
    public ObservableCollection<string> InputLogEvents { get; } = new();

    RecordDataBroker _broker;
    DisposableBag _disposables = new();
    readonly ILogger<MainWindowViewModel> _logger;

    public MainWindowViewModel(
        IPlaybackController playbackController,
        KinectDeviceController kinectDeviceController,
        RecordDataBroker broker,
        SettingsManager settingManager,
        LandmarkPresenter landmarkPresenter,
        ILogger<MainWindowViewModel> logger)
    {
        _playbackController = playbackController ?? throw new ArgumentNullException(nameof(playbackController));
        _kinectDeviceController = kinectDeviceController ?? throw new ArgumentNullException(nameof(kinectDeviceController));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _settingsManager = settingManager ?? throw new ArgumentNullException(nameof(settingManager));
        if (landmarkPresenter is null) throw new ArgumentNullException(nameof(landmarkPresenter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var latestSetting = _settingsManager.Load();
        _videoFilePath = latestSetting.VideoFilePath;
        _inputLogFilePath = latestSetting.InputLogFilePath;
        _metaFilePath = latestSetting.MetaFilePath;

        _playbackController.Reader.Playback
            .Where(playback => playback is not null)
            .Subscribe(playback => {
                Calibration calibration = default;
                bool isCalibrationLoaded = false;
                // Get calibration
                try
                {
                    playback.GetCalibration(out calibration);
                    isCalibrationLoaded = true;
                }
                catch (PlaybackException)
                {
                    // Clipped video by k4acut may not have calibration data, but it may have custom calibration stored in tags.
                    if (playback.TryGetTag("CUSTOM_CALIBRATION_RAW", out var base64))
                    {
                        var rawData = Convert.FromBase64String(base64);
                        playback.GetRecordConfiguration(out var recordConfig);
                        Calibration.CreateFromRaw(rawData, recordConfig.DepthMode, recordConfig.ColorResolution, out calibration);
                        isCalibrationLoaded = calibration.IsValid;
                    }
                }
                finally
                {
                    if (isCalibrationLoaded)
                        PointCloud.ComputePointCloudCache(calibration);
                    PlaybackLength = playback.RecordLength;
                }
            })
            .AddTo(ref _disposables);
        _playbackController.State
            .Subscribe(state =>
            {
                IsLoading = state is PlaybackState.Lock;

                // Update play/pause icon only if Playback mode is selected
                if (SelectedMode == OperationMode.Playback)
                {
                    if (state is PlaybackState.Playing)
                        PlayPauseIconUnicode = PauseIconUnicode;
                    if (state is PlaybackState.Pause)
                        PlayPauseIconUnicode = PlayIconUnicode;
                }
            })
            .AddTo(ref _disposables);

        _kinectDeviceController.IsReading
            .Subscribe(isReading =>
            {
                // Update play/pause icon only if Device mode is selected
                if (SelectedMode == OperationMode.Device)
                    PlayPauseIconUnicode = isReading ? PauseIconUnicode : PlayIconUnicode;
            })
            .AddTo(ref _disposables);

        // Update icon and mode flags when mode changes
        this.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedMode))
            {
                IsPlaybackMode = SelectedMode == OperationMode.Playback;
                SecondaryButtonIconUnicode = IsPlaybackMode ? RewindIconUnicode : StopIconUnicode;

                if (SelectedMode == OperationMode.Playback)
                {
                    var state = _playbackController.State.CurrentValue;
                    PlayPauseIconUnicode = state is PlaybackState.Playing ? PauseIconUnicode : PlayIconUnicode;
                }
                else
                {
                    var isReading = _kinectDeviceController.IsReading.CurrentValue;
                    PlayPauseIconUnicode = isReading ? PauseIconUnicode : PlayIconUnicode;
                }
            }
        };

        _playbackController.CurrentTime
            .Subscribe(time =>
            {
                _isInternalUpdating = true;
                CurrentTime = time;
                SeekSliderPosition = time.TotalSeconds;
                _isInternalUpdating = false;
            })
            .AddTo(ref _disposables);

        _broker.Capture
            .Where(capture => capture is not null)
            .Subscribe(capture => DisplayCapture(capture))
            .AddTo(ref _disposables);
        _broker.DeviceInputData
            .Where(input => input is not null)
            .Chunk(TimeSpan.FromSeconds(1.0 / 30.0))
            .Where(inputs => inputs is { Length: > 0 })
            .Subscribe(inputs => OnNewInputLogEvent(inputs))
            .AddTo(ref _disposables);

        this.ObservePropertyChanged(x => x.IsKinectInferenceEnabled)
            .Subscribe(isEnabled => landmarkPresenter.IsKinectEnabled = isEnabled)
            .AddTo(ref _disposables);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    async Task SecondaryButton(CancellationToken token)
    {
        if (SelectedMode == OperationMode.Playback)
        {
            await _playbackController.Rewind();
        }
        else
        {
            DeviceStop();
        }
    }

    [RelayCommand]
    public void PlayOrPause()
    {
        if (SelectedMode == OperationMode.Playback)
        {
            PlaybackPlayOrPause();
        }
        else
        {
            DevicePlayOrPause();
        }
    }

    public void Dispose()
    {
        _kinectDeviceController?.Dispose();
        _playbackController?.DisposeAsync();
        _disposables.Dispose();

        if (GlobalInputHook.IsHookActive)
        {
            GlobalInputHook.StopProcessingEvents();
            GlobalInputHook.StopHooks();
        }
    }
}
