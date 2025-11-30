using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using R3;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Playback;

public interface IPlaybackReader: IDisposable
{
    ReadOnlyReactiveProperty<K4AdotNet.Record.Playback> Playback { get; }
    ReadOnlyReactiveProperty<bool> IsReading { get; }
    event Action<BodyFrame, Capture> OnNewFrame;

    Task Configure(PlaybackDescriptor descriptor, CancellationToken token);
    void Play();
    void Pause();
    void Rewind();
}
