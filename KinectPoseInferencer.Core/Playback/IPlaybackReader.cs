using R3;
using K4AdotNet.Sensor;
using K4APlayback = K4AdotNet.Record.Playback;

namespace KinectPoseInferencer.Core.Playback;

public interface IPlaybackReader: IAsyncDisposable
{
    ReadOnlyReactiveProperty<K4APlayback?> Playback { get; }
    ReadOnlyReactiveProperty<TimeSpan> InitialDeviceTimestamp { get; }

    Task Configure(PlaybackDescriptor descriptor, CancellationToken token);
    bool TryRead(TimeSpan targetFrameTime, out Capture? capture, out ImuSample? imuSample);
    Task RewindAsync();
    Task SeekAsync(TimeSpan targetFrameTime);
}
