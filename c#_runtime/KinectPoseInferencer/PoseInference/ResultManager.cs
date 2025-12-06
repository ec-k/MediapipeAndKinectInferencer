using System;
using HumanLandmarks;


namespace KinectPoseInferencer.PoseInference;

public class ResultManager : IDisposable
{
    readonly UdpResultReceiver _receiver;

    public HolisticLandmarks Result { get; private set; } = new();

    public ResultManager(UdpResultReceiver receiver)
    {
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));

        _receiver.PoseReceived += UpdateBody;
        _receiver.ReceiveLeftHand += UpdateLeftHand;
        _receiver.ReceiveRightHand += UpdateRightHand;
        _receiver.ReceiveFace += UpdateFace;
    }

    public void UpdateLeftHand(HandLandmarks result)
    {
        Result.LeftHandLandmarks = result;
    }
    public void UpdateRightHand(HandLandmarks result)
    {
        Result.RightHandLandmarks = result;
    }

    public void UpdateBody(KinectPoseLandmarks result)
    {
        Result.PoseLandmarks = result;
    }

    public void UpdateFace(FaceResults result)
    {
        Result.FaceResults = result;
    }

    public void Dispose()
    {
        _receiver.PoseReceived -= UpdateBody;
        _receiver.ReceiveLeftHand -= UpdateLeftHand;
        _receiver.ReceiveRightHand -= UpdateRightHand;
        _receiver.ReceiveFace -= UpdateFace;
    }
}
