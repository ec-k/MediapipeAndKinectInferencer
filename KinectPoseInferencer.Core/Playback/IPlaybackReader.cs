using R3;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Core.Playback;

public interface IPlaybackReader: IDisposable
{
    ReadOnlyReactiveProperty<K4AdotNet.Record.Playback> Playback { get; }
    ReadOnlyReactiveProperty<bool> IsReading { get; }
    ReadOnlyReactiveProperty<K4AdotNet.Microseconds64> CurrentPositionUs { get; }

    Task Configure(PlaybackDescriptor descriptor, CancellationToken token);
    void Play();
    void Pause();
    void Rewind();
    void Seek(TimeSpan position);
}
