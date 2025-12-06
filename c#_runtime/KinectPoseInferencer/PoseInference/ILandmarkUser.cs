using HumanLandmarks;

namespace KinectPoseInferencer.PoseInference;

public interface ILandmarkUser
{
    void Process(HolisticLandmarks landmark);
}
