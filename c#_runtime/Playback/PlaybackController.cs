using System;
using System.Threading;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Playback;

public class PlaybackController : IPlaybackController
{
    readonly IPlaybackReader _playbackReader;
    readonly InputLogReader _logReader;

    public IPlaybackReader Reader => _playbackReader;
    public RecordDataBroker Broker { get; }
    public PlaybackDescriptor Descriptor { get; set; }


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
        await Task.WhenAll(
            _logReader.LoadMetaFileAsync(Descriptor.MetadataFilePath),
            _playbackReader.Configure(Descriptor, token)
        );
    }

    public void Play()
    {
        _playbackReader.Play();
    }

    public void Pause()
    {
        _playbackReader.Pause();
    }

    public void Rewind()
    {
        _playbackReader.Rewind();
    }

    public void Seek(TimeSpan position)
    {
        _playbackReader.Seek(position);
    }

    public void Dispose()
    {
        _playbackReader.Dispose();
    }
}
