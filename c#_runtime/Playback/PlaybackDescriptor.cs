using K4AdotNet.Record;

namespace KinectPoseInferencer.Playback;

public class PlaybackDescriptor
{
    public string VideoFilePath { get; set; }
    public RecordConfiguration RecordConfig {get;set;}

    public PlaybackDescriptor(
        string videoFilePath,
        RecordConfiguration recordConfig)
    {
        VideoFilePath = videoFilePath;
        RecordConfig = recordConfig;
    }
}
