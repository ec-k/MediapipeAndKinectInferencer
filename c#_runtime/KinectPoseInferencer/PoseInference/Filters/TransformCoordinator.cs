using System.Numerics;

namespace KinectPoseInferencer.PoseInference.Filters
{
    internal class TransformCoordinator: IPositionFilter
    {
        public Vector3 Apply(Vector3 position)
            => -position;
    }
}
