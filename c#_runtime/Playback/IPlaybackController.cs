using KinectPoseInferencer.Playback.States;
using System;

namespace KinectPoseInferencer.Playback;

public interface IPlaybackController: IDisposable
{
    IPlaybackControllerState CurrentState { get; set; }
    event Action<bool> PlayingStateChange;
    event Action<K4AdotNet.Record.Playback> PlaybackLoaded;

    IPlaybackReader Reader { get; }
    PlaybackDescriptor Descriptor { get; set; }

    void Start();
    void Pause();
    void Resume();
    void Stop();
}
