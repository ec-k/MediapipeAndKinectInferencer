using KinectPoseInferencer.Playback.States;
using KinectPoseInferencer.Renderers;
using System;

namespace KinectPoseInferencer.Playback;

public class PlaybackController : IPlaybackController
{
    readonly IPlaybackReader _reader;

    public IPlaybackReader Reader => _reader;
    public IPlaybackControllerState CurrentState { get; set; }
    public PlaybackDescriptor Descriptor { get; set; }

    public event Action<bool> PlayingStateChange;
    public event Action<K4AdotNet.Record.Playback> PlaybackLoaded;

    public PlaybackController(IPlaybackReader reader)
    {
        _reader = reader;
        CurrentState = new IdleState(this);
        Reader.ReadingStateChange += OnReaderRedingStateChange;
        Reader.PlaybackLoaded += OnPlaybackLoaded;
    }

    void OnReaderRedingStateChange(bool isReading) => PlayingStateChange?.Invoke(isReading);
    void OnPlaybackLoaded(K4AdotNet.Record.Playback playback) => PlaybackLoaded?.Invoke(playback);

    public void Start()
    {
        _reader.Configure(Descriptor);
        _reader.Playback.GetCalibration(out var calibration);
        PointCloud.ComputePointCloudCache(calibration);
        CurrentState.Start();
    }

    public void Pause()
    {
        CurrentState.Pause();
    }

    public void Resume()
    {
        CurrentState.Resume();
    }

    public void Stop()
    {
        CurrentState.Stop();
    }

    public void Dispose()
    {
        _reader.Dispose();
        Reader.ReadingStateChange -= OnReaderRedingStateChange;
        Reader.PlaybackLoaded -= OnPlaybackLoaded;
    }
}
