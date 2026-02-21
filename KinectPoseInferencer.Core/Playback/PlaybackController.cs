using Cysharp.Threading;
using KinectPoseInferencer.Core.PoseInference;
using Microsoft.Extensions.Logging;
using R3;
using System.Text.Json;
using ValueTaskSupplement;

namespace KinectPoseInferencer.Core.Playback;

public enum PlaybackState
{
    Playing,
    Pause,
    Lock,
}

public class PlaybackController : IPlaybackController
{
    readonly IPlaybackReader _playbackReader;
    readonly IInputLogReader _logReader;
    readonly RecordDataBroker _broker;
    readonly KinectInferencer _inferencer;

    public IPlaybackReader Reader => _playbackReader;
    public PlaybackDescriptor? Descriptor { get; set; }

    LogMetadata? _metadata;
    TimeSpan _firstFrameKinectTime = TimeSpan.Zero;
    TimeSpan _firstFrameSystemTime = TimeSpan.Zero;
    TimeSpan _kinectToSystemTimestampOffset = TimeSpan.Zero;

    public int TargetFps { get; private set; }
    LogicLooper? _readingLoop;
    TimeSpan _recordLength = TimeSpan.Zero;

    public ReadOnlyReactiveProperty<PlaybackState> State => _state;
    public ReadOnlyReactiveProperty<TimeSpan> CurrentTime => _playbackElapsedTime;
    ReactiveProperty<PlaybackState> _state = new(PlaybackState.Pause);
    ReactiveProperty<TimeSpan> _playbackElapsedTime = new(TimeSpan.Zero);
    bool _terminateLoop = false;

    public event Action? OnEOF;
    public event Action? OnSeek;

    DisposableBag _disposables = new();
    readonly ILogger<PlaybackController> _logger;

    public PlaybackController(
        IPlaybackReader playbackReader,
        IInputLogReader logReader,
        RecordDataBroker broker,
        ILogger<PlaybackController> logger,
        KinectInferencer inferencer,
        int targetFps = 60)
    {
        _playbackReader = playbackReader ?? throw new ArgumentNullException(nameof(playbackReader));
        _logReader = logReader ?? throw new ArgumentNullException(nameof(logReader));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inferencer = inferencer ?? throw new ArgumentNullException(nameof(inferencer));
        TargetFps = targetFps;

        _playbackReader.Playback
            .Where(playback => playback is not null)
            .Subscribe(playback => _recordLength = playback!.RecordLength)
            .AddTo(ref _disposables);
        _playbackReader.InitialDeviceTimestamp
            .Where(ts => ts != TimeSpan.Zero)
            .Subscribe(ts =>
            {
                _firstFrameKinectTime = ts;
                _firstFrameSystemTime = ts + _kinectToSystemTimestampOffset;
                _logReader.FirstFrameTime = _firstFrameSystemTime;
            })
            .AddTo(ref _disposables);
    }

    public async Task Prepare(CancellationToken token)
    {
        if (Descriptor is null
            || string.IsNullOrEmpty(Descriptor.MetadataFilePath)
            || string.IsNullOrEmpty(Descriptor.InputLogFilePath))
            return;

        _state.Value = PlaybackState.Lock;
        await LoadMetaFileAsync(Descriptor.MetadataFilePath);
        await Task.WhenAll(
            _logReader.LoadLogFile(Descriptor.InputLogFilePath),
            _playbackReader.Configure(Descriptor, token)
        );
        _state.Value = PlaybackState.Pause;
    }

    public async Task<bool> LoadMetaFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogInformation("Error: Input log metadata file not found at {FilePath}", filePath);
            return false;
        }

        try
        {
            using var openStream = File.OpenRead(filePath);
            _metadata = await JsonSerializer.DeserializeAsync<LogMetadata>(openStream);

            if (_metadata is not null)
            {
                _kinectToSystemTimestampOffset = _metadata.FirstFrameSystemTime - _metadata.FirstFrameKinectDeviceTime;
                return true;
            }

            _logger.LogError("Error: Failed to deserialize LogMetadata.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error reading input log metadata file: {Message}", ex.Message);
            _metadata = null;
            return false;
        }
    }

    public void Play()
    {
        if (_state.Value is not PlaybackState.Pause)
            return;

        _state.Value = PlaybackState.Playing;

        if (_readingLoop is null)
            StartReadingLoop();
    }

    void StartReadingLoop()
    {
        _readingLoop = new(TargetFps);

        _readingLoop.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            if (_terminateLoop)
            {
                _terminateLoop = false;
                return false;
            }
            if (_playbackElapsedTime.Value > _recordLength)
            {
                if (_state.Value is PlaybackState.Playing)
                {
                    // Process remaining frames in queue before EOF
                    _inferencer.ProcessQueueTail();
                    OnEOF?.Invoke();
                }
                _state.Value = PlaybackState.Pause;
            }
            if (_state.Value is not PlaybackState.Playing)
                return true;

            _playbackElapsedTime.Value += ctx.ElapsedTimeFromPreviousFrame;
            var kinectAbsoluteTime = _playbackElapsedTime.Value + _firstFrameKinectTime;
            var systemAbsoluteTime = _playbackElapsedTime.Value + _firstFrameSystemTime;

            // Ensure Tracker is initialized on this thread (LogicLooper thread)
            _inferencer.EnsureInitialized();

            // Single-thread model: if queue is full, pop first (like SingleThreadProcessor sample)
            if (_inferencer.QueueSize == K4AdotNet.BodyTracking.Tracker.MaxQueueSize)
            {
                _inferencer.TryProcessFrame(wait: true);
            }

            if (_playbackReader.TryRead(kinectAbsoluteTime, out var capture, out var imuSample))
            {
                if (capture is not null)
                {
                    // Capture ownership: we own it, enqueue to tracker, then dispose immediately.
                    // Tracker makes internal copy, so we can safely dispose here.
                    _inferencer.TryEnqueueData(capture);
                    capture.Dispose();

                    // Single-thread model: try to pop result immediately (non-blocking)
                    _inferencer.TryProcessFrame(wait: false);
                }
                if (imuSample.HasValue) _broker.SetImu(imuSample.Value);
            }
            _logReader.TryRead(systemAbsoluteTime, out var deviceInputs);

            foreach (var input in deviceInputs)
                _broker.SetDeviceInputData(input);

            return true;
        });
    }

    public void Pause()
    {
        _state.Value = PlaybackState.Pause;
    }

    public async Task Rewind()
    {
        _state.Value = PlaybackState.Lock;
        OnSeek?.Invoke();
        _playbackElapsedTime.Value = TimeSpan.Zero;
        await Task.WhenAll(_playbackReader.RewindAsync(),
                           _logReader.RewindAsync());

        Pause();
    }

    public async Task SeekAsync(TimeSpan position)
    {
        _state.Value = PlaybackState.Lock;
        OnSeek?.Invoke();
        _playbackElapsedTime.Value = position;
        await Task.WhenAll(_playbackReader.SeekAsync(position),
                           _logReader.SeekAsync(position));

        Pause();
    }

    public async ValueTask DisposeAsync()
    {
        _disposables.Dispose();
        await ValueTaskEx.WhenAll(
            _playbackReader.DisposeAsync(),
            _logReader.DisposeAsync());
    }
}
