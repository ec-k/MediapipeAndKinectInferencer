using HumanLandmarks;

namespace KinectPoseInferencer.PoseInference.Filters
{
    public interface ILandmarkFilter
    {
        Landmark Apply(Landmark position);
    }
}
