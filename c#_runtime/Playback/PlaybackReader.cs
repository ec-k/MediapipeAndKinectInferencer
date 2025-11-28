using K4AdotNet;
using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using KinectPoseInferencer.PoseInference;
using KinectPoseInferencer.Renderers;
using R3;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Playback;

internal class PlaybackReader : IPlaybackReader
{
    readonly FrameManager _frameManager;
    readonly ImageWriter _imageWriter;
    readonly LandmarkHandler _landmarkHandler;

    public ReadOnlyReactiveProperty<K4AdotNet.Record.Playback> Playback => _playback;
    public ReadOnlyReactiveProperty<bool> IsReading => _isReading;
    public event Action<BodyFrame, Capture> OnNewFrame;

    ReactiveProperty<K4AdotNet.Record.Playback> _playback = new();
    ReactiveProperty<bool> _isReading = new(false);

    Tracker _tracker;

    Task? _readingTask;
    CancellationTokenSource _cts = new();
    readonly int _taskCancelTimeoutSec = 2;

    Microseconds64 _currentTimestampUs = new(0);
    Microseconds64 _lastTimestampUs = new(0);


    public PlaybackReader(
        FrameManager frameManager,
        ImageWriter imageWriter,
        LandmarkHandler landmarkHandler)
    {
        _frameManager = frameManager ?? throw new ArgumentNullException(nameof(frameManager));
        _imageWriter = imageWriter ?? throw new ArgumentNullException(nameof(imageWriter));
        _landmarkHandler = landmarkHandler ?? throw new ArgumentNullException(nameof(landmarkHandler));
    }

    public void Configure(PlaybackDescriptor descriptor)
    {
        if (string.IsNullOrEmpty(descriptor.VideoFilePath))
            throw new ArgumentNullException(nameof(descriptor.VideoFilePath));

        // Dispose existing playback and related task
        if (_readingTask is not null)
        {
            StopReadingLoop().GetAwaiter().GetResult();
            Playback?.Dispose();
        }

        _playback.Value = new(descriptor.VideoFilePath);
        _playback.Value.GetCalibration(out var calibration);
        PointCloud.ComputePointCloudCache(calibration);

        _tracker?.Dispose();
        var trackerConfig = new TrackerConfiguration()
        {
            SensorOrientation = SensorOrientation.Default,
            ProcessingMode = TrackerProcessingMode.Gpu,
        };
        _tracker = new(calibration, trackerConfig);

        _cts.Dispose();
        _cts = new();
        _readingTask = FrameReadingLoop(_cts.Token);

        _currentTimestampUs = new(0);
        _lastTimestampUs = new(0);
    }

    public void Play()
    {
        if (_isReading.Value) return;

        _isReading.Value = true;
        Playback.CurrentValue.SeekTimestamp(_currentTimestampUs, K4AdotNet.Record.PlaybackSeekOrigin.Begin);
        _lastTimestampUs = _currentTimestampUs;
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

                    // Wait to recreate video scene in real time.
                    if (waitTime.TotalMilliseconds > 0)
                        await Task.Delay(waitTime, token);
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
        var frameTimeDiffTick = TimeSpan.Zero;
        
        if (_lastTimestampUs.ValueUsec > 0) // Calculate if this process is NOT the first reading.
        {
            var diffUs = _currentTimestampUs.ValueUsec - _lastTimestampUs.ValueUsec;
            if(diffUs > 0)
                frameTimeDiffTick = TimeSpan.FromMicroseconds(diffUs);
        }

        _tracker.EnqueueCapture(capture);

        using var frame = _tracker.PopResult();
        OnNewFrame?.Invoke(frame.DuplicateReference(), capture.DuplicateReference());

        if (frame is not null)
        {
            _frameManager.Frame = frame.DuplicateReference();

            // Write ColorImage to Shared Memory
            {
                var colorImage = frame.Capture.ColorImage;
                try
                {
                    _imageWriter.WriteImage(colorImage);
                }
                catch { }
            }

            var nullableSkeleton = Inference(frame);
            if (nullableSkeleton is Skeleton skeleton)
                SendLandmarks(skeleton);
        }

        _lastTimestampUs = _currentTimestampUs;
        capture.Dispose();

        return frameTimeDiffTick;
    }

    Skeleton? Inference(BodyFrame frame)
    {
        if (frame.BodyCount > 0)
        {
            Skeleton skeleton;
            frame.GetBodySkeleton(0, out skeleton);

            return skeleton;
        }
        return null;
    }

    void SendLandmarks(Skeleton skeleton)
    {
        _landmarkHandler.Update(skeleton);
        _landmarkHandler.SendResults();
    }

    public void Dispose()
    {
        StopReadingLoop().GetAwaiter().GetResult();

        Playback?.Dispose();
        _tracker?.Dispose();
        _cts.Dispose();
    }
}