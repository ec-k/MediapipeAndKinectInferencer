using System;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;

using HumanLandmarks;


namespace KinectPoseInferencer.PoseInference;

public class LandmarkSender: ILandmarkUser, IDisposable
{
    UdpClient _sender = new();

    public LandmarkSender(
        string uri = "127.0.0.1",
        int port = 9000
        )
    {
        var senderEndPoint = new IPEndPoint(IPAddress.Parse(uri), port);
        _sender.Connect(senderEndPoint);
    }

    public void Connect(string uri, int port)
    {
        if (_sender.Client.Connected)
            _sender.Close();

        var senderEndPoint = new IPEndPoint(IPAddress.Parse(uri), port);
        _sender.Connect(senderEndPoint);
    }

    public void Process(HolisticLandmarks landmark)
    {
        if (!_sender.Client.Connected)
            return;

        var sendData = landmark.ToByteArray();
        _sender.Send(sendData, sendData.Length);
    }

    public void Dispose()
    {
        _sender.Close();
        _sender.Dispose();
    }
}
