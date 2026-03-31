using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using K4AdotNet;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Avalonia.Messages;
using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.Settings;
using KinectPoseInferencer.Renderers;
using Microsoft.Extensions.Logging;
using R3;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Avalonia.ViewModels;

public partial class PlaybackControlViewModel : ViewModelBase,
    IRecipient<OperationModeChangedMessage>,
    IRecipient<PlayPauseRequestedMessage>,
    IRecipient<SecondaryActionRequestedMessage>,
    IDisposable
{
    readonly IPlaybackController _playbackController;
    readonly SettingsManager _settingsManager;
    readonly DisplayViewModel _displayViewModel;
    readonly IMessenger _messenger;
    readonly ILogger<PlaybackControlViewModel> _logger;

    [ObservableProperty] string _videoFilePath = "";
    [ObservableProperty] string _inputLogFilePath = "";
    [ObservableProperty] string _metaFilePath = "";
    [ObservableProperty] bool _isLoading = false;
    [ObservableProperty] TimeSpan _playbackLength;
    [ObservableProperty] double _seekSliderPosition;
    [ObservableProperty] TimeSpan _currentTime;

    OperationMode _currentMode = OperationMode.Playback;
    bool _isInternalUpdating = false;
    readonly int MaxSeekFramesForColorImage = 100;

    DisposableBag _disposables = new();

    public PlaybackControlViewModel(
        IPlaybackController playbackController,
        SettingsManager settingsManager,
        DisplayViewModel displayViewModel,
        IMessenger messenger,
        ILogger<PlaybackControlViewModel> logger)
    {
        _playbackController = playbackController;
        _settingsManager = settingsManager;
        _displayViewModel = displayViewModel;
        _messenger = messenger;
        _logger = logger;
        _messenger.RegisterAll(this);

        var latestSetting = _settingsManager.Load();
        _videoFilePath = latestSetting.VideoFilePath;
        _inputLogFilePath = latestSetting.InputLogFilePath;
        _metaFilePath = latestSetting.MetaFilePath;

        _playbackController.Reader.Playback
            .Where(playback => playback is not null)
            .Subscribe(playback =>
            {
                Calibration calibration = default;
                bool isCalibrationLoaded = false;
                try
                {
                    playback.GetCalibration(out calibration);
                    isCalibrationLoaded = true;
                }
                catch (PlaybackException)
                {
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

                if (_currentMode == OperationMode.Playback)
                    _messenger.Send(new PlaybackStateChangedMessage(state is PlaybackState.Playing));
            })
            .AddTo(ref _disposables);

        _playbackController.CurrentTime
            .Subscribe(time =>
            {
                _isInternalUpdating = true;
                CurrentTime = time;
                SeekSliderPosition = time.TotalSeconds;
                _isInternalUpdating = false;
            })
            .AddTo(ref _disposables);
    }

    public void Receive(OperationModeChangedMessage message)
    {
        _currentMode = message.Mode;

        if (_currentMode == OperationMode.Playback)
        {
            var state = _playbackController.State.CurrentValue;
            _messenger.Send(new PlaybackStateChangedMessage(state is PlaybackState.Playing));
        }
    }

    public void Receive(PlayPauseRequestedMessage message)
    {
        if (_currentMode != OperationMode.Playback) return;
        PlaybackPlayOrPause();
    }

    public void Receive(SecondaryActionRequestedMessage message)
    {
        if (_currentMode != OperationMode.Playback) return;
        _ = _playbackController.Rewind();
    }

    [RelayCommand(IncludeCancelCommand = true)]
    async Task LoadFiles(CancellationToken token)
    {
        if (string.IsNullOrEmpty(VideoFilePath)) return;

        _settingsManager.Save(new()
        {
            VideoFilePath = VideoFilePath,
            InputLogFilePath = InputLogFilePath,
            MetaFilePath = MetaFilePath,
        });

        try
        {
            var playbackDesc = new PlaybackDescriptor(VideoFilePath, InputLogFilePath, MetaFilePath);
            _playbackController.Descriptor = playbackDesc;
            await _playbackController.Prepare(token);

            if (_playbackController.Reader.Playback.CurrentValue is Playback playback)
            {
                await Task.Run(() => DisplayFirstColorFrame(playback, token), token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("File loading was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading file: {ex.Message}");
        }
    }

    partial void OnSeekSliderPositionChanged(double value)
    {
        if (_isInternalUpdating) return;
        var targetTime = TimeSpan.FromSeconds(value);
        CurrentTime = targetTime;
    }

    public void ConfirmSeek()
    {
        _playbackController.SeekAsync(TimeSpan.FromSeconds(SeekSliderPosition));
    }

    void PlaybackPlayOrPause()
    {
        var state = _playbackController.State.CurrentValue;

        if (state is PlaybackState.Playing)
            _playbackController.Pause();
        if (state is PlaybackState.Pause)
            _playbackController.Play();
    }

    async Task DisplayFirstColorFrame(Playback playback, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();

            playback.SeekTimestamp(new Microseconds64(0), PlaybackSeekOrigin.Begin);

            Capture? captureToDisplay = null;
            bool foundColorImage = false;

            for (int i = 0; i < MaxSeekFramesForColorImage; i++)
            {
                token.ThrowIfCancellationRequested();

                if (!playback.TryGetNextCapture(out var currentCapture))
                {
                    break;
                }

                if (currentCapture.ColorImage is not null)
                {
                    captureToDisplay = currentCapture.DuplicateReference();
                    foundColorImage = true;
                    break;
                }
                currentCapture?.Dispose();
            }

            if (foundColorImage && captureToDisplay is { ColorImage: not null })
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _displayViewModel.SetColorBitmap(captureToDisplay.ColorImage.ToWriteableBitmap(null));
                    captureToDisplay?.Dispose();
                });
            }
            else
            {
                playback.SeekTimestamp(new Microseconds64(0), PlaybackSeekOrigin.Begin);
                if (playback.TryGetNextCapture(out var firstCapture))
                {
                    using (firstCapture)
                    {
                        if (firstCapture?.DepthImage is not null)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                _displayViewModel.SetColorBitmap(firstCapture.DepthImage.ToWriteableBitmap(null));
                            });
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("DisplayFirstColorFrame was cancelled.");
        }
    }

    public void Dispose()
    {
        _playbackController?.DisposeAsync();
        _disposables.Dispose();
    }
}
