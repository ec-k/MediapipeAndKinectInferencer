using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KinectPoseInferencer.Playback;
using System;


namespace KinectPoseInferencer.UI;

public partial class MainWindowViewModel : ObservableObject
{
    IPlaybackController _controller;

    [ObservableProperty] double _currentFrameTimestamp;
    [ObservableProperty] string _playbackLength;
    [ObservableProperty] string _playPauseIconUnicode;
    [ObservableProperty] string _videoFilePath;

    const string PlayIconUnicode = "&#xE768;";
    const string PauseIconUnicode = "&#xE769";
    
    public MainWindowViewModel(IPlaybackController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _controller.PlayingStateChange += OnPlaybackStateChange;
        _controller.PlaybackLoaded += OnPlaybackLoaded;

        OnPlaybackStateChange(_controller.Reader.IsReading);
    }

    void OnPlaybackStateChange(bool isPlaying)
    {
        PlayPauseIconUnicode = isPlaying ? PauseIconUnicode : PlayIconUnicode;
    }

    void OnPlaybackLoaded(K4AdotNet.Record.Playback playback)
    {
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
        if (_controller.Reader.IsReading)
            _controller.Pause();
        else
            _controller.Play();
    }

    [RelayCommand]
    public void Play() { }

    [RelayCommand]
    public void Pause() { }
}
