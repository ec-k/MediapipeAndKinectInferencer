namespace KinectPoseInferencer.Core.Playback;

public interface IInputLogReader: IAsyncDisposable
{
    Task<bool> LoadLogFile(string filePath);
    Task Rewind();
    bool TryRead(TimeSpan targetTime, out IList<DeviceInputData> results);
}
