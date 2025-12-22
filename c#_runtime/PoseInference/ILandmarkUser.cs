using HumanLandmarks;

namespace KinectPoseInferencer.PoseInference;

public interface ILandmarkUser
{
    void Process(in HolisticLandmarks landmark);
}
