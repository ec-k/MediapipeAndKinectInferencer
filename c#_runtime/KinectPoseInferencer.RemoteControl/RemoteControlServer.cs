using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.PoseInference;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

namespace KinectPoseInferencer.RemoteControl;

public class RemoteControlServer
{
    readonly HttpListener _listener;
    readonly int _port;
    readonly IPlaybackController _playbackController;
    readonly LandmarkPresenter _landmarkPresenter;

    CancellationTokenSource? _cts;
    ILogger<RemoteControlServer> _logger;

    public RemoteControlServer(
        int port,
        IPlaybackController playbackController,
        LandmarkPresenter landmarkPresenter,
        ILogger<RemoteControlServer> logger
        )
    {
        _port = port;
        _playbackController = playbackController ?? throw new ArgumentNullException(nameof(playbackController));
        _landmarkPresenter = landmarkPresenter ?? throw new ArgumentNullException(nameof(landmarkPresenter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _listener = new();
        _listener.Prefixes.Add($"http://localhost:{_port}/control/");
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        try
        {
            _listener.Start();
            _logger.LogInformation("Listening on http://localhost:{0}/control/", _port);

            using (ct.Register(() => _listener.Stop()))
            {
                while (!ct.IsCancellationRequested)
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is HttpListenerException)
        {
            _logger.LogInformation("HttpControlServer is stopping.");
        }
        finally
        {
            if (_listener.IsListening) _listener.Stop();
        }
    }

    async Task ProcessRequestAsync(HttpListenerContext context)
    {
        using var response = context.Response;

        if (context.Request.HttpMethod != "POST")
        {
            response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            return;
        }

        try
        {
            var message = await JsonSerializer.DeserializeAsync<ControlMessage>(
                context.Request.InputStream
            );

            if (message is null)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            switch (message)
            {
                case SetConfigurationMessage setConfig:
                    if (setConfig.Config is not null)
                        Configure(setConfig.Config);
                    break;
                case PlayMessage:
                    _playbackController.Play();
                    break;
                case PauseMessage:
                    _playbackController.Pause();
                    break;
                case RewindMessage:
                    await _playbackController.Rewind();
                    break;
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            var buffer = Encoding.UTF8.GetBytes("Accepted");
            await response.OutputStream.WriteAsync(buffer);
        }
        catch (JsonException ex)
        {
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            _logger.LogError($"JSON Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            _logger.LogError($"Server Error: {ex.Message}");
        }
    }

    void Configure(InferencerConfiguration config)
    {
        _landmarkPresenter.IsKinectEnabled = config.IsKinectEnabled;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener.Stop();
    }
}