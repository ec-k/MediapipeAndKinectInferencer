using KinectPoseInferencer.Core.Playback;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace KinectPoseInferencer.RemoteControl;

public class RemoteControlBackgroundService : BackgroundService
{
    readonly RemoteControlServer _server;
    readonly IPlaybackController _playbackController;
    readonly ILogger<RemoteControlBackgroundService> _logger;

    public RemoteControlBackgroundService(
        RemoteControlServer server,
        IPlaybackController playbackController,
        ILogger<RemoteControlBackgroundService> logger)
    {
        _server = server;
        _playbackController = playbackController;
        _logger = logger;

        _playbackController.OnEOF += NotifyPlaybackEnds;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _server.StartAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteAsync exception");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop the server directly to unblock ExecuteAsync (faster than waiting for CancellationToken propagation)
        _server.Stop();

        // Give ExecuteAsync time to complete after server stops
        await Task.Delay(100).ConfigureAwait(false);

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
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
