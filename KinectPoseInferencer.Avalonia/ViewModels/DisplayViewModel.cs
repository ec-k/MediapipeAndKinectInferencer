using Avalonia.Media.Imaging;
using Avalonia.Threading;
using K4AdotNet;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Avalonia.ViewModels;

public partial class MainWindowViewModel
{
    async Task DisplayFirstColorFrame(Playback playback, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();

            playback.SeekTimestamp(new Microseconds64(0), PlaybackSeekOrigin.Begin);

            Capture? captureToDisplay = null;
            bool foundColorImage = false;

            // When displaying the first frame, we still need to iterate to find a color image.
            // The broker isn't involved in this initial seeking for the *first* display.
            for (int i = 0; i < MaxSeekFramesForColorImage; i++)
            {
                token.ThrowIfCancellationRequested();

                if (!playback.TryGetNextCapture(out var currentCapture))
                {
                    break;
                }

                if (currentCapture.ColorImage is not null)
                {
                    captureToDisplay = currentCapture.DuplicateReference();
                    foundColorImage = true;
                    break;
                }
                currentCapture?.Dispose();
            }

            if (foundColorImage && captureToDisplay is { ColorImage: not null })
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ColorBitmap = captureToDisplay.ColorImage.ToWriteableBitmap(ColorBitmap);
                    captureToDisplay?.Dispose();
                });
            }
            else
            {
                // Display gray scale image when no color image is found.
                playback.SeekTimestamp(new Microseconds64(0), PlaybackSeekOrigin.Begin);
                if (playback.TryGetNextCapture(out var firstCapture))
                {
                    using (firstCapture)
                    {
                        if (firstCapture?.DepthImage is not null)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                ColorBitmap = firstCapture.DepthImage.ToWriteableBitmap(ColorBitmap);
                            });
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("DisplayFirstColorFrame was cancelled.");
        }
    }

    void DisplayCapture(Capture capture)
    {
        // DuplicateReference first to avoid race condition with SetCapture disposing the original
        var captureForUi = capture?.DuplicateReference();
        if (captureForUi?.ColorImage is not Image colorImage)
        {
            captureForUi?.Dispose();
            return;
        }

        var width = colorImage.WidthPixels;
        var height = colorImage.HeightPixels;
        var stride = colorImage.StrideBytes;
        var buffer = colorImage.Buffer;
        var size = colorImage.SizeBytes;
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

    void OnNewFrame(Capture capture, K4AdotNet.BodyTracking.BodyFrame frame)
    {
        if (capture is null || frame is null) return;

        WriteableBitmap? colorImage = null;
        colorImage = capture?.ColorImage?.ToWriteableBitmap(colorImage);

        // Update UI on the main thread
        Dispatcher.UIThread.Post(() =>
        {
            if (colorImage is not null)
                ColorBitmap = colorImage;
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
}
