using Cysharp.Threading;
using R3;
using System.Text.Json;
using ValueTaskSupplement;

namespace KinectPoseInferencer.Core.Playback;

public class PlaybackController : IPlaybackController
{
    readonly IPlaybackReader _playbackReader;
    readonly InputLogReader _logReader;
    readonly RecordDataBroker _broker;

    public IPlaybackReader Reader => _playbackReader;
    public PlaybackDescriptor? Descriptor { get; set; }

    LogMetadata? _metadata;
    TimeSpan _firstFrameKinectTime = TimeSpan.Zero;
    TimeSpan _firstFrameSystemTime = TimeSpan.Zero;

    public int TargetFps { get; private set; }
    LogicLooper? _readingLoop;
    ReactiveProperty<TimeSpan> _playbackElapsedTime = new();
    TimeSpan _recordLength = TimeSpan.Zero;

    public ReadOnlyReactiveProperty<TimeSpan> CurrentTime => _playbackElapsedTime;
    public ReadOnlyReactiveProperty<bool> IsPlaying => _isPlaying;
    ReactiveProperty<bool> _isPlaying = new(false);
    bool _terminateLoop = false;

    DisposableBag _disposables = new();

    public PlaybackController(
        IPlaybackReader playbackReader,
        InputLogReader logReader,
        RecordDataBroker broker,
        int targetFps = 60)
    {
        _playbackReader = playbackReader ?? throw new ArgumentNullException(nameof(playbackReader));
        _logReader = logReader ?? throw new ArgumentNullException(nameof(logReader));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        TargetFps = targetFps;

        _playbackReader.Playback
            .Where(playback => playback is not null)
            .Subscribe(playback => _recordLength = playback.RecordLength)
            .AddTo(ref _disposables);
    }

    public async Task Prepare(CancellationToken token)
    {
        if (Descriptor is null
            || string.IsNullOrEmpty(Descriptor.MetadataFilePath)
            || string.IsNullOrEmpty(Descriptor.InputLogFilePath))
            return;

        await LoadMetaFileAsync(Descriptor.MetadataFilePath);
        await Task.WhenAll(
            _logReader.LoadLogFile(Descriptor.InputLogFilePath),
            _playbackReader.Configure(Descriptor, token)
        );
    }

    public async Task<bool> LoadMetaFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: Input log metadata file not found at {filePath}");
            return false;
        }

        try
        {
            using var openStream = File.OpenRead(filePath);
            _metadata = await JsonSerializer.DeserializeAsync<LogMetadata>(openStream);

            if (_metadata is not null)
            {
                _firstFrameKinectTime = _metadata.FirstFrameKinectDeviceTime;
                _firstFrameSystemTime = _metadata.FirstFrameSystemTime;

                _logReader.FirstFrameTime = _firstFrameSystemTime;
                return true;
            }

            Console.Error.WriteLine("Error: Failed to deserialize LogMetadata.");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading input log metadata file: {ex.Message}");
            _metadata = null;
            return false;
        }
    }

    public void Play()
    {
        if (_isPlaying.Value) return;

        _isPlaying.Value = true;

        if (_readingLoop is null)
            StartReadingLoop();
    }

    void StartReadingLoop()
    {
        _readingLoop = new(TargetFps);
        _playbackElapsedTime.Value = TimeSpan.Zero;

        _readingLoop.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            if (_terminateLoop)
            {
                _terminateLoop = false;
                return false;
            }
            if (_playbackElapsedTime.Value > _recordLength) 
                _isPlaying.Value = false;
            if (!_isPlaying.Value) return true;

            _playbackElapsedTime.Value += ctx.ElapsedTimeFromPreviousFrame;
            var kinectAbsoluteTime = _playbackElapsedTime.Value + _firstFrameKinectTime;
            var systemAbsoluteTime = _playbackElapsedTime.Value + _firstFrameSystemTime;

            if (_playbackReader.TryRead(kinectAbsoluteTime, out var capture, out var imuSample))
            {
                if (capture is not null) _broker.SetCapture(capture);
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
        _isPlaying.Value = false;
    }

    public async Task Rewind()
    {
        Pause();

        _playbackElapsedTime.Value = TimeSpan.Zero;
        await Task.WhenAll(_playbackReader.RewindAsync(),
                           _logReader.Rewind());
    }

    public async Task SeekAsync(TimeSpan position)
    {
        _playbackElapsedTime.Value = position;
        await Task.WhenAll(_playbackReader.SeekAsync(position), _logReader.SeekAsync(position));
    }

    public async ValueTask DisposeAsync()
    {
        _disposables.Dispose();
        await ValueTaskEx.WhenAll(
            _playbackReader.DisposeAsync(),
            _logReader.DisposeAsync());
    }
}
