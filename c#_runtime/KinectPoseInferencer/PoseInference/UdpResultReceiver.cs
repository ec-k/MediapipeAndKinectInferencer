using HumanLandmarks;
using System;
using System.Net;
using System.Net.Sockets;


namespace KinectPoseInferencer.PoseInference;

[Flags]
public enum ReceiverEventSettings
{
    Body,
    LeftHand,
    RightHand,
    Face,
}

public class UdpResultReceiver: IDisposable
{
    public Action<KinectPoseLandmarks> PoseReceived;
    public Action<HandLandmarks>       ReceiveLeftHand;
    public Action<HandLandmarks>       ReceiveRightHand;
    public Action<FaceResults>         ReceiveFace;

    UdpClient _receiver;
    ReceiverEventSettings _settings;

    readonly Action<SocketException> _socketExceptionCallback;
    readonly Action<ObjectDisposedException> _objectDisposedExceptionCallback;

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
        var getUdp = (UdpClient)result.AsyncState;
        IPEndPoint ipEnd = null;

        try
        {
            var getByte = getUdp.EndReceive(result, ref ipEnd);

            var receivedBody = HolisticLandmarks.Parser.ParseFrom(getByte);

            // Invoke events
            if (_settings.HasFlag(ReceiverEventSettings.Body))       PoseReceived?.Invoke(receivedBody.PoseLandmarks);
            if (_settings.HasFlag(ReceiverEventSettings.LeftHand))   ReceiveLeftHand?.Invoke(receivedBody.LeftHandLandmarks);
            if (_settings.HasFlag(ReceiverEventSettings.RightHand))  ReceiveRightHand?.Invoke(receivedBody.RightHandLandmarks);
            if (_settings.HasFlag(ReceiverEventSettings.Face))       ReceiveFace?.Invoke(receivedBody.FaceResults);
        }
        catch (SocketException e)
        {
            _socketExceptionCallback(e);
            return;
        }
        catch (ObjectDisposedException e)
        {
            _objectDisposedExceptionCallback(e);
            return;
        }

        _receiver.BeginReceive(OnReceived, getUdp);
    }

    public void Dispose()
    {
        _receiver.Close();
        _receiver.Dispose();
    }
}
