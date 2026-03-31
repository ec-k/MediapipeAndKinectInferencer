using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Avalonia.Messages;
using KinectPoseInferencer.Core;
using R3;
using System;
using System.Collections.ObjectModel;

namespace KinectPoseInferencer.Avalonia.ViewModels;

public partial class DisplayViewModel : ViewModelBase, IRecipient<MediaSourceClearedMessage>, IDisposable
{
    [ObservableProperty] WriteableBitmap? _colorBitmap;

    public ObservableCollection<string> InputLogEvents { get; } = new();

    readonly IMessenger _messenger;
    DisposableBag _disposables = new();

    public DisplayViewModel(RecordDataBroker broker, IMessenger messenger)
    {
        _messenger = messenger;
        _messenger.RegisterAll(this);

        broker.Capture
            .Where(capture => capture is not null)
            .Subscribe(capture => DisplayCapture(capture!))
            .AddTo(ref _disposables);

        broker.DeviceInputData
            .Where(input => input is not null)
            .Chunk(TimeSpan.FromSeconds(1.0 / 30.0))
            .Where(inputs => inputs is { Length: > 0 })
            .Subscribe(inputs => OnNewInputLogEvent(inputs!))
            .AddTo(ref _disposables);
    }

    void DisplayCapture(Capture capture)
    {
        var captureForUi = capture?.DuplicateReference();
        if (captureForUi?.ColorImage is not Image colorImage)
        {
            captureForUi?.Dispose();
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                ColorBitmap = captureForUi.ColorImage.ToWriteableBitmap(null);
            }
            finally
            {
                captureForUi?.Dispose();
            }
        });
    }

    void OnNewInputLogEvent(DeviceInputData[] inputEvents)
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var input in inputEvents)
            {
                if (input.Data is MouseEventData mouseEventData)
                    InputLogEvents.Add($"[{input.Timestamp:hh\\:mm\\:ss\\.fff}] Mouse: {mouseEventData.X}, {mouseEventData.Y}");
                else if (input.Data is KeyboardEventData keyboardEventData && !keyboardEventData.IsKeyDown)
                    InputLogEvents.Add($"[{input.Timestamp:hh\\:mm\\:ss\\.fff}] Keyboard: {keyboardEventData.KeyCode.ToString()}");

                if (InputLogEvents.Count > 10)
                    InputLogEvents.RemoveAt(0);
            }
        });
    }

    public void SetColorBitmap(WriteableBitmap? bitmap)
    {
        ColorBitmap = bitmap;
    }

    public void Receive(MediaSourceClearedMessage message)
    {
        Dispatcher.UIThread.Post(() => ColorBitmap = null);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
