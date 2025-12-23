using KinectPoseInferencer.InputLogProto;
using KinectPoseInferencer.Playback;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;


namespace KinectPoseInferencer;

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

    public void SendMessage(InputLogEvent inputEvent)
    {
        if(_endPoints.Count == 0)
            return;

        if (inputEvent is null
            || inputEvent.Data is null
            || inputEvent.EventType is InputEventType.Unknown)
            return;

        IDeviceInput? data = inputEvent.Data switch
        {
            KeyboardEventData => ComposeKeyboardEventMessage(inputEvent),
            MouseEventData    => ComposeMouseEventMessage(inputEvent),
            _                 => null
        };
        var sendData = MessagePackSerializer.Serialize(data);

        if (sendData is null or [])
        {
            return;
        }

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

    public KeyboardEventDataProto ComposeKeyboardEventMessage(InputLogEvent inputEvent)
        => inputEvent switch
        {
            { Data: KeyboardEventData { VirtualKeyCode: int, ModifiersFlags: int } data } => new()
            {
                RawStopwatchTimestamp = data.RawStopwatchTimestamp,
                VirtualKeyCode        = (uint)data.VirtualKeyCode,
                ModifiersFlags        = (KeyboardEventDataProto.ModifierKeyState)data.ModifiersFlags,
                IsKeyDown             = data.IsKeyDown ?? false
            },
            _ => new()
        };

    public MouseEventDataProto ComposeMouseEventMessage(InputLogEvent inputEvent)
        => inputEvent switch
        {
            { Data: MouseEventData data } => new()
            {
                RawStopwatchTimestamp = data.RawStopwatchTimestamp,
                X                     = data.X ?? 0,
                Y                     = data.Y ?? 0,
                WheelDelta            = 0,                                  // HACK: This should be set properly if needed.
                IsButtonDown          = data.IsMouseButtonDown ?? false,
                IsMouseMoving         = data.IsMouseMoving ?? false,
                IsWheelMoving         = data.IsWheelMoving ?? false
            },
            _ => new()
        };

    public void Dispose()
    {
        _sender.Close();
        _sender.Dispose();
    }
}
