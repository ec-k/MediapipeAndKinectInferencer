namespace KinectPoseInferencer.Playback.States;

public class IdleState : IPlaybackControllerState
{
    readonly IPlaybackController _context;

    public IdleState(IPlaybackController context)
    {
        _context = context;
    }

    public void Start()
    {
        _context.Reader.Start();
        _context.CurrentState = new PlayingState(_context);
    }

    public void Pause()
    {
        _context.Reader.Pause();
    }

    public void Resume()
    {
        _context.Reader.Resume();
        _context.CurrentState = new PlayingState(_context);
    }

    public void Stop()
    {
        _context.Reader.Stop();
    }
}
