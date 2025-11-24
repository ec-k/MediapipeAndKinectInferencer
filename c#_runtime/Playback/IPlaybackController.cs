using System;

namespace KinectPoseInferencer.Playback;

public interface IPlaybackController: IDisposable
{
    IPlaybackReader Reader { get; }
    PlaybackDescriptor Descriptor { get; set; }

    void Prepare();
    void Play();
    void Pause();
    void Rewind();
}
