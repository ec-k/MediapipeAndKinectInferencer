using KinectPoseInferencer.Core.PoseInference;
using System.Net;

namespace KinectPoseInferencer.Core;

public record CoreSettings
{
    public string MmfFileName { get; set; } = string.Empty;
    public string ReceiverSettings { get; set; } = "Face, LeftHand, RightHand";
    public string ResultReceiverEndPoint { get; set; } = "127.0.0.1:9001";
    public string LandmarkSenderEndPoint { get; set; } = "127.0.0.1:22000";
    public string[] InputEventSenderEndPoints { get; set; } = Array.Empty<string>();

    public IPEndPoint GetLandmarkSenderEndPoint() => IPEndPoint.Parse(LandmarkSenderEndPoint);

    public IPEndPoint GetResultReceiverEndPoint() => IPEndPoint.Parse(ResultReceiverEndPoint);

    public IPEndPoint[] GetInputEventSenderEndPoints() =>
        InputEventSenderEndPoints.Select(IPEndPoint.Parse).ToArray();

    public ReceiverEventSettings GetReceiverSettings() =>
        Enum.Parse<ReceiverEventSettings>(ReceiverSettings, ignoreCase: true);
}
