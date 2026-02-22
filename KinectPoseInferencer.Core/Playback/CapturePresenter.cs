using R3;
using System;
using System.Linq;

namespace KinectPoseInferencer.Core.Playback;

public class CapturePresenter
{
    readonly RecordDataBroker _broker;

    public CapturePresenter(
        RecordDataBroker broker,
        ImageWriter imageWriter)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        if (imageWriter is null) throw new ArgumentNullException(nameof(imageWriter));

        _broker.Capture
            .Where(capture => capture is not null)
            .Subscribe(capture =>
            {
                // DuplicateReference first to avoid race condition with SetCapture disposing the original
                using var captureRef = capture?.DuplicateReference();
                if (captureRef?.ColorImage is null) return;
                imageWriter.WriteImage(captureRef.ColorImage);
            });
    }
}
