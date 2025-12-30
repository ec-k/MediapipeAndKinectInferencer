using KinectPoseInferencer.Core.PoseInference;
using System.Net;

namespace KinectPoseInferencer.Core;

public record CoreSettings()
{
    public string MmfFileName { get; init; } = string.Empty;
    public string ReceiverSettings { get; init; } = "Face, LeftHand, RightHand";
    public string ResultReceiverEndPoint { get; init; } = "127.0.0.1:9001";
    public string LandmarkSenderEndPoint { get; init; } = "127.0.0.1:22000";
    public string[] InputEventSenderEndPoints { get; init; } = Array.Empty<string>();
    public int AppFrameRate { get; init; } = 60;
    public FilterSettings FilterSettings { get; init; } = new();

    public IPEndPoint GetLandmarkSenderEndPoint() => IPEndPoint.Parse(LandmarkSenderEndPoint);

    public IPEndPoint GetResultReceiverEndPoint() => IPEndPoint.Parse(ResultReceiverEndPoint);

    public IPEndPoint[] GetInputEventSenderEndPoints() =>
        InputEventSenderEndPoints.Select(IPEndPoint.Parse).ToArray();

    public ReceiverEventSettings GetReceiverSettings() =>
        Enum.Parse<ReceiverEventSettings>(ReceiverSettings, ignoreCase: true);
}

public record FilterSettings
{
    public OneEuroFilterSettings OneEuroFilter { get; init; } = new();
}

public record OneEuroFilterSettings
{
    public float MinCutoff { get; init; } = 1.0f;
    public float Slope { get; init; } = 0.007f;
    public float DCutoff { get; init; } = 1.0f;
}
