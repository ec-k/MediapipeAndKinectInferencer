using CliWrap;
using System.Windows;

namespace KinectPoseInferencer.Core;

public class MediaPipeProcessManager
{
    readonly IMediaPipeConfiguration _config;

    CancellationTokenSource _gracefulCts = new();
    CancellationTokenSource _forcefulCts = new();
    readonly TimeSpan _gracefulStopTimeoutSec = TimeSpan.FromSeconds(3);

    public MediaPipeProcessManager(IMediaPipeConfiguration config)
    {
        _config = config;
    }

    public async Task StartMediapipeProcess()
    {
        try
        {
            var exePath = _config.ExecutablePath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                MessageBox.Show("MediaPipe Inferencer .exe path is not specified at MediaPipeSettings:ExecutablePath.");
                return;
            }

            var fullPath = Path.GetFullPath(exePath, AppContext.BaseDirectory);

            if (!File.Exists(fullPath))
            {
                MessageBox.Show($"MediaPipe Inferencer .exe is not found: {fullPath}");
                return;
            }

            await Cli.Wrap(fullPath)
                     .ExecuteAsync(_forcefulCts.Token, _gracefulCts.Token);
        }
        catch (OperationCanceledException) { }
    }

    public void StopProcess()
    {
        _gracefulCts.Cancel();
        _forcefulCts.CancelAfter(_gracefulStopTimeoutSec);
    }
}
