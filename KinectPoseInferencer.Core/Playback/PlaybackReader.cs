using K4AdotNet.Sensor;
using Microsoft.Extensions.Logging;
using R3;
using K4APlayback = K4AdotNet.Record.Playback;
using K4APlaybackSeekOrigin = K4AdotNet.Record.PlaybackSeekOrigin;

namespace KinectPoseInferencer.Core.Playback;

/// <summary>
/// Simplified PlaybackReader - direct read from Playback without buffering.
/// </summary>
public class PlaybackReader : IPlaybackReader
{
    public ReadOnlyReactiveProperty<K4APlayback?> Playback => _playback;
    public ReadOnlyReactiveProperty<TimeSpan> InitialDeviceTimestamp => _initialDeviceTimestamp;

    readonly ReactiveProperty<K4APlayback?> _playback = new();
    readonly ReactiveProperty<TimeSpan> _initialDeviceTimestamp = new();
    readonly ILogger<PlaybackReader> _logger;

    public PlaybackReader(ILogger<PlaybackReader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Configure(PlaybackDescriptor descriptor, CancellationToken token)
    {
        if (string.IsNullOrEmpty(descriptor.VideoFilePath))
            throw new ArgumentNullException(nameof(descriptor.VideoFilePath));

        // Dispose existing playback
        _playback.Value?.Dispose();

        // Load video synchronously (wrapped in Task.Run for non-blocking)
        return Task.Run(() =>
        {
            LoadVideo(descriptor.VideoFilePath);
            SeekInternal(TimeSpan.Zero);
        }, token);
    }

    void LoadVideo(string videoFilePath)
    {
        var playback = new K4APlayback(videoFilePath);
        playback.SetColorConversion(ImageFormat.ColorBgra32);

        // Get initial timestamp
        if (playback.TryGetNextCapture(out var capture))
        {
            using (capture)
            {
                _initialDeviceTimestamp.Value = capture.DepthImage?.DeviceTimestamp
                    ?? capture.ColorImage?.DeviceTimestamp
                    ?? TimeSpan.Zero;
            }
            playback.SeekTimestamp(TimeSpan.Zero, K4APlaybackSeekOrigin.Begin);
        }

        _playback.Value = playback;
    }

    public Task RewindAsync() => SeekAsync(TimeSpan.Zero);

    public Task SeekAsync(TimeSpan position)
    {
        return Task.Run(() => SeekInternal(position));
    }

    void SeekInternal(TimeSpan position)
    {
        if (_playback.Value is not K4APlayback playback) return;

        _logger.LogInformation("Seeking to {Position}", position);
        playback.SeekTimestamp(position, K4APlaybackSeekOrigin.Begin);
    }

    /// <summary>
    /// Directly read next capture from playback.
    /// Caller is responsible for disposing the capture.
    /// </summary>
    public bool TryRead(TimeSpan targetFrameTime, out Capture? capture, out ImuSample? imuSample)
    {
        capture = null;
        imuSample = null;

        if (_playback.Value is not K4APlayback playback)
            return false;

        try
        {
            if (!playback.TryGetNextCapture(out var cap))
                return false;

            if (cap is null)
                return false;

            capture = cap;

            if (playback.TryGetNextImuSample(out var imu))
                imuSample = imu;

            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (K4AdotNet.Record.PlaybackException ex)
        {
            _logger.LogWarning("PlaybackException in TryRead: {Message}", ex.Message);
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        _playback.Value?.Dispose();
        _playback.Dispose();
        return ValueTask.CompletedTask;
    }
}
