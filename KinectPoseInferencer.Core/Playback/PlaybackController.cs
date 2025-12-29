using Cysharp.Threading;
using R3;
using ValueTaskSupplement;

namespace KinectPoseInferencer.Core.Playback;

public class PlaybackController : IPlaybackController
{
    readonly IPlaybackReader _playbackReader;
    readonly InputLogReader _logReader;

    public IPlaybackReader Reader => _playbackReader;
    public RecordDataBroker Broker { get; }
    public PlaybackDescriptor? Descriptor { get; set; }

    public int TargetFps { get; private set; } = 30;
    LogicLooper? _readingLoop;
    TimeSpan _playbackCurrentTimestamp;

    public ReadOnlyReactiveProperty<bool> IsPlaying => _isPlaying;
    ReactiveProperty<bool> _isPlaying = new(false);
    bool _terminateLoop = false;

    public PlaybackController(
        IPlaybackReader playbackReader,
        InputLogReader logReader,
        RecordDataBroker broker)
    {
        _playbackReader = playbackReader ?? throw new ArgumentNullException(nameof(playbackReader));
        _logReader = logReader ?? throw new ArgumentNullException(nameof(logReader));
        Broker = broker ?? throw new ArgumentNullException(nameof(broker));
    }

    public async Task Prepare(CancellationToken token)
    {
        if (Descriptor is null
            || string.IsNullOrEmpty(Descriptor.MetadataFilePath)
            || string.IsNullOrEmpty(Descriptor.InputLogFilePath)) return;

        await _logReader.LoadMetaFileAsync(Descriptor.MetadataFilePath);
        await Task.WhenAll(
            _logReader.LoadLogFile(Descriptor.InputLogFilePath),
            _playbackReader.Configure(Descriptor, token)
        );
    }

    public void Play()
    {
        if (_isPlaying.Value) return;

        if (_readingLoop is null)
        {
            _readingLoop = new(TargetFps);
            _playbackCurrentTimestamp = TimeSpan.Zero;
        }

        _isPlaying.Value = true;

        _readingLoop.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            if (_terminateLoop)
            {
                _terminateLoop = false;
                return false;
            }
            if (!_isPlaying.Value) return true;

            _playbackCurrentTimestamp += ctx.ElapsedTimeFromPreviousFrame;

            if (_playbackReader.TryRead(_playbackCurrentTimestamp, out var capture, out var imuSample))
            {
                if (capture is not null) Broker.SetCapture(capture);
                if (imuSample.HasValue)  Broker.SetImu(imuSample.Value);
            }
            _logReader.TryRead(_playbackCurrentTimestamp, out var deviceInputs);

            foreach (var input in deviceInputs)
                Broker.SetDeviceInputData(input);

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

        _playbackCurrentTimestamp = TimeSpan.Zero;
        _playbackReader.Rewind();
        await _logReader.Rewind();
    }

    public void Seek(TimeSpan position)
    {
        _playbackReader.Seek(position);
    }

    public async ValueTask DisposeAsync()
    {
        await ValueTaskEx.WhenAll(
            _playbackReader.DisposeAsync(),
            _logReader.DisposeAsync());
    }
}
