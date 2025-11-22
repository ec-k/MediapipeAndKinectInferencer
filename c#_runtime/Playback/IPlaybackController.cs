using KinectPoseInferencer.Playback.States;
using System;

namespace KinectPoseInferencer.Playback;

public interface IPlaybackController: IDisposable
{
    IPlaybackControllerState CurrentState { get; set; }
    IPlaybackReader Reader { get; }

    void Start();
    void Pause();
    void Resume();
    void Stop();
}
