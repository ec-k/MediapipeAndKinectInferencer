using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using K4AdotNet.Sensor;
using K4AdotNet.BodyTracking;
using KinectPoseInferencer.Playback;
using KinectPoseInferencer.Renderers;
using R3;
using System;
using System.Windows.Media.Media3D;
using System.Windows;
using System.Linq;
using System.Collections.ObjectModel;


namespace KinectPoseInferencer.UI;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    readonly IPlaybackController _controller;
    readonly ObservableCollection<ModelVisual3D> _modelVisuals = new();

    PlayerVisualizer _visualizer;

    [ObservableProperty] double _currentFrameTimestamp;
    [ObservableProperty] string _playbackLength;
    [ObservableProperty] string _playPauseIconUnicode;
    [ObservableProperty] string _videoFilePath;

    const string PlayIconUnicode = "&#xE768;";
    const string PauseIconUnicode = "&#xE769";

    public ObservableCollection<ModelVisual3D> ModelVisuals => _modelVisuals;

    DisposableBag _disposables = new();
    
    public MainWindowViewModel(IPlaybackController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));

        _controller.Reader.Playback
            .Where(playback => playback is not null)
            .Subscribe(playback => {
                    UpdatePlaybackLengthDisplay(playback);
                    playback.GetCalibration(out var calibration);
                    _visualizer = new PlayerVisualizer(calibration);
            })
            .AddTo(ref _disposables);
        _controller.Reader.IsReading
            .Subscribe(isPlaying => PlayPauseIconUnicode = isPlaying ? PauseIconUnicode : PlayIconUnicode)
            .AddTo(ref _disposables);

        _controller.Reader.OnNewFrame += OnNewFrame;
    }

    void OnNewFrame(BodyFrame frame, Capture capture)
    {
        var depthImage = capture.DepthImage;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var newModels = _visualizer.UpdateVisuals(frame, depthImage);
            _modelVisuals.Clear();
            foreach (var model in newModels)
            {
                _modelVisuals.Add(model);
            }
        });
        frame.Dispose();
        capture.Dispose();
    }

    void UpdatePlaybackLengthDisplay(K4AdotNet.Record.Playback playback)
    {
        if (playback is null) return;

        var minutes = (int)playback.RecordLength.TotalSeconds / 60;
        var seconds = (int)playback.RecordLength.TotalSeconds % 60;

        PlaybackLength = $"{minutes}:{seconds}";
    }

    [RelayCommand]
    void LoadFiles()
    {
        if (string.IsNullOrEmpty(VideoFilePath)) return;

        var playbackDesc = new PlaybackDescriptor(VideoFilePath);
        _controller.Descriptor = playbackDesc;
        _controller.Prepare();
    }

    [RelayCommand]
    void Rewind()
    {
        _controller.Rewind();
    }

    [RelayCommand]
    public void PlayOrPause()
    {
        if (_controller.Reader.IsReading.CurrentValue)
            _controller.Pause();
        else
            _controller.Play();
    }

    [RelayCommand]
    public void Play() { }

    [RelayCommand]
    public void Pause() { }

    public void Dispose()
    {
        _controller.Reader.OnNewFrame -= OnNewFrame;
        _visualizer?.Dispose();
        _disposables.Dispose();
    }
}
