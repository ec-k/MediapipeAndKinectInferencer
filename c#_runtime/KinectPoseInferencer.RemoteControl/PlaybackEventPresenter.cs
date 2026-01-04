using KinectPoseInferencer.Core.Playback;


namespace KinectPoseInferencer.RemoteControl;

public class PlaybackEventPresenter : IDisposable
{
    IPlaybackController _playbackController;
    RemoteControlServer _server;

    public PlaybackEventPresenter(
        IPlaybackController playbackController,
        RemoteControlServer server
        )
    {
        _playbackController = playbackController ?? throw new ArgumentNullException(nameof(playbackController));
        _server = server ?? throw new ArgumentNullException(nameof(server));

        _playbackController.OnEOF += NotifyPlaybackEnds;
    }

    async void NotifyPlaybackEnds()
    {
        await _server.SendToClientAsync("This playback reached to end.");
    }

    public void Dispose()
    {
        _playbackController.OnEOF -= NotifyPlaybackEnds;
    }
}
