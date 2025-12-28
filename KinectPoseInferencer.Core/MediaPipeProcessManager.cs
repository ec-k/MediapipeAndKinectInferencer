using CliWrap;

namespace KinectPoseInferencer.Core;

public class MediaPipeProcessManager: IDisposable
{
    readonly IMediaPipeConfiguration _config;

    CancellationTokenSource? _gracefulCts;
    CancellationTokenSource? _forcefulCts;
    readonly TimeSpan _gracefulStopTimeoutSec = TimeSpan.FromSeconds(3);

    public MediaPipeProcessManager(IMediaPipeConfiguration config)
    {
        _config = config;
    }

    public async Task StartMediapipeProcessAsync(CancellationToken token)
    {
        StopProcess();

        _gracefulCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _forcefulCts = new();

        var exePath = _config.ExecutablePath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            Console.Error.WriteLine("MediaPipe Inferencer .exe path is not specified at MediaPipeSettings:ExecutablePath.");
            return;
        }

        var fullPath = Path.GetFullPath(exePath, AppContext.BaseDirectory);
        if (!File.Exists(fullPath))
        {
            Console.Error.WriteLine($"MediaPipe Inferencer .exe is not found: {fullPath}");
            return;
        }

        try
        {
            await Cli.Wrap(fullPath)
                     .ExecuteAsync(_forcefulCts.Token, _gracefulCts.Token);
        }
        catch (OperationCanceledException) { }
    }

    public void StopProcess()
    {
        _gracefulCts?.Cancel();
        _forcefulCts?.CancelAfter(_gracefulStopTimeoutSec);
    }

    public void Dispose()
    {
        StopProcess();
        _gracefulCts?.Dispose();
        _forcefulCts?.Dispose();
    }
}
