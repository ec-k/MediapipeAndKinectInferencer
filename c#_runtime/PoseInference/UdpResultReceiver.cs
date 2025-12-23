using HumanLandmarks;
using System;
using System.Net;
using System.Net.Sockets;


namespace KinectPoseInferencer.PoseInference;

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
    public Action<KinectPoseLandmarks>? PoseReceived;
    public Action<HandLandmarks>?       ReceiveLeftHand;
    public Action<HandLandmarks>?       ReceiveRightHand;
    public Action<FaceResults>?         ReceiveFace;

    UdpClient _receiver;
    ReceiverEventSettings _settings;

    readonly Action<SocketException>?         _socketExceptionCallback;
    readonly Action<ObjectDisposedException>? _objectDisposedExceptionCallback;

    public UdpResultReceiver(
        ReceiverEventSettings settings,
        int port
        )
    {
        _settings = settings;
        _receiver = new UdpClient(port);
        _receiver.BeginReceive(OnReceived, _receiver);
    }

    void OnReceived(IAsyncResult result)
    {
        var udp = result.AsyncState as UdpClient;
        IPEndPoint? ipEnd = null;

        if (udp is not UdpClient)
        {
            Console.Error.WriteLine("UdpResultReceiver: result.AsyncState is null.");
            return;
        }

        try
        {
            var getByte = udp.EndReceive(result, ref ipEnd);

            var receivedBody = HolisticLandmarks.Parser.ParseFrom(getByte);

            // Invoke events
            if (_settings.HasFlag(ReceiverEventSettings.Body)) PoseReceived?.Invoke(receivedBody.PoseLandmarks);
            if (_settings.HasFlag(ReceiverEventSettings.LeftHand)) ReceiveLeftHand?.Invoke(receivedBody.LeftHandLandmarks);
            if (_settings.HasFlag(ReceiverEventSettings.RightHand)) ReceiveRightHand?.Invoke(receivedBody.RightHandLandmarks);
            if (_settings.HasFlag(ReceiverEventSettings.Face)) ReceiveFace?.Invoke(receivedBody.FaceResults);
        }
        catch (SocketException e)
        {
            if (_socketExceptionCallback is not null)
                _socketExceptionCallback(e);
            return;
        }
        catch (ObjectDisposedException e)
        {
            if (_objectDisposedExceptionCallback is not null)
                _objectDisposedExceptionCallback(e);
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
