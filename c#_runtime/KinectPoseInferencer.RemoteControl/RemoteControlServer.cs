using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.PoseInference;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace KinectPoseInferencer.RemoteControl;

public class RemoteControlServer
{
    readonly HttpListener _listener;
    readonly int _port;
    readonly IPlaybackController _playbackController;
    readonly LandmarkPresenter _landmarkPresenter;
    WebSocket? _currentClient;

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
                    _ = ProcessHttpRequestAsync(context, ct);
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

    async Task ProcessHttpRequestAsync(HttpListenerContext context, CancellationToken ct)
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

            await ExecuteCommand(message, ct);

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

    public async Task ProcessWebSocketRequest(HttpListenerContext context, CancellationToken ct)
    {
        var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
        _currentClient = wsContext.WebSocket;
        _logger.LogInformation("Client connected via WebSocket");

        var buffer = new byte[1024 * 4];

        try
        {
            while (_currentClient.State == WebSocketState.Open)
            {
                var result = await _currentClient.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close) break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var message = JsonSerializer.Deserialize<ControlMessage>(json);

                if (message is not null)
                {
                    await ExecuteCommand(message, ct);

                    await SendToClientAsync(new
                    {
                        Event = "CommandExecuted",
                        Command = message.GetType().Name,
                        Timestamp = DateTime.Now
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("WebSocket Error: {ex}", ex);
        }
        finally
        {
            _currentClient.Dispose();
            _currentClient = null;
            _logger.LogInformation("Client disconnected");
        }
    }

    public async Task SendToClientAsync<T>(T data)
    {
        if (_currentClient?.State == WebSocketState.Open)
        {
            var json = JsonSerializer.Serialize(data);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _currentClient.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    async Task ExecuteCommand(ControlMessage message, CancellationToken ct)
    {
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