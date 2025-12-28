using R3;
using K4AdotNet.Sensor;

namespace KinectPoseInferencer.Core.Playback;

public interface IPlaybackReader: IAsyncDisposable
{
    ReadOnlyReactiveProperty<K4AdotNet.Record.Playback> Playback { get; }
    ReadOnlyReactiveProperty<K4AdotNet.Microseconds64> CurrentPositionUs { get; }

    Task Configure(PlaybackDescriptor descriptor, CancellationToken token);
    bool TryRead(TimeSpan targetFrameTime, out Capture? capture, out ImuSample? imuSample);
    void Rewind();
    void Seek(TimeSpan targetFrameTime);
}
