using MessagePack;
using System.Net;
using System.Net.Sockets;


namespace KinectPoseInferencer.Core;

public class InputEventSender: IDisposable
{
    UdpClient _sender = new();
    readonly List<IPEndPoint> _endPoints = new();

    public InputEventSender(string host, int port)
    {
        _sender.Client?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        AddEndPoint(host, port);
    }

    public InputEventSender(in IList<IPEndPoint> endPoints)
    {
        _sender.Client?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        AddEndPoints(endPoints);
    }

    public void AddEndPoint(string host, int port)
    {
        if(IPAddress.TryParse(host, out var ipAddress))
            _endPoints.Add(new(ipAddress, port));
        else
            Console.WriteLine($"Warning: Could not parse host '{host}'. Only IP addresses are supported.");
    }

    public void AddEndPoints(in IList<IPEndPoint> endPoints)
    {
        _endPoints.AddRange(endPoints);
    }

    public void SendMessage(DeviceInputData inputData)
    {
        if(_endPoints.Count == 0
            || inputData?.Data is null)
            return;

        var sendData = MessagePackSerializer.Serialize(inputData.Data);
        if (sendData is null or [])
            return;

        foreach (var endPoint in _endPoints)
        {
            try
            {
                _sender.Send(sendData, sendData.Length, endPoint);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Error: Failed to send input event to {endPoint}: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _sender.Close();
        _sender.Dispose();
    }
}
