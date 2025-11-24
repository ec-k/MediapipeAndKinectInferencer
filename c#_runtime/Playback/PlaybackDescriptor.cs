using K4AdotNet.Record;

namespace KinectPoseInferencer.Playback;

public class PlaybackDescriptor
{
    public string VideoFilePath { get; set; }

    public PlaybackDescriptor(
        string videoFilePath)
    {
        VideoFilePath = videoFilePath;
    }
}
