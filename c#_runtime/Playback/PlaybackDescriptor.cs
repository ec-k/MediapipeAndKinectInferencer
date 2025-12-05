using K4AdotNet.Record;

namespace KinectPoseInferencer.Playback;

public class PlaybackDescriptor
{
    public string VideoFilePath { get; }
    public string? InputLogFilePath { get; }
    public string? MetadataFilePath { get; }

    public PlaybackDescriptor(
        string videoFilePath,
        string? inputLogFilePath,
        string? metadataFilePath)
    {
        VideoFilePath = videoFilePath;
        InputLogFilePath = inputLogFilePath;
        MetadataFilePath = metadataFilePath;
    }
}
