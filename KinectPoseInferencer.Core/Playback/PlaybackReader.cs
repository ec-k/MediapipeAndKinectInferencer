using K4AdotNet.Sensor;
using R3;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace KinectPoseInferencer.Core.Playback;

public record struct PlaybackFrame(
    Capture? Capture,
    ImuSample? ImuSample
);

public class PlaybackReader : IPlaybackReader
{
    public ReadOnlyReactiveProperty<K4AdotNet.Record.Playback> Playback => _playback;
    ReactiveProperty<K4AdotNet.Record.Playback> _playback = new();

    enum Command { None, Seek, Rewind }
    record struct CommandRequest(
        Command Type,
        TaskCompletionSource Tcs,
        TimeSpan? Position = null
        );
    ConcurrentQueue<CommandRequest> _commandQueue = new();

    Channel<PlaybackFrame> _frameChannel;
    Task? _producerLoopTask;
    CancellationTokenSource _producerLoopCts = new();
    readonly int _taskCancelTimeoutSec = 2;
    bool _isEOF = false;
    readonly SemaphoreSlim _loopSignal = new(0);

    readonly ILogger<PlaybackReader> _logger;

    public PlaybackReader(
        ILogger<PlaybackReader> logger,
        int bufferSize = 10)
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
            _playback.Value.Dispose();
        }

        await Task.Run(() => _playback.Value = new(descriptor.VideoFilePath), token);
        _playback.Value.SetColorConversion(ImageFormat.ColorBgra32);

        ClearBuffer();
        _isEOF = false;
        _producerLoopCts?.Dispose();
        _producerLoopCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _producerLoopTask = Task.Run(() => ProducerLoop(_producerLoopCts.Token).ConfigureAwait(false));
    }

    public async Task RewindAsync()
    {
        if (Playback.CurrentValue is null) return;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _commandQueue.Enqueue(new(Command.Rewind, tcs));

        _loopSignal.Release();

        await tcs.Task;
    }

    public async Task SeekAsync(TimeSpan position)
    {
        if (Playback.CurrentValue is null) return;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _commandQueue.Enqueue(new(Command.Seek, tcs, position));

        _loopSignal.Release();

        await tcs.Task;
    }

    public bool TryRead(TimeSpan targetFrameTime, out Capture? capture, out ImuSample? imuSample)
    {
        capture   = null;
        imuSample = null;

        if (!_frameChannel.Reader.TryPeek(out var frame))
            return false;

        if (frame.Capture?.DepthImage is not { DeviceTimestamp: var peekedFrameTime}
        || peekedFrameTime > targetFrameTime)
            return false;

        if (!_frameChannel.Reader.TryRead(out frame))
            return false;

        try
        {
            capture   = frame.Capture?.DuplicateReference();
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
                if (request.Type is Command.Rewind)
                    ProcessSeekAction(TimeSpan.Zero);
                if (request.Type is Command.Seek && request.Position.HasValue)
                    ProcessSeekAction(request.Position.Value);
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
            _logger.LogInformation($"Seeking to {position}");
            Playback.CurrentValue.SeekTimestamp(position, K4AdotNet.Record.PlaybackSeekOrigin.Begin);
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
                    _logger.LogInformation("Waiting for next command at EOF.");
                    waitWriterTask = null;
                    waitSignalTask = null;
                    await _loopSignal.WaitAsync(token);
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

                if (!_commandQueue.IsEmpty) continue;   // To process commands alived while waiting, go to the top of this loop.

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
                    _logger.LogError($"Producer Error: {ex.Message}");
                    await Task.Delay(100, token);
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Operation cancelled.");
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
            catch (OperationCanceledException ex)
            {
                _logger.LogInformation("Operation cancelled{ex}", ex);
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
            {
                _logger.LogInformation("Task cancelled{ex}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Warning: FrameReadingLoop did not terminate gracefully within timeout. Error: {ex.Message}");
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
            _logger.LogInformation("Playback reached end of file or failed to get a capture.");
            _isEOF = true;
            return result;
        }

        if (capture?.DepthImage is null)
        {
            capture?.Dispose();
            return result;
        }

        result.Capture = capture;
        if (Playback.CurrentValue.TryGetNextImuSample(out var imuSample))
            result.ImuSample = imuSample;

        return result;
    }

    void ClearBuffer()
    {
        if (_frameChannel is null) return;

        while(_frameChannel.Reader.TryRead(out var frame))
        {
            frame.Capture?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopProducerLoop();

        Playback.Dispose();
        _producerLoopCts.Dispose();
    }
}
