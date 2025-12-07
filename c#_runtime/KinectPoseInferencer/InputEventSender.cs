using System;
using System.Net.Sockets;
using Google.Protobuf;
using InputLogging;
using KinectPoseInferencer.Playback;


namespace KinectPoseInferencer;

public class InputEventSender: IDisposable
{
    UdpClient _sender = new();

    public InputEventSender(string host, int port)
    {
        _sender.Connect(host, port);
    }

    public void Connect(string host, int port)
    {
        if(_sender.Client.Connected)
            _sender.Close();

        _sender.Connect(host, port);
    }

    public void SendKeyboardEvent(InputLogEvent inputEvent)
    {
        if (!_sender.Client.Connected)
            return;
        if (inputEvent is null
            || inputEvent.Data is null
            || inputEvent.EventType is not InputEventType.Keyboard)
            return;

        var keybordEventMessage = ComposeMessage(inputEvent);
        var data = keybordEventMessage.ToByteArray();
        _sender.Send(data, data.Length);
    }

    public KeyboardEventDataProto ComposeMessage(InputLogEvent inputEvent)
    {
        if (inputEvent is null)
            return new();

        return new()
        {
            RawStopwatchTimestamp = inputEvent.Data.RawStopwatchTimestamp,
            VirtualKeyCode = (uint)(inputEvent.Data as KeyboardEventData)?.VirtualKeyCode,
            ModifiersFlags = (uint)(inputEvent.Data as KeyboardEventData)?.ModifiersFlags,
            IsKeyDown = (inputEvent.Data as KeyboardEventData)?.IsKeyDown ?? false
        };
    }

    public void Dispose()
    {
        _sender.Close();
        _sender.Dispose();
    }
}
