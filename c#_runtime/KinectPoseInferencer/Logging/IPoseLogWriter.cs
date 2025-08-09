using HumanLandmarks;

namespace KinectPoseInferencer.Logging
{
    internal interface IPoseLogWriter
    {
        void Initialize(string filePath);

        /// <summary>
        /// Writes the pose landmarks of one frame to the log file.
        /// </summary>
        /// <param name="poseLandmarks"></param>
        void Write(KinectPoseLandmarks poseLandmarks);
    }
}
