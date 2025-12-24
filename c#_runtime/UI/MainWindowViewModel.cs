using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using K4AdotNet;
using K4AdotNet.BodyTracking;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using KinectPoseInferencer.InputHook;
using KinectPoseInferencer.Playback;
using KinectPoseInferencer.Renderers.Unused;
using KinectPoseInferencer.Settings;
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
    readonly IPlaybackController _playbackController;
    readonly KinectDeviceController _kinectDeviceController;
    readonly SettingsManager _settingsManager;

    [ObservableProperty] double _currentFrameTimestamp      = 0.0;
    [ObservableProperty] string _playbackLength             = "";
    [ObservableProperty] string _playPauseIconUnicode       = PlayIconUnicode;
    [ObservableProperty] string _kinectPlayPauseIconUnicode = PlayIconUnicode;
    [ObservableProperty] string _videoFilePath              = "";
    [ObservableProperty] string _inputLogFilePath           = "";
    [ObservableProperty] string _metaFilePath               = "";
    [ObservableProperty] WriteableBitmap? _colorBitmap;

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
    
    public MainWindowViewModel(
        IPlaybackController playbackController,
        KinectDeviceController kinectDeviceController,
        RecordDataBroker broker,
        SettingsManager settingManager)
    {
        _playbackController     = playbackController     ?? throw new ArgumentNullException(nameof(playbackController));
        _kinectDeviceController = kinectDeviceController ?? throw new ArgumentNullException(nameof(kinectDeviceController));
        _broker                 = broker                 ?? throw new ArgumentNullException(nameof(broker));
        _settingsManager        = settingManager         ?? throw new ArgumentNullException(nameof(settingManager));
        // _bodyVisualElements = new ObservableCollection<UIElement>(); // No longer needed

        var latestSetting = _settingsManager.Load();
        _videoFilePath    = latestSetting.VideoFilePath;
        _inputLogFilePath = latestSetting.InputLogFilePath;
        _metaFilePath     = latestSetting.MetaFilePath;

        _playbackController.Reader.Playback
            .Where(playback => playback is not null)
            .Subscribe(playback => {
                    UpdatePlaybackLengthDisplay(playback);
                    playback.GetCalibration(out var calibration);
                    PointCloud.ComputePointCloudCache(calibration);
                    TotalDurationSeconds = playback.RecordLength.TotalSeconds;
            })
            .AddTo(ref _disposables);
        _playbackController.Reader.IsReading
            .Subscribe(isPlaying => PlayPauseIconUnicode = isPlaying ? PauseIconUnicode : PlayIconUnicode)
            .AddTo(ref _disposables);

        _kinectDeviceController.IsReading
            .Subscribe(isReading => KinectPlayPauseIconUnicode = isReading ? PauseIconUnicode: PlayIconUnicode)
            .AddTo(ref _disposables);

        _playbackController.Reader.CurrentPositionUs
            .ThrottleLast(TimeSpan.FromSeconds(1.0/2.0))
            .Subscribe(position => CurrentPositionSeconds = position.TotalSeconds)
            .AddTo(ref _disposables);

        _broker.Capture
            .Where(capture => capture is not null)
            .Subscribe(capture => DisplayCapture(capture))
            .AddTo(ref _disposables);
        _broker.InputEvents
            .Where(input => input is not null)
            .Chunk(TimeSpan.FromSeconds(1.0 / 10.0))
            .Where(inputs => inputs is { Length: > 0 })
            .Subscribe(inputs => OnNewInputLogEvent(inputs))
            .AddTo(ref _disposables);
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

            if (foundColorImage && captureToDisplay is { ColorImage: not null })
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if(ColorBitmap is not null)
                        ColorBitmap = captureToDisplay.ColorImage.ToWriteableBitmap();
                }, System.Windows.Threading.DispatcherPriority.Background, token); // Pass token to Invoke
            }
            else
            {
                // Display gray scale image when no color image is found.
                playback.SeekTimestamp(new Microseconds64(0), PlaybackSeekOrigin.Begin);
                if (playback.TryGetNextCapture(out var firstCapture))
                {
                    if (firstCapture?.DepthImage is not null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ColorBitmap = firstCapture.DepthImage.ToWriteableBitmap();
                        }, System.Windows.Threading.DispatcherPriority.Background, token); // Pass token to Invoke
                    }
                    firstCapture?.Dispose();
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
        if (capture?.ColorImage is null) return;

        var captureForUi = capture.DuplicateReference();
        if (captureForUi?.ColorImage is not Image colorImage) return;

        var width  = colorImage.WidthPixels;
        var height = colorImage.HeightPixels;
        var stride = colorImage.StrideBytes;
        var buffer = colorImage.Buffer;
        var size   = colorImage.SizeBytes;
        Application.Current.Dispatcher.InvokeAsync(() =>
        {

            try
            {
                if (ColorBitmap is null
                || ColorBitmap.PixelHeight != height
                || ColorBitmap.PixelWidth  != width)
                    ColorBitmap = captureForUi.ColorImage.ToWriteableBitmap();
                else
                {
                    ColorBitmap.WritePixels(
                        new Int32Rect(0, 0, width, height),
                        buffer,
                        size,
                        stride
                    );
                }
            }
            finally
            {
                captureForUi?.Dispose();
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
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
        colorImage = capture?.ColorImage?.ToWriteableBitmap();

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

    void OnNewInputLogEvent(DeviceInputData[] inputEvents)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var input in inputEvents)
            {
                InputLogEvents.Add($"[{CurrentPositionSeconds:F3}s] {input.GetType().Name}: {input.Timestamp}");
                if (InputLogEvents.Count > 10)
                    InputLogEvents.RemoveAt(0);
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
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

        _settingsManager.Save(new()
        {
            VideoFilePath    = VideoFilePath,
            InputLogFilePath = InputLogFilePath,
            MetaFilePath     = MetaFilePath,
        });

        try
        {
            IsLoading = true;
            var playbackDesc = new PlaybackDescriptor(VideoFilePath, InputLogFilePath, MetaFilePath);
            _playbackController.Descriptor = playbackDesc;
            await _playbackController.Prepare(token);

            // Display the first frame after successful loading
            if (_playbackController.Reader.Playback.CurrentValue is K4AdotNet.Record.Playback playback)
            {
                await Task.Run(() => DisplayFirstColorFrame(playback, _playbackController.Broker, token), token);
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
        _playbackController.Rewind();
        CurrentPositionSeconds = 0; // Reset CurrentPositionSeconds
        
        // Display the first frame
        if (_playbackController.Reader.Playback.CurrentValue is K4AdotNet.Record.Playback playback)
        {
            await Task.Run(() => DisplayFirstColorFrame(playback, _playbackController.Broker, token), token);
        }
    }

    [RelayCommand]
    public void KinectPlayOrPause()
    {
        if (_kinectDeviceController.KinectDevice.CurrentValue is null)
        {
            _kinectDeviceController.Open();

            // Setup visualization
            if (_kinectDeviceController?.KinectDevice is null) return;

            var calibration = _kinectDeviceController.GetCalibration();
            if (calibration.HasValue)
            {
                PointCloud.ComputePointCloudCache(calibration.Value);
            }

            _kinectDeviceController.StartCamera();

            if (!GlobalInputHook.IsHookActive)
            {
                GlobalInputHook.StartHooks();
                GlobalInputHook.StartProcessingEvents();
            }
        }
        else
        {
            if (_kinectDeviceController.IsReading.CurrentValue)
                _kinectDeviceController.Pause();
            else
                _kinectDeviceController.Play();


            if (!GlobalInputHook.IsHookActive)
            {
                GlobalInputHook.StartHooks();
                GlobalInputHook.StartProcessingEvents();
            }
            else
            {
                GlobalInputHook.StopProcessingEvents();
                GlobalInputHook.StopHooks();
            }
        }
    }

    [RelayCommand]
    public void PlayOrPause()
    {
        if (_playbackController.Reader.IsReading.CurrentValue)
            _playbackController.Pause();
        else
            _playbackController.Play();
    }

    public void Dispose()
    {
        _kinectDeviceController?.Dispose();
        _playbackController?.Dispose();
        _disposables.Dispose();

        if (GlobalInputHook.IsHookActive)
        {
            GlobalInputHook.StopProcessingEvents();
            GlobalInputHook.StopHooks();
        }
    }
}