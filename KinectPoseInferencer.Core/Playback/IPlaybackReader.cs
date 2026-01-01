using R3;
using K4AdotNet.Sensor;

namespace KinectPoseInferencer.Core.Playback;

public interface IPlaybackReader: IAsyncDisposable
{
    ReadOnlyReactiveProperty<K4AdotNet.Record.Playback> Playback { get; }

    Task Configure(PlaybackDescriptor descriptor, CancellationToken token);
    bool TryRead(TimeSpan targetFrameTime, out Capture? capture, out ImuSample? imuSample);
    Task RewindAsync();
    void Seek(TimeSpan targetFrameTime);
}
