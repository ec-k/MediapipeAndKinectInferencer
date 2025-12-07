using K4AdotNet;
using R3;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Playback;

internal class PlaybackReader : IPlaybackReader
{
    readonly RecordDataBroker _frameCaptureBroker;
    readonly ImageWriter _imageWriter;
    readonly InputLogReader _inputLogReader;

    public ReadOnlyReactiveProperty<K4AdotNet.Record.Playback> Playback => _playback;
    public ReadOnlyReactiveProperty<bool> IsReading => _isReading;
    public ReadOnlyReactiveProperty<Microseconds64> CurrentPositionUs => _currentPositionUs;

    ReactiveProperty<K4AdotNet.Record.Playback> _playback = new();
    ReactiveProperty<bool> _isReading = new(false);
    ReactiveProperty<Microseconds64> _currentPositionUs = new(new Microseconds64(0));

    bool _isFirstFrameAfterPlay = true;
    long _systemStopwatchTimestampAtLoopStart = 0;

    Task? _readingTask;
    CancellationTokenSource _cts = new();
    readonly int _taskCancelTimeoutSec = 2;

    Microseconds64 _currentTimestampUs = new(0);
    Microseconds64 _lastTimestampUs = new(0);

    public PlaybackReader(
        RecordDataBroker frameCaptureBroker,
        ImageWriter imageWriter,
        InputLogReader inputLogReader)
    {
        _frameCaptureBroker = frameCaptureBroker ?? throw new ArgumentNullException(nameof(frameCaptureBroker));
        _imageWriter = imageWriter ?? throw new ArgumentNullException(nameof(imageWriter));
        _inputLogReader = inputLogReader ?? throw new ArgumentNullException(nameof(inputLogReader));
    }

    public async Task Configure(PlaybackDescriptor descriptor, CancellationToken token)
    {
        if (string.IsNullOrEmpty(descriptor.VideoFilePath))
            throw new ArgumentNullException(nameof(descriptor.VideoFilePath));

        // Dispose existing playback and related task
        if (_readingTask is not null)
        {
            await StopReadingLoop();
            Playback?.Dispose();
        }

        await Task.Run(() => _playback.Value = new(descriptor.VideoFilePath), token);

        // Load input log file if path is provided
        if (!string.IsNullOrEmpty(descriptor.InputLogFilePath))
        {
            _inputLogReader.LoadLogFile(descriptor.InputLogFilePath);
        }

        _playback.Value.GetCalibration(out var calibration);

        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _readingTask = FrameReadingLoop(_cts.Token);

        _currentTimestampUs = new(0);
        _lastTimestampUs = new(0);
        _systemStopwatchTimestampAtLoopStart = 0;
        _isFirstFrameAfterPlay = true;
        _currentPositionUs.Value = new(0); // Reset current position
    }

    public void Play()
    {
        if (_isReading.Value) return;

        _isReading.Value = true;
        Playback.CurrentValue.SeekTimestamp(_currentTimestampUs, K4AdotNet.Record.PlaybackSeekOrigin.Begin);
        _lastTimestampUs = _currentTimestampUs;
        _systemStopwatchTimestampAtLoopStart = Stopwatch.GetTimestamp(); // Capture system time when playback starts
        _isFirstFrameAfterPlay = true;
    }

    public void Pause()
    {
        if (!_isReading.Value) return;
        _isReading.Value = false;
    }

    public void Rewind()
    {
        _isReading.Value = false;
        _currentTimestampUs = new(0);
        _lastTimestampUs = new(0);
        _currentPositionUs.Value = new(0); // Reset current position
        _systemStopwatchTimestampAtLoopStart = 0;
        _isFirstFrameAfterPlay = true;
    }

    public void Seek(TimeSpan position)
    {
        if (Playback.CurrentValue is null) return;
        _isReading.Value = false; // Pause reading before seeking
        
        var targetUs = new Microseconds64((long)position.TotalMicroseconds);
        Playback.CurrentValue.SeekTimestamp(targetUs, K4AdotNet.Record.PlaybackSeekOrigin.Begin);
        _currentTimestampUs = targetUs;
        _lastTimestampUs = targetUs;
        _currentPositionUs.Value = targetUs;
        _systemStopwatchTimestampAtLoopStart = Stopwatch.GetTimestamp(); // Reset system start time for sync
        _isFirstFrameAfterPlay = true;
    }

    async Task FrameReadingLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_isReading.Value)
                {
                    var startSystemTime = Stopwatch.GetTimestamp();
                    var frameTimeDiff = ReadAndProcessFrame();
                    var endSystemTime = Stopwatch.GetTimestamp();
                    var processingTime = TimeSpan.FromTicks(endSystemTime - startSystemTime);

                    var waitTime = frameTimeDiff - processingTime;

                    if (_isFirstFrameAfterPlay)
                    {
                        // Skip initial wait for the first frame after play/seek
                        _isFirstFrameAfterPlay = false;
                    }
                    else
                    {
                        // Wait to recreate video scene in real time.
                        if (waitTime.TotalMilliseconds > 0)
                            await Task.Delay(waitTime, token);
                    }
                }
                else
                    await Task.Delay(50, token);    // Suppress polling rate in no-reading state
            }
        }
        catch (TaskCanceledException)
        {
            // Stop successfully on cancel requested
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in FrameReadingLoop: {ex}");
            _isReading.Value = false;
        }
    }

    async Task StopReadingLoop()
    {
        if (_cts is not null && _readingTask is not null)
        {
            _cts.Cancel();
            try
            {
                if (!_readingTask.IsCompleted)
                {
                    await _readingTask.WaitAsync(TimeSpan.FromSeconds(_taskCancelTimeoutSec));
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
                _readingTask = null;
                _cts.Dispose();
                _cts = new();
            }
        }
    }

    TimeSpan ReadAndProcessFrame()
    {
        if (Playback is null) return TimeSpan.FromMilliseconds(50);

        var waitResult = Playback.CurrentValue.TryGetNextCapture(out var capture);

        if (!waitResult)
        {
            _isReading.Value = false;
            Console.WriteLine("Info: Playback reached end of file or failed to get a capture.");
            return TimeSpan.Zero;
        }

        
        if(capture.DepthImage is null)
            return TimeSpan.Zero;

        _currentTimestampUs = capture.DepthImage.DeviceTimestamp;
        _currentPositionUs.Value = _currentTimestampUs;

        // Process input log events for the current timestamp
        foreach (var inputEvent in _inputLogReader.GetEventsUpToKinectTimestamp(_currentTimestampUs.ValueUsec))
        {
            _frameCaptureBroker.ProcessNewInputLogEvent(inputEvent);
        }

        var frameTimeDiffTick = TimeSpan.Zero;
        
        if (_lastTimestampUs.ValueUsec > 0) // Calculate if this process is NOT the first reading.
        {
            var diffUs = _currentTimestampUs.ValueUsec - _lastTimestampUs.ValueUsec;
            if(diffUs > 0)
                frameTimeDiffTick = TimeSpan.FromMicroseconds(diffUs);
        }

        // Write ColorImage to Shared Memory
        var colorImage = capture.ColorImage;
        try
        {
            _imageWriter.WriteImage(colorImage);
        }
        catch { }

        _frameCaptureBroker.UpdateCapture(capture);
        _lastTimestampUs = _currentTimestampUs;
        capture.Dispose();

        return frameTimeDiffTick;
    }

    public void Dispose()
    {
        StopReadingLoop().Wait();

        Playback?.Dispose();
        _cts?.Dispose();
        _currentPositionUs?.Dispose();
        _frameCaptureBroker?.Dispose();
        _inputLogReader?.Dispose();
    }
}
