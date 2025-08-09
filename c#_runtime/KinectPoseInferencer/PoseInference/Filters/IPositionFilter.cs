using System.Numerics;

namespace KinectPoseInferencer.PoseInference.Filters
{
    internal interface IPositionFilter
    {
        Vector3 Apply(Vector3 position);
    }
}
