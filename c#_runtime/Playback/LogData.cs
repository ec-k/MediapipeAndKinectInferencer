using System.Text.Json.Serialization;

namespace KinectPoseInferencer.Playback;


public class LogMetadata
{
    [JsonPropertyName("SystemStopwatchTimestampAtKinectStart")]
    public long SystemStopwatchTimestampAtKinectStart { get; set; }

    [JsonPropertyName("FirstKinectDeviceTimestampUs")]
    public long FirstKinectDeviceTimestampUs { get; set; }
}


public class InputEventData
{
    [JsonPropertyName("RawStopwatchTimestamp")]
    public ulong RawStopwatchTimestamp { get; set; }

    [JsonPropertyName("VirtualKeyCode")]
    public int? VirtualKeyCode { get; set; }
    [JsonPropertyName("ModifiersFlags")]
    public int? ModifiersFlags { get; set; }
    [JsonPropertyName("IsKeyDown")]
    public bool? IsKeyDown { get; set; }

    [JsonPropertyName("X")]
    public int? X { get; set; }
    [JsonPropertyName("Y")]
    public int? Y { get; set; }
    [JsonPropertyName("DeltaX")]
    public int? DeltaX { get; set; }
    [JsonPropertyName("DeltaY")]
    public int? DeltaY { get; set; }
    [JsonPropertyName("MouseData")]
    public int? MouseData { get; set; }
    [JsonPropertyName("IsMouseButtonDown")]
    public bool? IsMouseButtonDown { get; set; }
}


public class InputLogEvent : IInputLogEvent
{
    [JsonPropertyName("Timestamp")]
    public double Timestamp { get; set; }

    [JsonPropertyName("EventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("Data")]
    public InputEventData Data { get; set; } = new InputEventData();

    long IInputLogEvent.RawStopwatchTimestamp => (long)Data.RawStopwatchTimestamp;
}
