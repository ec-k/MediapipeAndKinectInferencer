namespace KinectPoseInferencer.Core.Playback;

public interface IInputLogReader: IAsyncDisposable
{
    Task<bool> LoadMetaFileAsync(string filePath);
    Task<bool> LoadLogFile(string filePath);
    Task Rewind();
    bool TryRead(long targetTime, out IList<DeviceInputData> results);
}
