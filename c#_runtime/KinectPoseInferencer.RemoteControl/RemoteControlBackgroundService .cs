using Microsoft.Extensions.Hosting;


namespace KinectPoseInferencer.RemoteControl;

public class RemoteControlBackgroundService : BackgroundService
{
    readonly HttpControlServer _server;

    public RemoteControlBackgroundService(HttpControlServer server)
    {
        _server = server;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _server.StartAsync(stoppingToken);
    }
}
