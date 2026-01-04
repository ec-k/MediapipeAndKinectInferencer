using Microsoft.Extensions.Hosting;


namespace KinectPoseInferencer.RemoteControl;

public class RemoteControlBackgroundService : BackgroundService
{
    readonly RemoteControlServer _server;

    public RemoteControlBackgroundService(RemoteControlServer server)
    {
        _server = server;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _server.StartAsync(stoppingToken);
    }
}
