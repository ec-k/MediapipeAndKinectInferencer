using K4AdotNet.Sensor;
using KinectPoseInferencer.InputHook;
using KinectPoseInferencer.Playback;
using OpenTK.Graphics.ES11;
using R3;
using System;


namespace KinectPoseInferencer.PoseInference;

public class InputLogPresenter : IDisposable
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
            .Subscribe(inputEvent =>
            {
                _sender?.SendMessage(inputEvent);
            })
            .AddTo(ref _disposables);

        GlobalInputHook.OnKeyboardEvent += KeyboardInputEventCallback;
        GlobalInputHook.OnMouseEvent += MouseInputEventCallback;
    }

    void KeyboardInputEventCallback(KeyboardEventData keyInputEvent)
    {
        var deviceInputData = new DeviceInputData
        {
            Timestamp = TimeSpan.FromTicks(keyInputEvent.RawStopwatchTimestamp).TotalMicroseconds,
            Data = keyInputEvent
        };

        _recordDataBroker.PushInputLogEvent(deviceInputData);
    }

    void MouseInputEventCallback(MouseEventData mouseInputEvent)
    {
        var deviceInputData = new DeviceInputData
        {
            Timestamp = TimeSpan.FromTicks(mouseInputEvent.RawStopwatchTimestamp).TotalMicroseconds,
            Data = mouseInputEvent
        };

        _recordDataBroker.PushInputLogEvent(deviceInputData);
    }

    public void Dispose()
    {
        GlobalInputHook.OnKeyboardEvent -= KeyboardInputEventCallback;
        GlobalInputHook.OnMouseEvent -= MouseInputEventCallback;
    }
}
