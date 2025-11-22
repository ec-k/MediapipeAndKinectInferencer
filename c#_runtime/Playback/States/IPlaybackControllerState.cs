namespace KinectPoseInferencer.Playback.States;

public interface IPlaybackControllerState
{
    void Start();
    void Pause();
    void Resume();
    void Stop();
}
