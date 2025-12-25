using HumanLandmarks;

namespace KinectPoseInferencer.Core.PoseInference;

public interface ILandmarkUser
{
    void Process(in HolisticLandmarks landmark);
}
