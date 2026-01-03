using HumanLandmarks;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;


namespace KinectPoseInferencer.Core.PoseInference;

[Flags]
public enum ReceiverEventSettings
{
    Body      = 1 << 0,
    LeftHand  = 1 << 1,
    RightHand = 1 << 2,
    Face      = 1 << 3,
}

public class UdpResultReceiver: IDisposable
{
    public Action<MediaPipePoseLandmarks>? PoseReceived;
    public Action<HandLandmarks>?      ReceiveLeftHand;
    public Action<HandLandmarks>?      ReceiveRightHand;
    public Action<FaceResults>?        ReceiveFace;

    UdpClient _receiver;
    ReceiverEventSettings _settings;

    readonly Action<SocketException>?         _socketExceptionCallback;
    readonly Action<ObjectDisposedException>? _objectDisposedExceptionCallback;

    readonly ILogger<UdpResultReceiver> _logger;

    public UdpResultReceiver(
        IPEndPoint receiverEndPoint,
        ReceiverEventSettings settings,
        ILogger<UdpResultReceiver> logger
        )
    {
        _settings = settings;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _receiver = new UdpClient(receiverEndPoint);
        _receiver.BeginReceive(OnReceived, _receiver);
    }

    void OnReceived(IAsyncResult result)
    {
        IPEndPoint? ipEnd = null;

        if (result.AsyncState is not UdpClient udp)
        {
            _logger.LogError("UdpResultReceiver: result.AsyncState is null.");
            return;
        }

        try
        {
            var getByte = udp.EndReceive(result, ref ipEnd);

            var receivedBody = HolisticLandmarks.Parser.ParseFrom(getByte);

            // Invoke events
            if (_settings.HasFlag(ReceiverEventSettings.Body)) PoseReceived?.Invoke(receivedBody.MediaPipePoseLandmarks);
            if (_settings.HasFlag(ReceiverEventSettings.LeftHand)) ReceiveLeftHand?.Invoke(receivedBody.LeftHandLandmarks);
            if (_settings.HasFlag(ReceiverEventSettings.RightHand)) ReceiveRightHand?.Invoke(receivedBody.RightHandLandmarks);
            if (_settings.HasFlag(ReceiverEventSettings.Face)) ReceiveFace?.Invoke(receivedBody.FaceResults);
        }
        catch (SocketException e)
        {
            if (_socketExceptionCallback is not null)
                _socketExceptionCallback(e);
            _logger.LogError("Socket Exception {ex}", e);
            return;
        }
        catch (ObjectDisposedException e)
        {
            if (_objectDisposedExceptionCallback is not null)
                _objectDisposedExceptionCallback(e);
            _logger.LogError("Object disposed exception {ex}", e);
            return;
        }

        _receiver.BeginReceive(OnReceived, udp);
    }

    public void Dispose()
    {
        _receiver.Close();
        _receiver.Dispose();
    }
}
