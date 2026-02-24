using K4AdotNet.Sensor;
using Microsoft.Extensions.Logging;
using R3;
using System.Collections.Concurrent;
using System.Threading.Channels;
using K4APlayback = K4AdotNet.Record.Playback;
using K4APlaybackSeekOrigin = K4AdotNet.Record.PlaybackSeekOrigin;

namespace KinectPoseInferencer.Core.Playback;

public record struct PlaybackFrame(
    Capture? Capture,
    ImuSample? ImuSample
);

public class PlaybackReader : IPlaybackReader
{
    public ReadOnlyReactiveProperty<K4APlayback?> Playback => _playback;
    public ReadOnlyReactiveProperty<TimeSpan> InitialDeviceTimestamp => _initialDeviceTimestamp;

    readonly ReactiveProperty<K4APlayback?> _playback = new();
    readonly ReactiveProperty<TimeSpan> _initialDeviceTimestamp = new();

    record struct SeekRequest(
        TaskCompletionSource Tcs,
        TimeSpan? Position = null
        );
    ConcurrentQueue<SeekRequest> _commandQueue = new();

    Channel<PlaybackFrame> _frameChannel;
    Thread? _producerThread;
    volatile bool _stopRequested = false;
    bool _isEOF = false;
    readonly ManualResetEventSlim _loopSignal = new(false);
    TimeSpan _lastFrameTimestamp = TimeSpan.Zero;

    readonly ILogger<PlaybackReader> _logger;

    public PlaybackReader(
        ILogger<PlaybackReader> logger,
        int bufferSize = 300)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _frameChannel = Channel.CreateBounded<PlaybackFrame>(
            new BoundedChannelOptions(bufferSize)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    public Task Configure(PlaybackDescriptor descriptor, CancellationToken token)
    {
        if (string.IsNullOrEmpty(descriptor.VideoFilePath))
            throw new ArgumentNullException(nameof(descriptor.VideoFilePath));

        if (_producerThread is not null)
        {
            StopProducerThread();
            _playback.Value?.Dispose();
        }

        ClearBuffer();
        _isEOF = false;
        _stopRequested = false;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _commandQueue.Enqueue(new(tcs, TimeSpan.Zero));
        _loopSignal.Set();

        // Start dedicated thread for K4AdotNet operations
        _producerThread = new Thread(() => ProducerLoop(descriptor.VideoFilePath))
        {
            IsBackground = true,
            Name = "PlaybackReader.ProducerLoop"
        };
        _producerThread.Start();

        return tcs.Task;
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

    public async Task RewindAsync() => await SeekAsync(TimeSpan.Zero);

    public Task SeekAsync(TimeSpan position)
    {
        if (Playback.CurrentValue is null) return Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _commandQueue.Enqueue(new(tcs, position));

        _loopSignal.Set();

        return tcs.Task;
    }

    public bool TryRead(TimeSpan targetFrameTime, out Capture? capture, out ImuSample? imuSample)
    {
        capture = null;
        imuSample = null;

        if (!_frameChannel.Reader.TryPeek(out var frame))
            return false;

        // Check timestamp using DepthImage or ColorImage
        TimeSpan? frameTimestamp = frame.Capture?.DepthImage?.DeviceTimestamp
            ?? frame.Capture?.ColorImage?.DeviceTimestamp;

        if (frameTimestamp is not TimeSpan ts || ts > targetFrameTime)
            return false;

        if (!_frameChannel.Reader.TryRead(out frame))
            return false;

        try
        {
            capture = frame.Capture?.DuplicateReference();
            imuSample = frame.ImuSample;

            return true;
        }
        finally
        {
            frame.Capture?.Dispose();
        }
    }

    void ProcessCommands()
    {
        while (_commandQueue.TryDequeue(out var request))
        {
            try
            {
                var position = request.Position;
                if (position.HasValue)
                    ProcessSeekAction(position.Value);

                // Warm-up: fill buffer with initial frames before signaling completion
                WarmUpBuffer();

                request.Tcs.SetResult();
            }
            catch (Exception ex)
            {
                request.Tcs.SetException(ex);
            }
        }
    }

    void ProcessSeekAction(TimeSpan position)
    {
        ClearBuffer();
        _isEOF = false;
        _lastFrameTimestamp = TimeSpan.Zero;

        if (Playback.CurrentValue is not null)
        {
            _logger.LogInformation("Seeking to {Position}", position);
            Playback.CurrentValue.SeekTimestamp(position, K4APlaybackSeekOrigin.Begin);
        }
    }

    void WarmUpBuffer()
    {
        const int warmUpFrameCount = 30;

        for (int i = 0; i < warmUpFrameCount && !_isEOF; i++)
        {
            var frame = ReadNextFrame();
            if (frame.Capture is null)
                continue;

            if (!_frameChannel.Writer.TryWrite(frame))
            {
                frame.Capture?.Dispose();
                break;
            }
        }
    }

    void ProducerLoop(string videoFilePath)
    {
        try
        {
            // Load video on this dedicated thread
            LoadVideo(videoFilePath);

            while (!_stopRequested)
            {
                ProcessCommands();

                if (_isEOF && _commandQueue.IsEmpty)
                {
                    _loopSignal.Wait(100);
                    _loopSignal.Reset();
                    continue;
                }

                // Check for signal (seek request etc.)
                if (_loopSignal.IsSet)
                {
                    _loopSignal.Reset();
                    continue;
                }

                if (!_commandQueue.IsEmpty) continue;

                PlaybackFrame frame = default;
                try
                {
                    frame = ReadNextFrame();
                    if (_isEOF) continue;
                    if (frame.Capture is null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    // Wait for space in buffer (synchronous)
                    while (!_stopRequested && !_frameChannel.Writer.TryWrite(frame))
                    {
                        if (_loopSignal.Wait(10))
                        {
                            _loopSignal.Reset();
                            // Signal received, dispose frame and recheck commands
                            frame.Capture?.Dispose();
                            frame = default;
                            break;
                        }
                    }

                    if (frame.Capture is not null && _stopRequested)
                        frame.Capture?.Dispose();
                }
                catch (Exception ex)
                {
                    frame.Capture?.Dispose();
                    _logger.LogError("Producer Error: {Message}", ex.Message);
                    Thread.Sleep(100);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("ProducerLoop terminated with error: {Message}", ex.Message);
        }
    }

    void StopProducerThread()
    {
        if (_producerThread is null) return;

        _stopRequested = true;
        _loopSignal.Set();

        if (_producerThread.IsAlive)
        {
            if (!_producerThread.Join(TimeSpan.FromSeconds(2)))
            {
                _logger.LogWarning("ProducerThread did not terminate gracefully within timeout");
            }
        }

        _producerThread = null;
    }

    PlaybackFrame ReadNextFrame()
    {
        var result = new PlaybackFrame();

        if (Playback?.CurrentValue is null)
            return result;

        var waitResult = Playback.CurrentValue.TryGetNextCapture(out var capture);

        if (!waitResult)
        {
            _isEOF = true;
            return result;
        }

        if (capture is null)
            return result;

        // Check timestamp progression - skip frames with non-increasing timestamps
        var currentTimestamp = capture.DepthImage?.DeviceTimestamp
            ?? capture.ColorImage?.DeviceTimestamp
            ?? TimeSpan.Zero;

        if (currentTimestamp != TimeSpan.Zero && currentTimestamp <= _lastFrameTimestamp)
        {
            // Timestamp went backwards or stayed same, skip this frame
            capture.Dispose();
            return result;
        }

        _lastFrameTimestamp = currentTimestamp;
        result = result with { Capture = capture };

        if (Playback.CurrentValue.TryGetNextImuSample(out var imuSample))
            result = result with { ImuSample = imuSample };

        return result;
    }

    void ClearBuffer()
    {
        if (_frameChannel is null) return;

        while (_frameChannel.Reader.TryRead(out var frame))
        {
            frame.Capture?.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        StopProducerThread();

        _playback.Value?.Dispose();
        _playback.Dispose();
        _loopSignal.Dispose();

        return ValueTask.CompletedTask;
    }
}
