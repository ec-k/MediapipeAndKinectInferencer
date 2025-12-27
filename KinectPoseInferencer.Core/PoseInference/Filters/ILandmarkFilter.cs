using HumanLandmarks;

namespace KinectPoseInferencer.Core.PoseInference.Filters
{
    public interface ILandmarkFilter
    {
        Landmark Apply(in Landmark position);
    }
}
