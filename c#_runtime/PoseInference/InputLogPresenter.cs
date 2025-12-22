using KinectPoseInferencer.Playback;
using R3;
using System;


namespace KinectPoseInferencer.PoseInference;

public class InputLogPresenter
{
    readonly InputEventSender _sender;
    readonly RecordDataBroker _recordDataBroker;

    DisposableBag _disposables = new();

    public InputLogPresenter(
        InputEventSender sender,
        RecordDataBroker recordDataBroker)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _recordDataBroker = recordDataBroker ?? throw new ArgumentNullException(nameof(recordDataBroker));

        _recordDataBroker.InputEvents
            .Where(inputEvent => inputEvent is not null)
            .Where(inputEvent => inputEvent.EventType is not InputEventType.Unknown)
            .Subscribe(inputEvent => 
            {
                _sender?.SendMessage(inputEvent);
            })
            .AddTo(ref _disposables);
    }
}
