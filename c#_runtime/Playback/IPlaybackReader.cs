using K4AdotNet;
using System;

namespace KinectPoseInferencer.Playback;

public interface IPlaybackReader: IDisposable
{
    K4AdotNet.Record.Playback Playback { get; }
    event Action<bool> ReadingStateChange;
    event Action<K4AdotNet.Record.Playback> PlaybackLoaded;
    bool IsReading { get; }
    void Configure(PlaybackDescriptor descriptor);
    void Start();
    void Pause();
    void Resume();
    void Stop();
}
