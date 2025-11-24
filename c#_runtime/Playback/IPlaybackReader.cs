using R3;
using System;

namespace KinectPoseInferencer.Playback;

public interface IPlaybackReader: IDisposable
{
    ReadOnlyReactiveProperty<K4AdotNet.Record.Playback> Playback { get; }
    ReadOnlyReactiveProperty<bool> IsReading { get; }

    void Configure(PlaybackDescriptor descriptor);
    void Play();
    void Pause();
    void Rewind();
}
