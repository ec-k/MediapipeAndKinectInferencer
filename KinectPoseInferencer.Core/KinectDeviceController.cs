using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Core.PoseInference;
using Microsoft.Extensions.Logging;
using R3;
using System.Collections.Concurrent;


namespace KinectPoseInferencer.Core;

public class KinectDeviceController: IDisposable
{
    enum Command { None, Play, Pause, Stop, }
    ConcurrentQueue<Command> _commandQueue = new();

    readonly RecordDataBroker _dataBroker;
    readonly KinectInferencer _inferencer;

    public ReadOnlyReactiveProperty<Device> KinectDevice => _kinectDevice;
    public ReadOnlyReactiveProperty<bool> IsReading => _isReading;
    ReactiveProperty<Device> _kinectDevice = new();
    ReactiveProperty<bool> _isReading = new(false);

    public DeviceConfiguration? DeviceConfig { get; set; } = new()
    {
        CameraFps       = FrameRate.Thirty,
        ColorResolution = ColorResolution.R720p,
        DepthMode       = DepthMode.NarrowViewUnbinned,
        WiredSyncMode   = WiredSyncMode.Standalone,
        ColorFormat     = ImageFormat.ColorBgra32,
    };

    readonly int _captureTimeoutMs = 100;
    readonly int _loopStopTimeoutSec = 2;
    Thread? _readingThread = null;
    DisposableBag _disposables = new();
    readonly ILogger<KinectDeviceController> _logger;

    public KinectDeviceController(
        RecordDataBroker dataBroker,
        KinectInferencer inferencer,
        ILogger<KinectDeviceController> logger)
    {
        _dataBroker = dataBroker ?? throw new ArgumentNullException(nameof(dataBroker));
        _inferencer = inferencer ?? throw new ArgumentNullException(nameof(inferencer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Open()
    {
        if (Device.TryOpen(out var device))
            _kinectDevice.Value = device;
        else
            _logger.LogInformation("Failed to open a device.");
    }

    public K4AdotNet.Sensor.Calibration? GetCalibration()
    {
        if (DeviceConfig is not DeviceConfiguration config) return null;
        if (KinectDevice?.CurrentValue is null)             return null;

        var depthMode = config.DepthMode;
        var colorResolution = config.ColorResolution;
        KinectDevice.CurrentValue.GetCalibration(depthMode, colorResolution, out var calibration);

        return calibration;
    }

    public void Play()  => _commandQueue.Enqueue(Command.Play);
    public void Pause() => _commandQueue.Enqueue(Command.Pause);

    /// <summary>
    /// 
    /// </summary>
    /// <returns>Should stop or not.</returns>
    bool ProcessCommand()
    {
        var shouldStop = false;
        while (_commandQueue.TryDequeue(out var command))
        {
            switch (command)
            {
                case Command.Play:
                    _isReading.Value = true;  break;
                case Command.Pause:
                    _isReading.Value = false; break;
                case Command.Stop:
                    shouldStop = true;  break;
                default:
                    break;
            }
        }
        return shouldStop;
    }

    public void StartCamera()
    {
        if (DeviceConfig is not DeviceConfiguration    config
            || KinectDevice.CurrentValue is not Device device
            || _readingThread is not null)
            return;

        device.StartCameras(config);
        device.StartImu();

        // Set calibration for inferencer (will be initialized on reading thread)
        var calibration = GetCalibration();
        if (calibration.HasValue)
            _inferencer.SetCalibration(calibration.Value);

        _isReading.Value = true;

        // Use dedicated thread for CUDA thread affinity
        _readingThread = new Thread(ReadingLoop)
        {
            IsBackground = true,
            Name = "KinectDeviceController.ReadingLoop"
        };
        _readingThread.Start();
    }

    void ReadingLoop()
    {
        // Initialize Tracker on this thread (CUDA context affinity)
        _inferencer.EnsureInitialized();

        try
        {
            while (true)
            {
                if (ProcessCommand())
                    return;

                if (IsReading.CurrentValue)
                {
                    GetKinectData();
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error in ReadingLoop: {Message}", ex.Message);
        }
        finally
        {
            while(_commandQueue.TryDequeue(out _)) { }  // Drain remaining commands

            KinectDevice.CurrentValue?.StopCameras();
            KinectDevice.CurrentValue?.StopImu();
        }
    }

    void GetKinectData()
    {
        var device = KinectDevice.CurrentValue;

        if (device.TryGetImuSample(out var imu, _captureTimeoutMs))
        {
            _dataBroker.SetImu(imu);
        }

        // Single-thread model: if queue is full, pop first
        if (_inferencer.QueueSize == Tracker.MaxQueueSize)
        {
            _inferencer.TryProcessFrame(wait: true);
        }

        if (device.TryGetCapture(out var capture, _captureTimeoutMs))
        {
            using (capture)
            {
                // Send to UI for color image display
                var captureForUi = capture.DuplicateReference();
                _dataBroker.SetCapture(captureForUi);

                // Only enqueue to tracker if DepthImage is available
                if (capture.DepthImage is not null)
                {
                    _inferencer.TryEnqueueData(capture);
                    // Single-thread model: try to pop result immediately (non-blocking)
                    _inferencer.TryProcessFrame(wait: false);
                }
            }
        }
    }

    void StopReadingThread()
    {
        if (_readingThread is null) return;

        _commandQueue.Enqueue(Command.Stop);

        if (_readingThread.IsAlive)
        {
            if (!_readingThread.Join(TimeSpan.FromSeconds(_loopStopTimeoutSec)))
            {
                _logger.LogWarning("ReadingThread did not terminate gracefully within timeout");
            }
        }

        _readingThread = null;
    }

    /// <summary>
    /// Stops the camera and closes the device, but keeps the controller reusable.
    /// Call Open() and StartCamera() to restart.
    /// </summary>
    public void StopCamera()
    {
        StopReadingThread();

        _kinectDevice.Value?.Dispose();
        _kinectDevice.Value = null!;
        _isReading.Value = false;
    }

    public void Dispose()
    {
        StopCamera();

        _disposables.Dispose();
        _kinectDevice.Dispose();
        _isReading.Dispose();
    }
}
