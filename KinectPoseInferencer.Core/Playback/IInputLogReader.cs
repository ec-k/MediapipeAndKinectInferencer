namespace KinectPoseInferencer.Core.Playback;

public interface IInputLogReader: IAsyncDisposable
{
    public TimeSpan FirstFrameTime { set; }
    Task<bool> LoadLogFile(string filePath);
    Task RewindAsync();
    Task SeekAsync(TimeSpan position);
    bool TryRead(TimeSpan targetTime, out IList<DeviceInputData> results);
}
