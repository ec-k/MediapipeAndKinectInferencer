using CliWrap;

namespace KinectPoseInferencer.Core;

public class MediaPipeProcessManager: IDisposable
{
    readonly IMediaPipeConfiguration _config;
    readonly string _mmfFilePath;

    CancellationTokenSource? _gracefulCts;
    CancellationTokenSource? _forcefulCts;
    readonly TimeSpan _gracefulStopTimeoutSec = TimeSpan.FromSeconds(3);

    public MediaPipeProcessManager(IMediaPipeConfiguration config, string mmfFilePath)
    {
        _config      = config      ?? throw new ArgumentNullException(nameof(config));
        _mmfFilePath = mmfFilePath ?? throw new ArgumentNullException(nameof(mmfFilePath));
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
            var args = new List<string>() { "--mmap_file_path", _mmfFilePath };
            if (_config.EnablePoseInference)       args.Add("--enable_pose_inference");
            if (_config.EnableVisualizationWindow) args.Add("--enable_visualization_window");

            await Cli.Wrap(fullPath)
                     .WithArguments(args)
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
