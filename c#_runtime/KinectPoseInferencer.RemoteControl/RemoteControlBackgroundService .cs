using KinectPoseInferencer.Core.Playback;
using Microsoft.Extensions.Hosting;


namespace KinectPoseInferencer.RemoteControl;

public class RemoteControlBackgroundService : BackgroundService
{
    readonly RemoteControlServer _server;
    readonly IPlaybackController _playbackController;

    public RemoteControlBackgroundService(
        RemoteControlServer server,
        IPlaybackController playbackController)
    {
        _server = server;
        _playbackController = playbackController;

        _playbackController.OnEOF += NotifyPlaybackEnds;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _server.StartAsync(stoppingToken);
    }

    async void NotifyPlaybackEnds()
    {
        await _server.SendToClientAsync("This playback reached to end.");
    }

    public override void Dispose()
    {
        base.Dispose();
        _playbackController.OnEOF -= NotifyPlaybackEnds;
    }
}
