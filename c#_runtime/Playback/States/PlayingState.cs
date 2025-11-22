namespace KinectPoseInferencer.Playback.States;

public class PlayingState : IPlaybackControllerState
{
    readonly IPlaybackController _context;

    public PlayingState(IPlaybackController context)
    {
        _context = context;
    }

    public void Start() { }

    public void Pause()
    {
        _context.Reader.Pause();
        _context.CurrentState = new IdleState(_context);
    }

    public void Resume() { }

    public void Stop()
    {
        _context.Reader.Stop();
        _context.CurrentState = new IdleState(_context);
    }
}
