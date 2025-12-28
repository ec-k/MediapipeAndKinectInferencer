using K4AdotNet.Sensor;
using R3;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace KinectPoseInferencer.Core;

public class KinectDeviceController: IDisposable
{
    enum Command { None, Play, Pause, Stop, }
    ConcurrentQueue<Command> _commandQueue = new();

    readonly RecordDataBroker _dataBroker;

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
    readonly int _taskCancelTimeoutSec = 2;
    Task? _readingLoop = null;
    CancellationTokenSource? _cts = null;
    DisposableBag _disposables = new();

    public KinectDeviceController(RecordDataBroker dataBroker)
    {
        _dataBroker = dataBroker ?? throw new ArgumentNullException(nameof(dataBroker));
    }

    public void Open()
    {
        if (Device.TryOpen(out var device))
            _kinectDevice.Value = device;
        else
            Console.WriteLine("Failed to open a device.");
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
    public void Stop()  => _commandQueue.Enqueue(Command.Stop);

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
            || _readingLoop is not null) 
            return;

        device.StartCameras(config);
        device.StartImu();

        _cts = new();
        _isReading.Value = true;
        _readingLoop = Task.Run(() => ReadingLoop(_cts.Token), _cts.Token);
    }

    async Task ReadingLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var shouldStop = ProcessCommand();
                if (shouldStop)
                    return;

                if (IsReading.CurrentValue)
                {
                    GetKinectData();
                    await Task.Yield();
                }
                else
                    await Task.Delay(50, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Stop successfully on cancel requested
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in ReadingLoop: {ex}");
        }
        finally
        {
            KinectDevice.CurrentValue?.StopCameras();
            KinectDevice.CurrentValue?.StopImu();
        }
    }

    void GetKinectData()
    {
        var device = KinectDevice.CurrentValue;

        if(device.TryGetImuSample(out var imu, _captureTimeoutMs))
        {
            _dataBroker.SetImu(imu);
        }

        if (device.TryGetCapture(out var capture, _captureTimeoutMs))
        {
            using (capture)
            {
                _dataBroker.SetCapture(capture);
            }
        }
    }

    async Task StopLoopAsync()
    {
        if (_cts is null || _readingLoop is null) return;

        _commandQueue.Enqueue(Command.Stop);
        _cts.Cancel();

        try
        {
            await _readingLoop.WaitAsync(TimeSpan.FromSeconds(_taskCancelTimeoutSec));
        }
        catch (OperationCanceledException)
        {
            // ignore this exception
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
        {
            /* ignore this exception */
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: ReadingLoop did not terminate gracefully within timeout. Error: {ex.Message}");
        }
        finally
        {
            _readingLoop = null;
            _cts.Dispose();
            _cts = null;
        }
    }

    public void Dispose()
    {
        StopLoopAsync().GetAwaiter().GetResult();

        _disposables.Dispose();
        _kinectDevice?.Dispose();
    }
}
