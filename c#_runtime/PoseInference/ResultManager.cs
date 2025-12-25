using System;
using System.Windows.Media.Animation;
using HumanLandmarks;


namespace KinectPoseInferencer.PoseInference;

public class ResultManager : IDisposable
{
    readonly UdpResultReceiver     _receiver;
    readonly ReceiverEventSettings _receiverSetting;

    public HolisticLandmarks Result { get; private set; } = new();

    public ResultManager(
        UdpResultReceiver     receiver,
        ReceiverEventSettings receiverSetting
        )
    {
        _receiver        = receiver ?? throw new ArgumentNullException(nameof(receiver));
        _receiverSetting = receiverSetting;

        if (_receiverSetting.HasFlag(ReceiverEventSettings.Body))      _receiver.PoseReceived     += UpdateBody;
        if (_receiverSetting.HasFlag(ReceiverEventSettings.LeftHand))  _receiver.ReceiveLeftHand  += UpdateLeftHand;
        if (_receiverSetting.HasFlag(ReceiverEventSettings.RightHand)) _receiver.ReceiveRightHand += UpdateRightHand;
        if (_receiverSetting.HasFlag(ReceiverEventSettings.Face))      _receiver.ReceiveFace      += UpdateFace;
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
        if (_receiverSetting.HasFlag(ReceiverEventSettings.Body))      _receiver.PoseReceived     -= UpdateBody;
        if (_receiverSetting.HasFlag(ReceiverEventSettings.LeftHand))  _receiver.ReceiveLeftHand  -= UpdateLeftHand;
        if (_receiverSetting.HasFlag(ReceiverEventSettings.RightHand)) _receiver.ReceiveRightHand -= UpdateRightHand;
        if (_receiverSetting.HasFlag(ReceiverEventSettings.Face))      _receiver.ReceiveFace      -= UpdateFace;
    }
}
