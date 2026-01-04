using R3;

namespace KinectPoseInferencer.Core.Playback;

public interface IPlaybackController: IAsyncDisposable
{
    IPlaybackReader Reader { get; }
    PlaybackDescriptor? Descriptor { get; set; }
    ReadOnlyReactiveProperty<PlaybackState> State { get; }
    ReadOnlyReactiveProperty<TimeSpan> CurrentTime { get; }
    event Action OnEOF;

    Task Prepare(CancellationToken token);
    void Play();
    void Pause();
    Task Rewind();
    Task SeekAsync(TimeSpan position);
}
