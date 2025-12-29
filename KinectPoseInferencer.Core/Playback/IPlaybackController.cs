using R3;

namespace KinectPoseInferencer.Core.Playback;

public interface IPlaybackController: IAsyncDisposable
{
    IPlaybackReader Reader { get; }
    PlaybackDescriptor? Descriptor { get; set; }
    ReadOnlyReactiveProperty<bool> IsPlaying { get; }

    Task Prepare(CancellationToken token);
    void Play();
    void Pause();
    Task Rewind();
    void Seek(TimeSpan position);
}
