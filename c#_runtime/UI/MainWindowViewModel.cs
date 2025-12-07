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
using System.Collections.ObjectModel;
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
    [ObservableProperty] string _inputLogFilePath;
    [ObservableProperty] string _metaFilePath;
    [ObservableProperty] WriteableBitmap _colorBitmap;

    [ObservableProperty] bool _isLoading = false;
    [ObservableProperty] double _totalDurationSeconds;
    [ObservableProperty] double _currentPositionSeconds;

    const string PlayIconUnicode = "\uE768";
    const string PauseIconUnicode = "\uE769";

    readonly int MaxSeekFramesForColorImage = 100;

    public ObservableCollection<UIElement> BodyVisualElements { get; } = new();
    public ObservableCollection<string> InputLogEvents { get; } = new();

    RecordDataBroker _broker;

    DisposableBag _disposables = new();
    CancellationTokenSource _cts = new();
    
    public MainWindowViewModel(
        IPlaybackController controller,
        RecordDataBroker broker)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));

        // _bodyVisualElements = new ObservableCollection<UIElement>(); // No longer needed


        _controller.Reader.Playback
            .Where(playback => playback is not null)
            .Subscribe(playback => {
                    UpdatePlaybackLengthDisplay(playback);
                    playback.GetCalibration(out var calibration);
                    _visualizer = new PlayerVisualizer(calibration);
                    PointCloud.ComputePointCloudCache(calibration);
                    TotalDurationSeconds = playback.RecordLength.TotalSeconds;
            })
            .AddTo(ref _disposables);
        _controller.Reader.IsReading
            .Subscribe(isPlaying => PlayPauseIconUnicode = isPlaying ? PauseIconUnicode : PlayIconUnicode)
            .AddTo(ref _disposables);

        _controller.Reader.CurrentPositionUs
            .Subscribe(position => CurrentPositionSeconds = position.TotalSeconds)
            .AddTo(ref _disposables);

        _broker.Capture
            .Where(capture => capture is not null)
            .Subscribe(capture => DisplayCapture(capture))
            .AddTo(ref _disposables);
        _broker.OnNewInputLogEvent += OnNewInputLogEvent; // Subscribe to new input log event
    }

    void DisplayFirstColorFrame(K4AdotNet.Record.Playback playback, RecordDataBroker broker, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested(); // Check for cancellation at the start

            playback.SeekTimestamp(new Microseconds64(0), K4AdotNet.Record.PlaybackSeekOrigin.Begin);

            Capture? captureToDisplay = null;
            bool foundColorImage = false;

            // When displaying the first frame, we still need to iterate to find a color image.
            // The broker isn't involved in this initial seeking for the *first* display.
            for (int i = 0; i < MaxSeekFramesForColorImage; i++)
            {
                token.ThrowIfCancellationRequested(); // Check for cancellation within the loop

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
                }, System.Windows.Threading.DispatcherPriority.Background, token); // Pass token to Invoke
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
                        }, System.Windows.Threading.DispatcherPriority.Background, token); // Pass token to Invoke
                    }
                    firstCapture.Dispose();
                }
            }

            captureToDisplay?.Dispose();
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation
            Console.WriteLine("DisplayFirstColorFrame was cancelled.");
        }
        finally
        {
            playback?.SeekTimestamp(new Microseconds64(0), PlaybackSeekOrigin.Begin);
        }
    }

    void DisplayCapture(Capture capture)
    {
        if (capture is null) return;

        WriteableBitmap? colorImage = null;
        colorImage = capture?.ColorImage?.ToWriteableBitmap(colorImage);

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (colorImage is not null)
                ColorBitmap = colorImage;
        });
    }

    void OnNewFrame(Capture capture, BodyFrame frame)
    {
        if (capture is null || frame is null) return;

        //var visualData = _visualizer.ProcessFrame(frame, capture?.DepthImage);
        //_visualizer.UpdateVisuals(visualData, 640, 360);
        //var activeElements = new HashSet<UIElement>(_visualizer.ActiveVisualElements);
        //var elementsToRemove = BodyVisualElements
        //                                .Where(element => !activeElements.Contains(element))
        //                                .ToList();

        WriteableBitmap? colorImage = null;
        colorImage = capture?.ColorImage?.ToWriteableBitmap(colorImage);

        // Update UI on the main thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (colorImage is not null)
                ColorBitmap = colorImage;

            //foreach (var element in elementsToRemove)
            //{
            //    BodyVisualElements.Remove(element);
            //}

            //foreach (var element in activeElements)
            //{
            //    if (!BodyVisualElements.Contains(element))
            //    {
            //        BodyVisualElements.Add(element);
            //    }
            //}
        });
    }

    void OnNewInputLogEvent(IInputLogEvent inputLogEvent)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            InputLogEvents.Add($"[{CurrentPositionSeconds:F3}s] {inputLogEvent.GetType().Name}: {inputLogEvent.RawStopwatchTimestamp}");
            if (InputLogEvents.Count > 20)
            {
                InputLogEvents.RemoveAt(0);
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

    [RelayCommand(IncludeCancelCommand = true)]
    async Task LoadFiles(CancellationToken token)
    {
        if (string.IsNullOrEmpty(VideoFilePath)) return;

        try
        {
            IsLoading = true;
            var playbackDesc = new PlaybackDescriptor(VideoFilePath, InputLogFilePath, MetaFilePath);
            _controller.Descriptor = playbackDesc;
            await _controller.Prepare(token);

            // Display the first frame after successful loading
            if (_controller.Reader.Playback.CurrentValue is K4AdotNet.Record.Playback playback)
            {
                await Task.Run(() => DisplayFirstColorFrame(playback, _controller.Broker, token), token);
            }
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation if needed
            Console.WriteLine("File loading was cancelled.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading file: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    async Task Rewind(CancellationToken token)
    {
        _controller.Rewind();
        CurrentPositionSeconds = 0; // Reset CurrentPositionSeconds
        
        // Display the first frame
        if (_controller.Reader.Playback.CurrentValue is K4AdotNet.Record.Playback playback)
        {
            await Task.Run(() => DisplayFirstColorFrame(playback, _controller.Broker, token), token);
        }
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
        _controller.Broker.OnNewInputLogEvent -= OnNewInputLogEvent;
        _visualizer?.Dispose();
        _disposables.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}