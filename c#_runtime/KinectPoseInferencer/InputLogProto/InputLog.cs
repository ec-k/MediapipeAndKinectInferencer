using System;
using MessagePack;


namespace KinectPoseInferencer.InputLogProto;

[MessagePackObject]
public class LogMetadata
{
    [Key(0)]　public ulong SystemStopwatchTimestampAtKinectStart { get; set; }
    [Key(1)]　public ulong FirstKinectDeviceTimestampUs { get; set; }
}

[MessagePack.Union(0, typeof(KeyboardEventDataProto))]
[MessagePack.Union(1, typeof(MouseEventDataProto))]
public interface IDeviceInput { }


[MessagePackObject]
public class KeyboardEventDataProto: IDeviceInput
{
    [Key(0)] public ulong            RawStopwatchTimestamp { get; set; }
    [Key(1)] public uint             VirtualKeyCode { get; set; }
    [Key(2)] public ModifierKeyState ModifiersFlags { get; set; }
    [Key(3)] public bool             IsKeyDown { get; set; }

    [Flags]
    public enum ModifierKeyState
    {
        NONE    = 0,
        ALT     = 1,
        CONTROL = 2,
        SHIFT   = 4,
    }
}

[MessagePackObject]
public class MouseEventDataProto: IDeviceInput
{
    [Key(0)] public ulong RawStopwatchTimestamp { get; set; }
    [Key(1)] public MouseButton Button { get; set; }
    [Key(2)] public int X { get; set; }
    [Key(3)] public int Y {get;set;}
    [Key(4)] public int WheelDelta {get;set;}
    [Key(5)] public bool IsButtonDown {get;set;}
    [Key(6)] public bool IsMouseMoving {get;set;}
    [Key(7)] public bool IsWheelMoving {get;set;}

    public enum MouseButton
    {
        NONE     = 0,
        LEFT     = 1,
        RIGHT    = 2,
        MIDDLE   = 3,
        XBUTTON1 = 4,
        XBUTTON2 = 5,
    }
}
