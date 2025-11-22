using KinectPoseInferencer.Playback.States;
using System;

namespace KinectPoseInferencer.Playback;

public interface IPlaybackController: IDisposable
{
    IPlaybackControllerState CurrentState { get; set; }
    IPlaybackReader Reader { get; }
    PlaybackDescriptor Descriptor { get; set; }

    void Start();
    void Pause();
    void Resume();
    void Stop();
}
