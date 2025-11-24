namespace KinectPoseInferencer.Playback;

public class PlaybackController : IPlaybackController
{
    readonly IPlaybackReader _reader;

    public IPlaybackReader Reader => _reader;
    public PlaybackDescriptor Descriptor { get; set; }


    public PlaybackController(IPlaybackReader reader)
    {
        _reader = reader;
    }

    public void Prepare()
    {
        _reader.Configure(Descriptor);
    }

    public void Play()
    {
        _reader.Play();
    }

    public void Pause()
    {
        _reader.Pause();
    }

    public void Rewind()
    {
        _reader.Rewind();
    }

    public void Dispose()
    {
        _reader.Dispose();
    }
}
