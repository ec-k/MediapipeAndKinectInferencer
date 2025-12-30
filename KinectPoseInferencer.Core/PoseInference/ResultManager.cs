using HumanLandmarks;
using System.Windows.Media;


namespace KinectPoseInferencer.Core.PoseInference;

public class ResultManager : IDisposable
{
    readonly UdpResultReceiver   _receiver;
    public ReceiverEventSettings ReceiverSetting { get; private set; }

    public HolisticLandmarks Result { get; private set; } = new();

    public ResultManager(
        UdpResultReceiver     receiver,
        ReceiverEventSettings receiverSetting
        )
    {
        _receiver        = receiver ?? throw new ArgumentNullException(nameof(receiver));
        ReceiverSetting  = receiverSetting;

        if (ReceiverSetting.HasFlag(ReceiverEventSettings.Body))      _receiver.PoseReceived     += UpdateBody;
        if (ReceiverSetting.HasFlag(ReceiverEventSettings.LeftHand))  _receiver.ReceiveLeftHand  += UpdateLeftHand;
        if (ReceiverSetting.HasFlag(ReceiverEventSettings.RightHand)) _receiver.ReceiveRightHand += UpdateRightHand;
        if (ReceiverSetting.HasFlag(ReceiverEventSettings.Face))      _receiver.ReceiveFace      += UpdateFace;
    }

    public void UpdateSettings(ReceiverEventSettings settings)
    {
        var added = settings & ~ReceiverSetting;
        var removed = ReceiverSetting & ~settings;

        if (added.HasFlag(ReceiverEventSettings.Body))      _receiver.PoseReceived += UpdateBody;
        if (added.HasFlag(ReceiverEventSettings.LeftHand))  _receiver.ReceiveLeftHand += UpdateLeftHand;
        if (added.HasFlag(ReceiverEventSettings.RightHand)) _receiver.ReceiveRightHand += UpdateRightHand;
        if (added.HasFlag(ReceiverEventSettings.Face))      _receiver.ReceiveFace += UpdateFace;

        if (removed.HasFlag(ReceiverEventSettings.Body))      _receiver.PoseReceived -= UpdateBody;
        if (removed.HasFlag(ReceiverEventSettings.LeftHand))  _receiver.ReceiveLeftHand -= UpdateLeftHand;
        if (removed.HasFlag(ReceiverEventSettings.RightHand)) _receiver.ReceiveRightHand -= UpdateRightHand;
        if (removed.HasFlag(ReceiverEventSettings.Face))      _receiver.ReceiveFace -= UpdateFace;

        ReceiverSetting = settings;
    }

    public void UpdateLeftHand(HandLandmarks result)
    {
        Result.LeftHandLandmarks = result;
    }
    public void UpdateRightHand(HandLandmarks result)
    {
        Result.RightHandLandmarks = result;
    }
    public void UpdateBody(MediaPipePoseLandmarks result)
    {
        Result.MediaPipePoseLandmarks = result;
    }
    public void UpdateFace(FaceResults result)
    {
        Result.FaceResults = result;
    }

    public void Dispose()
    {
        if (ReceiverSetting.HasFlag(ReceiverEventSettings.Body))      _receiver.PoseReceived     -= UpdateBody;
        if (ReceiverSetting.HasFlag(ReceiverEventSettings.LeftHand))  _receiver.ReceiveLeftHand  -= UpdateLeftHand;
        if (ReceiverSetting.HasFlag(ReceiverEventSettings.RightHand)) _receiver.ReceiveRightHand -= UpdateRightHand;
        if (ReceiverSetting.HasFlag(ReceiverEventSettings.Face))      _receiver.ReceiveFace      -= UpdateFace;
    }
}
