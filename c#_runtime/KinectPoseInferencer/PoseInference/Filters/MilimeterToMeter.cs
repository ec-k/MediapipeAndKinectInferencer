using System.Numerics;

namespace KinectPoseInferencer.PoseInference.Filters
{
    internal class MilimeterToMeter: IPositionFilter
    {
        /// <summary>
        /// // Convert position from millimeters to meters
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public Vector3 Apply(Vector3 position)
        {
            return new Vector3(
                position.X / 1000f,
                position.Y / 1000f,
                position.Z / 1000f);
        }
    }
}
