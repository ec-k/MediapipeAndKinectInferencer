using K4AdotNet;
using K4AdotNet.Sensor;
using R3;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace KinectPoseInferencer.Core.Playback;

public record struct PlaybackFrame(
    K4AdotNet.Sensor.Capture? Capture,
    K4AdotNet.Sensor.ImuSample? ImuSample
);

public class PlaybackReader : IPlaybackReader
{
    readonly InputLogReader _inputLogReader;

    public ReadOnlyReactiveProperty<K4AdotNet.Record.Playback> Playback => _playback;
    public ReadOnlyReactiveProperty<Microseconds64> CurrentPositionUs => _currentPositionUs;

    ReactiveProperty<K4AdotNet.Record.Playback> _playback = new();
    ReactiveProperty<Microseconds64> _currentPositionUs = new(new Microseconds64(0));

    enum Command { None, Rewind }
    ConcurrentQueue<Command> _commandQueue = new();

    Channel<PlaybackFrame> _frameChannel;
    Task? _producerLoopTask;
    CancellationTokenSource _producerLoopCts = new();
    readonly int _taskCancelTimeoutSec = 2;

    public PlaybackReader(
        InputLogReader inputLogReader,
        int bufferSize = 10)
    {
        _inputLogReader = inputLogReader ?? throw new ArgumentNullException(nameof(inputLogReader));

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
            await StopProducerLoop();

        await Task.Run(() => _playback.Value = new(descriptor.VideoFilePath), token);

        // Load input log file if path is provided
        if (!string.IsNullOrEmpty(descriptor.InputLogFilePath))
            await _inputLogReader.LoadLogFile(descriptor.InputLogFilePath);

        _playback.Value.GetCalibration(out var calibration);

        ClearBuffer();        
        _producerLoopCts?.Dispose();
        _producerLoopCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _producerLoopTask = ProducerLoop(_producerLoopCts.Token);

        _currentPositionUs.Value = new(0); // Reset current position
    }

    public void Rewind()
    {
        _commandQueue.Enqueue(Command.Rewind);
    }

    public void Seek(TimeSpan position)
    {
        if (Playback.CurrentValue is null) return;

        ClearBuffer();
        
        var targetUs = new Microseconds64(position);
        Playback.CurrentValue.SeekTimestamp(targetUs, K4AdotNet.Record.PlaybackSeekOrigin.Begin);
        _currentPositionUs.Value = targetUs;
    }

    public bool TryRead(TimeSpan targetFrameTime, out Capture? capture, out ImuSample? imuSample)
    {
        capture   = null;
        imuSample = null;

        if (!_frameChannel.Reader.TryRead(out var frame))
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

    void ProcessCommand()
    {
        if(_commandQueue.TryDequeue(out var command))
        {
            if(command is Command.Rewind)
                ProcessRewindAction();
        }
    }

    void ProcessRewindAction()
    {
        _currentPositionUs.Value = new(0); // Reset current position
        ClearBuffer();

        if (Playback.CurrentValue is not null)
            Playback.CurrentValue.SeekTimestamp(Microseconds64.Zero, K4AdotNet.Record.PlaybackSeekOrigin.Begin);
    }

    async Task ProducerLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                ProcessCommand();

                if (!await _frameChannel.Writer.WaitToWriteAsync(token))
                    break;

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
        var result = new PlaybackFrame();

        if (Playback?.CurrentValue is null)
            return result;

        var waitResult = Playback.CurrentValue.TryGetNextCapture(out var capture);

        if (!waitResult)
        {
            Console.WriteLine("Info: Playback reached end of file or failed to get a capture.");
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
        _currentPositionUs.Dispose();
    }
}
