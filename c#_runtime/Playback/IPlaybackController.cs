using System;

namespace KinectPoseInferencer.Playback;

public interface IPlaybackController: IDisposable
{
    event Action<bool> PlayingStateChange;
    event Action<K4AdotNet.Record.Playback> PlaybackLoaded;

    IPlaybackReader Reader { get; }
    PlaybackDescriptor Descriptor { get; set; }

    void Play();
    void Pause();
    void Rewind();
}
