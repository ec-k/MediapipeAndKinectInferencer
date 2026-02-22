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
    Task? _producerLoopTask;
    CancellationTokenSource _producerLoopCts = new();
    readonly int _taskCancelTimeoutSec = 2;
    bool _isEOF = false;
    readonly SemaphoreSlim _loopSignal = new(0);

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

    public async Task Configure(PlaybackDescriptor descriptor, CancellationToken token)
    {
        if (string.IsNullOrEmpty(descriptor.VideoFilePath))
            throw new ArgumentNullException(nameof(descriptor.VideoFilePath));

        if (_producerLoopTask is not null)
        {
            await StopProducerLoop();
            _playback.Value?.Dispose();
        }

        await Task.Run(() => LoadVideo(descriptor.VideoFilePath), token);

        ClearBuffer();
        _isEOF = false;

        var tcs = new TaskCompletionSource();
        _commandQueue.Enqueue(new(tcs, TimeSpan.Zero));
        _loopSignal.Release();

        _producerLoopCts?.Dispose();
        _producerLoopCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _producerLoopTask = Task.Run(() => ProducerLoop(_producerLoopCts.Token).ConfigureAwait(false));

        await tcs.Task.ConfigureAwait(false);
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

    public async Task SeekAsync(TimeSpan position)
    {
        if (Playback.CurrentValue is null) return;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _commandQueue.Enqueue(new(tcs, position));

        _loopSignal.Release();

        await tcs.Task.ConfigureAwait(false);
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

    async Task ProducerLoop(CancellationToken token)
    {
        Task<bool>? waitWriterTask = null;
        Task? waitSignalTask = null;

        try
        {
            while (!token.IsCancellationRequested)
            {
                ProcessCommands();

                if (_isEOF && _commandQueue.IsEmpty)
                {
                    waitWriterTask = null;

                    if (waitSignalTask is null)
                        waitSignalTask = _loopSignal.WaitAsync(token);
                    await waitSignalTask;
                    waitSignalTask = null;

                    continue;
                }

                if (waitWriterTask is null)
                    waitWriterTask = _frameChannel.Writer.WaitToWriteAsync(token).AsTask();
                if (waitSignalTask is null)
                    waitSignalTask = _loopSignal.WaitAsync(token);
                var completedTask = await Task.WhenAny(waitWriterTask, waitSignalTask);

                if (completedTask == waitSignalTask)
                {
                    waitSignalTask = null;
                    continue;
                }
                if (!await waitWriterTask) break;
                waitWriterTask = null;

                if (!_commandQueue.IsEmpty) continue;

                PlaybackFrame frame = default;
                try
                {
                    frame = ReadNextFrame();
                    if (_isEOF) continue;
                    if (frame.Capture is null)
                    {
                        await Task.Delay(1, token);
                        continue;
                    }

                    if (!_frameChannel.Writer.TryWrite(frame))
                        frame.Capture?.Dispose();
                }
                catch (Exception ex)
                {
                    frame.Capture?.Dispose();
                    _logger.LogError("Producer Error: {Message}", ex.Message);
                    await Task.Delay(100, token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
    }

    async Task StopProducerLoop()
    {
        if (_producerLoopCts is not null && _producerLoopTask is not null)
        {
            _producerLoopCts.Cancel();
            try
            {
                if (!_producerLoopTask.IsCompleted)
                {
                    await _producerLoopTask.WaitAsync(TimeSpan.FromSeconds(_taskCancelTimeoutSec));
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
            {
                // Expected on cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError("Warning: FrameReadingLoop did not terminate gracefully within timeout. Error: {Message}", ex.Message);
            }
            finally
            {
                _producerLoopTask = null;
                _producerLoopCts.Dispose();
                _producerLoopCts = new();
            }
        }
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

        // Return capture even without DepthImage (for color image display)
        if (capture is null)
            return result;

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

    public async ValueTask DisposeAsync()
    {
        await StopProducerLoop();

        _playback.Value?.Dispose();
        _playback.Dispose();
        _producerLoopCts.Dispose();
    }
}
