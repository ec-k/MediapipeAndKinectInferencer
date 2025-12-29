using System;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;

using HumanLandmarks;


namespace KinectPoseInferencer.Core.PoseInference;

public class LandmarkSender: ILandmarkUser, IDisposable
{
    UdpClient _sender = new();

    public LandmarkSender(IPEndPoint endPoint)
    {
        _sender.Connect(endPoint);
    }

    public void Connect(IPEndPoint endPoint)
    {
        if (_sender.Client.Connected)
            _sender.Close();

        _sender.Connect(endPoint);
    }

    public void Process(in HolisticLandmarks landmark)
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
