using HumanLandmarks;
using HumanLandmarks.Log;

namespace KinectPoseInferencer.Logging
{
    internal interface IResultLogWriter
    {
        void Initialize(string filePath, LogHeader header);

        /// <summary>
        /// Writes the pose landmarks of one frame to the log file.
        /// </summary>
        /// <param name="landmarks"></param>
        void Write(HolisticLandmarks landmarks, ulong timestampMs, uint frameNumber);
    }
}
