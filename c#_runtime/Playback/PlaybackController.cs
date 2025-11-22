using KinectPoseInferencer.Playback.States;
using KinectPoseInferencer.Renderers;

namespace KinectPoseInferencer.Playback;

public class PlaybackController : IPlaybackController
{
    readonly IPlaybackReader _reader;

    public IPlaybackReader Reader => _reader;
    public IPlaybackControllerState CurrentState { get; set; }
    PlaybackDescriptor _descriptor { get; set; }

    public PlaybackController(PlaybackDescriptor descriptor, IPlaybackReader reader)
    {
        _descriptor = descriptor;
        _reader = reader;

        CurrentState = new IdleState(this);
    }

    public void Start()
    {
        _reader.Configure(_descriptor);
        _reader.Playback.GetCalibration(out var calibration);
        PointCloud.ComputePointCloudCache(calibration);
        CurrentState.Start();
    }

    public void Pause()
    {
        CurrentState.Pause();
    }

    public void Resume()
    {
        CurrentState.Resume();
    }

    public void Stop()
    {
        CurrentState.Stop();
    }

    public void Dispose()
    {
        _reader.Dispose();
    }
}
