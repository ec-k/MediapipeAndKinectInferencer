using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using R3;
using System;

namespace KinectPoseInferencer.Playback;

public interface IPlaybackReader: IDisposable
{
    ReadOnlyReactiveProperty<K4AdotNet.Record.Playback> Playback { get; }
    ReadOnlyReactiveProperty<bool> IsReading { get; }
    event Action<BodyFrame, Capture> OnNewFrame;

    void Configure(PlaybackDescriptor descriptor);
    void Play();
    void Pause();
    void Rewind();
}
