using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Playback;
using System;
using System.IO;


namespace KinectPoseInferencer.UI;

public partial class MainWindowViewModel : ObservableObject
{
    IPlaybackController _controller;

    [ObservableProperty] double _currentFrameTimestamp;
    [ObservableProperty] string _playbackLength;
    [ObservableProperty] string _playPauseIconUnicode;

    bool _isPlaying;
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
        // implement:
        //      1. Load files specified in GUI
        //      2. Compose PlaybackControllerDescriptor
        //      3. Call playbackController.Configure(desc);
        //      4. Call playbackController.Start();

        // Setup a device
        var testVideoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "KinectAndInputRecorder",
            "test_video.mkv");                          // This should specified by settings or user (via UI)
        var recordConfig = new RecordConfiguration()    // This should specified by metafile
        {
            ColorFormat = ImageFormat.ColorBgra32,
            ColorResolution = ColorResolution.R720p,
            DepthMode = DepthMode.NarrowViewUnbinned,
            CameraFps = FrameRate.Thirty,
        };
        var playbackDesc = new PlaybackDescriptor(testVideoPath, recordConfig);
        _controller.Descriptor = playbackDesc;
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
