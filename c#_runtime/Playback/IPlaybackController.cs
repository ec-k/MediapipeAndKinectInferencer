using System;
using System.Threading;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Playback;

public interface IPlaybackController: IDisposable
{
    IPlaybackReader Reader { get; }
    PlaybackDescriptor Descriptor { get; set; }

    Task Prepare(CancellationToken token);
    void Play();
    void Pause();
    void Rewind();
    void Seek(TimeSpan position);
}
