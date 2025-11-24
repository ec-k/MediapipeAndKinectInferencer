using KinectPoseInferencer.Renderers;
using System;

namespace KinectPoseInferencer.Playback;

public class PlaybackController : IPlaybackController
{
    readonly IPlaybackReader _reader;

    public IPlaybackReader Reader => _reader;
    public PlaybackDescriptor Descriptor { get; set; }

    public event Action<bool> PlayingStateChange;
    public event Action<K4AdotNet.Record.Playback> PlaybackLoaded;

    public PlaybackController(IPlaybackReader reader)
    {
        _reader = reader;
        Reader.ReadingStateChange += OnReaderRedingStateChange;
        Reader.PlaybackLoaded += OnPlaybackLoaded;
    }

    void OnReaderRedingStateChange(bool isReading) => PlayingStateChange?.Invoke(isReading);
    void OnPlaybackLoaded(K4AdotNet.Record.Playback playback) => PlaybackLoaded?.Invoke(playback);

    public void Prepare()
    {
        _reader.Configure(Descriptor);
    }

    public void Play()
    {
        _reader.Play();
    }

    public void Pause()
    {
        _reader.Pause();
    }

    public void Rewind()
    {
        _reader.Rewind();
    }

    public void Dispose()
    {
        _reader.Dispose();
        Reader.ReadingStateChange -= OnReaderRedingStateChange;
        Reader.PlaybackLoaded -= OnPlaybackLoaded;
    }
}
