using R3;
using System;
using System.Linq;

namespace KinectPoseInferencer.Playback;

public class CapturePresenter
{
    readonly FrameCaptureBroker _broker;

    public CapturePresenter(
        FrameCaptureBroker broker,
        ImageWriter imageWriter)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        if (imageWriter is null) throw new ArgumentNullException(nameof(imageWriter));

        _broker.Capture
            .Where(capture => capture is not null && capture.ColorImage is not null)
            .Subscribe(capture =>
            {
                imageWriter.WriteImage(capture.ColorImage);
            });
    }
}
