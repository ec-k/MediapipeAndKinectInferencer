using K4AdotNet;
using R3;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace KinectPoseInferencer.Core.Playback;

public record struct PlaybackFrame(
    K4AdotNet.Sensor.Capture? Capture,
    K4AdotNet.Sensor.ImuSample? ImuSample,
    List<DeviceInputData> InputEvents,
    TimeSpan FrameTimeDiff
);

public class PlaybackReader : IPlaybackReader
{
    enum Command
    {
        None,
        Play,
        Rewind,
        Pause
    }
    ConcurrentQueue<Command> _commandQueue = new();

    Channel<PlaybackFrame> _frameChannel;
    readonly int _bufferSize = 10;

    readonly RecordDataBroker _frameCaptureBroker;
    readonly InputLogReader _inputLogReader;

    public ReadOnlyReactiveProperty<K4AdotNet.Record.Playback> Playback => _playback;
    public ReadOnlyReactiveProperty<bool> IsReading => _isReading;
    public ReadOnlyReactiveProperty<Microseconds64> CurrentPositionUs => _currentPositionUs;

    ReactiveProperty<K4AdotNet.Record.Playback> _playback = new();
    ReactiveProperty<bool> _isReading = new(false);
    ReactiveProperty<Microseconds64> _currentPositionUs = new(new Microseconds64(0));

    bool _isFirstFrameAfterPlay = true;

    Task? _readingLoopTask;
    Task? _producerLoopTask;
    CancellationTokenSource _readingLoopCts = new();
    CancellationTokenSource _producerLoopCts = new();
    readonly int _taskCancelTimeoutSec = 2;

    Microseconds64 _currentTimestampUs = new(0);
    Microseconds64 _lastFrameTimestampUs = new(0);

    public PlaybackReader(
        RecordDataBroker frameCaptureBroker,
        InputLogReader inputLogReader)
    {
        _frameCaptureBroker = frameCaptureBroker ?? throw new ArgumentNullException(nameof(frameCaptureBroker));
        _inputLogReader = inputLogReader ?? throw new ArgumentNullException(nameof(inputLogReader));

        _frameChannel = Channel.CreateBounded<PlaybackFrame>(
            new BoundedChannelOptions(_bufferSize)
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

        // Dispose existing playback and related task
        if (_readingLoopTask is not null)
        {
            await StopReadingLoop();
            Playback?.CurrentValue?.Dispose();
        }
        if (_producerLoopTask is not null)
            await StopProducerLoop();

        await Task.Run(() => _playback.Value = new(descriptor.VideoFilePath), token);

        // Load input log file if path is provided
        if (!string.IsNullOrEmpty(descriptor.InputLogFilePath))
            await _inputLogReader.LoadLogFile(descriptor.InputLogFilePath);

        _playback.Value.GetCalibration(out var calibration);

        _readingLoopCts?.Dispose();
        _readingLoopCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _readingLoopTask = FrameReadingLoop(_readingLoopCts.Token);

        _producerLoopCts?.Dispose();
        _producerLoopCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _producerLoopTask = ProducerLoop(_producerLoopCts.Token);

        _currentTimestampUs = new(0);
        _lastFrameTimestampUs = new(0);
        _isFirstFrameAfterPlay = true;
        _currentPositionUs.Value = new(0); // Reset current position
    }

    public void Play()
    {
        _commandQueue.Enqueue(Command.Play);
    }

    public void Pause()
    {
        _commandQueue.Enqueue(Command.Pause);
    }

    public void Rewind()
    {
        _commandQueue.Enqueue(Command.Rewind);
    }

    public void Seek(TimeSpan position)
    {
        if (Playback.CurrentValue is null) return;
        _isReading.Value = false; // Pause reading before seeking

        ClearBuffer();
        
        var targetUs = new Microseconds64((long)position.TotalMicroseconds);
        Playback.CurrentValue.SeekTimestamp(targetUs, K4AdotNet.Record.PlaybackSeekOrigin.Begin);
        _currentTimestampUs = targetUs;
        _lastFrameTimestampUs = Microseconds64.Zero;
        _currentPositionUs.Value = targetUs;
        _isFirstFrameAfterPlay = true;
    }

    async Task ProcessCommand()
    {
        if(_commandQueue.TryDequeue(out var command))
        {
            switch (command)
            {
                case Command.Play:
                    ProcessPlayAction();
                    break;
                case Command.Pause:
                    ProcessPauseAction();
                    break;
                case Command.Rewind:
                    await ProcessRewindAction();
                    break;
                default:
                    break;
            }
        }
    }

    void ProcessPlayAction()
    {
        if (_isReading.Value) return;

        ClearBuffer();
        Playback.CurrentValue.SeekTimestamp(_currentTimestampUs, K4AdotNet.Record.PlaybackSeekOrigin.Begin);
        _lastFrameTimestampUs = Microseconds64.Zero;
        _isFirstFrameAfterPlay = true;
        _isReading.Value = true;
    }

    void ProcessPauseAction()
    {
        _isReading.Value = false;
    }

    async Task ProcessRewindAction()
    {
        _isReading.Value = false;
        _currentTimestampUs = new(0);
        _lastFrameTimestampUs = new(0);
        _currentPositionUs.Value = new(0); // Reset current position
        _isFirstFrameAfterPlay = true;
        ClearBuffer();
        await _inputLogReader.Rewind();

        if (Playback.CurrentValue is not null)
            Playback.CurrentValue.SeekTimestamp(Microseconds64.Zero, K4AdotNet.Record.PlaybackSeekOrigin.Begin);
    }

    async Task FrameReadingLoop(CancellationToken token)
    {
        try
        {
            var lastEmissionTicks = Stopwatch.GetTimestamp();
            while (!token.IsCancellationRequested)
            {
                await ProcessCommand();

                if (_isReading.Value)
                {
                    if (await _frameChannel.Reader.WaitToReadAsync(token))
                    {
                        while (_frameChannel.Reader.TryRead(out var frame))
                        {
                            try
                            {
                                if (!_isReading.Value) continue;

                                var currentTick = Stopwatch.GetTimestamp();
                                var elapsedSinceLastFrame = TimeSpan.FromTicks(currentTick - lastEmissionTicks);
                                if (!_isFirstFrameAfterPlay && elapsedSinceLastFrame < frame.FrameTimeDiff)
                                {
                                    var waitTime = frame.FrameTimeDiff - elapsedSinceLastFrame;
                                    await Task.Delay(waitTime, token);
                                }

                                if (frame.Capture is not null)
                                {
                                    _frameCaptureBroker.SetCapture(frame.Capture);
                                    if (frame.Capture.DepthImage is not null)
                                    {
                                        _currentTimestampUs = frame.Capture.DepthImage.DeviceTimestamp;
                                        _currentPositionUs.Value = _currentTimestampUs;
                                    }
                                }
                                if (frame.ImuSample.HasValue)
                                    _frameCaptureBroker.SetImu(frame.ImuSample.Value);

                                foreach (var ev in frame.InputEvents)
                                    _frameCaptureBroker.SetDeviceInputData(ev);

                                lastEmissionTicks = Stopwatch.GetTimestamp();

                                var frameTimeDiff = frame.FrameTimeDiff;

                                if (_isFirstFrameAfterPlay)
                                    _isFirstFrameAfterPlay = false;         // Skip initial wait for the first frame after play/seek

                                await ProcessCommand();
                            }
                            finally
                            {
                                frame.Capture?.Dispose();
                            }
                        }
                    }
                }
                else
                {
                    await Task.Delay(50, token);    // Suppress polling rate in no-reading state
                    lastEmissionTicks = Stopwatch.GetTimestamp();
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _isReading.Value = false;
            ClearBuffer();
        }
    }

    async Task ProducerLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!_isReading.Value)
                {
                    await Task.Delay(100, token);
                    continue;
                }

                if (!await _frameChannel.Writer.WaitToWriteAsync(token)) break;

                if (!_isReading.Value) break;

                PlaybackFrame frame = default;
                try
                {
                    frame = ReadNextFrame();
                    if (frame.Capture is null) continue;

                    if (!_frameChannel.Writer.TryWrite(frame))
                        frame.Capture?.Dispose();
                }
                catch (Exception ex)
                {
                    frame.Capture?.Dispose();
                    Console.Error.WriteLine($"Producer Error: {ex.Message}");
                    await Task.Delay(1000, token);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    async Task StopReadingLoop()
    {
        if (_readingLoopCts is not null && _readingLoopTask is not null)
        {
            _readingLoopCts.Cancel();
            try
            {
                if (!_readingLoopTask.IsCompleted)
                {
                    await _readingLoopTask.WaitAsync(TimeSpan.FromSeconds(_taskCancelTimeoutSec));
                }
            }
            catch (OperationCanceledException)
            {
                // ignore this exception
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
            {
                /* ignore this exception */
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: FrameReadingLoop did not terminate gracefully within timeout. Error: {ex.Message}");
            }
            finally
            {
                _readingLoopTask = null;
                _readingLoopCts.Dispose();
                _readingLoopCts = new();
            }
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
                // ignore this exception
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
            {
                /* ignore this exception */
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: FrameReadingLoop did not terminate gracefully within timeout. Error: {ex.Message}");
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
        var result = new PlaybackFrame() { InputEvents = new() };

        if (Playback?.CurrentValue is null)
            return result with { FrameTimeDiff = TimeSpan.FromMilliseconds(50) };

        var waitResult = Playback.CurrentValue.TryGetNextCapture(out var capture);

        if (!waitResult)
        {
            _isReading.Value = false;
            Console.WriteLine("Info: Playback reached end of file or failed to get a capture.");
            return result;
        }

        if (capture?.DepthImage is null)
        {
            capture?.Dispose();
            return result;
        }

        var currentFrameTimestamp = capture.DepthImage.DeviceTimestamp;

        var inputEvents = _inputLogReader.GetEventsUpToKinectTimestamp(currentFrameTimestamp.ValueUsec);
        result.InputEvents.AddRange(inputEvents);

        var frameTimeDiffTick = TimeSpan.Zero;
        if (_lastFrameTimestampUs.ValueUsec > 0) // Calculate if this process is NOT the first reading.
        {
            var diffUs = currentFrameTimestamp.ValueUsec - _lastFrameTimestampUs.ValueUsec;
            if(diffUs > 0)
                frameTimeDiffTick = TimeSpan.FromMicroseconds(diffUs);
        }

        _lastFrameTimestampUs = currentFrameTimestamp;
        result.Capture = capture;
        result.FrameTimeDiff = frameTimeDiffTick;

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

    public void Dispose()
    {
        StopReadingLoop().Wait();
        StopProducerLoop().Wait();

        Playback?.Dispose();
        _readingLoopCts?.Dispose();
        _currentPositionUs?.Dispose();
        _frameCaptureBroker?.Dispose();
        _inputLogReader?.DisposeAsync();
    }
}
