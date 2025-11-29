using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using K4AdotNet;
using K4AdotNet.BodyTracking;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Playback;
using KinectPoseInferencer.Renderers;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace KinectPoseInferencer.UI;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    readonly IPlaybackController _controller;

    PlayerVisualizer _visualizer;

    [ObservableProperty] double _currentFrameTimestamp;
    [ObservableProperty] string _playbackLength;
    [ObservableProperty] string _playPauseIconUnicode;
    [ObservableProperty] string _videoFilePath;
    [ObservableProperty] WriteableBitmap _colorBitmap;

    const string PlayIconUnicode = "\uE768";
    const string PauseIconUnicode = "\uE769";

    readonly int MaxSeekFramesForColorImage = 100;

    public event Action<List<UIElement>> UpdateVisuals; // Change signature of event

    DisposableBag _disposables = new();
    CancellationTokenSource _cts = new();
    
    public MainWindowViewModel(IPlaybackController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));

        // _bodyVisualElements = new ObservableCollection<UIElement>(); // No longer needed


        _controller.Reader.Playback
            .Where(playback => playback is not null)
            .Subscribe(playback => {
                    UpdatePlaybackLengthDisplay(playback);
                    playback.GetCalibration(out var calibration);
                    _visualizer = new PlayerVisualizer(calibration);

                    _ = Task.Run(() => DisplayFirstColorFrame(playback), _cts.Token);
            })
            .AddTo(ref _disposables);
        _controller.Reader.IsReading
            .Subscribe(isPlaying => PlayPauseIconUnicode = isPlaying ? PauseIconUnicode : PlayIconUnicode)
            .AddTo(ref _disposables);

        _controller.Reader.OnNewFrame += OnNewFrame;
    }

    void DisplayFirstColorFrame(K4AdotNet.Record.Playback playback)
    {
        try
        {
            playback.SeekTimestamp(new Microseconds64(0), K4AdotNet.Record.PlaybackSeekOrigin.Begin);

            Capture? captureToDisplay = null;
            bool foundColorImage = false;

            for (int i = 0; i < MaxSeekFramesForColorImage; i++)
            {
                if (!playback.TryGetNextCapture(out var currentCapture))
                {
                    break;  // Probably at EOF
                }

                if (currentCapture.ColorImage is not null)
                {
                    captureToDisplay = currentCapture;
                    foundColorImage = true;
                    break;
                }
                currentCapture.Dispose();
            }

            if (foundColorImage && captureToDisplay is not null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ColorBitmap = captureToDisplay.ColorImage.ToWriteableBitmap(ColorBitmap);
                });
            }
            else
            {
                // Display gray scale image when no color image is found.
                playback.SeekTimestamp(new Microseconds64(0), PlaybackSeekOrigin.Begin);
                if (playback.TryGetNextCapture(out var firstCapture))
                {
                    if (firstCapture.DepthImage is not null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ColorBitmap = firstCapture.DepthImage.ToWriteableBitmap(ColorBitmap);
                        });
                    }
                    firstCapture.Dispose();
                }
            }

            captureToDisplay?.Dispose();
        }
        finally
        {
            playback?.SeekTimestamp(new Microseconds64(0), PlaybackSeekOrigin.Begin);
        }
    }

    void OnNewFrame(BodyFrame frame, Capture capture)
    {
        // Capture frame and depth image for background processing
        var bodyFrame = frame;
        var depthImage = capture.DepthImage;
        var colorImage = capture.ColorImage; // Get color image

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                // Process frame data in a background thread
                var visualData = _visualizer.ProcessFrame(bodyFrame, depthImage);

                // Update UI on the main thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Update ColorBitmap
                    if (colorImage is not null)
                    {
                        ColorBitmap = colorImage.ToWriteableBitmap(ColorBitmap);
                    }

                    // Get image dimensions for 2D transformation scaling
                    // var imageWidth = ColorBitmap?.PixelWidth ?? 0; // No longer needed
                    // var imageHeight = ColorBitmap?.PixelHeight ?? 0; // No longer needed

                    // Update visual elements for 2D drawing
                    var newVisualElements = _visualizer.UpdateVisuals(visualData, 640, 360); 
                    
                    // BodyVisualElements.Clear(); // No longer needed
                    // foreach (var element in newVisualElements) // No longer needed
                    // {
                    //     BodyVisualElements.Add(element); // No longer needed
                    // }

                    UpdateVisuals?.Invoke(newVisualElements); // Call new event with List<UIElement>

                    // UpdateVisuals?.Invoke(); // No longer needed as ObservableCollection updates UI automatically
                });
            }
            finally
            {
                // Ensure disposal of unmanaged resources after processing
                bodyFrame.Dispose();
                depthImage?.Dispose(); // depthImage can be null
                capture.Dispose();
            }
        });
    }

    void UpdatePlaybackLengthDisplay(K4AdotNet.Record.Playback playback)
    {
        if (playback is null) return;

        var minutes = (int)playback.RecordLength.TotalSeconds / 60;
        var seconds = (int)playback.RecordLength.TotalSeconds % 60;

        PlaybackLength = $"{minutes}:{seconds}";
    }

    [RelayCommand]
    void LoadFiles()
    {
        if (string.IsNullOrEmpty(VideoFilePath)) return;

        var playbackDesc = new PlaybackDescriptor(VideoFilePath);
        _controller.Descriptor = playbackDesc;
        _controller.Prepare();
    }

    [RelayCommand]
    void Rewind()
    {
        _controller.Rewind();
    }

    [RelayCommand]
    public void PlayOrPause()
    {
        if (_controller.Reader.IsReading.CurrentValue)
            _controller.Pause();
        else
            _controller.Play();
    }

    [RelayCommand]
    public void Play() { }

    [RelayCommand]
    public void Pause() { }

    public void Dispose()
    {
        _controller.Reader.OnNewFrame -= OnNewFrame;
        _visualizer?.Dispose();
        _disposables.Dispose();
    }
}
