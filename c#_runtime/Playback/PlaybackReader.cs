using K4AdotNet.BodyTracking;
using KinectPoseInferencer.PoseInference;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Playback;

internal class PlaybackReader: IPlaybackReader
{
    readonly FrameManager _frameManager;
    readonly ImageWriter _imageWriter;
    readonly LandmarkHandler _landmarkHandler;

    public K4AdotNet.Record.Playback Playback { get; private set; }
    Tracker _tracker;

    Task? _readingTask;
    CancellationTokenSource _cts;
    int _taskCancelTimeoutSec = 2;
    bool _isReading = false;

    internal PlaybackReader(
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
        Playback = new(descriptor.VideoFilePath);

        Playback.GetRecordConfiguration(out var recordConfig);
        Playback.GetCalibration(out var calibration);
        var trackerConfig = new TrackerConfiguration()
        {
            SensorOrientation = SensorOrientation.Default,
            ProcessingMode = TrackerProcessingMode.Gpu,
        };

        _tracker = new(calibration, trackerConfig);
    }

    public void Start()
    {
        _isReading = true;
        Playback.SeekTimestamp(0, K4AdotNet.Record.PlaybackSeekOrigin.Begin);
        _cts = new();
        _readingTask = Task.Run(() => FrameReadingLoop(_cts.Token));
    }

    public void Pause()
    {
        _isReading = false;
    }

    public void Resume()
    {
        _isReading = true;
    }

    public void Stop()
    {
        _isReading = false;
        StopReadingLoop();
        Playback.SeekTimestamp(0, K4AdotNet.Record.PlaybackSeekOrigin.Begin);
    }

    void FrameReadingLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if(_isReading)
                ReadAndProcessFrame();
        }
    }

    void StopReadingLoop()
    {
        if (_cts is not null && _readingTask is not null)
        {
            _cts.Cancel();
            try
            {
                if (!_readingTask.Wait(TimeSpan.FromSeconds(_taskCancelTimeoutSec)))
                    Console.Error.WriteLine("Warning: Capture loop did not terminate within timeout.");
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
            { /* ignore this exception */ }
            finally
            {
                _readingTask = null;
                _cts.Dispose();
                _cts = null;
            }
        }
    }

    void ReadAndProcessFrame()
    {
        var waitResult = Playback.TryGetNextCapture(out var capture);
        if (!waitResult)
        {
            Console.WriteLine("Error: Failed to get a capture.");
            return;
        }

        _tracker.EnqueueCapture(capture);
        capture.Dispose();

        using var frame = _tracker.PopResult();
        if(frame is not null)
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
        Playback.Dispose();
    }
}
