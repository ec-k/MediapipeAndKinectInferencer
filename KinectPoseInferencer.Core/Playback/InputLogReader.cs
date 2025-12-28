using System.Text.Json;
using System.Threading.Channels;

namespace KinectPoseInferencer.Core.Playback;


public class InputLogReader : IInputLogReader
{
    long                      _kinectToStopwatchOffset = 0;
    string?                   _logFilePath;
    StreamReader?             _reader;
                              
    Channel<DeviceInputData>? _eventChannel;
    readonly int              _bufferSize = 100;
    Task?                     _producerTask;
    CancellationTokenSource?  _cts;

    LogMetadata?              _metadata;

    public async Task<bool> LoadMetaFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: Input log metadata file not found at {filePath}");
            return false;
        }

        try
        {
            using var openStream = File.OpenRead(filePath);
            _metadata = await JsonSerializer.DeserializeAsync<LogMetadata>(openStream);
            
            if (_metadata is not null)
            {
                CalculateKinectOffset();
                return true;
            }

            Console.Error.WriteLine("Error: Failed to deserialize LogMetadata.");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading input log metadata file: {ex.Message}");
            _metadata = null;
            return false;
        }
    }

    public async Task<bool> LoadLogFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: Input log file not found at {filePath}");
            return false;
        }

        _logFilePath = filePath;
        var successInitialization = await InitializeProducer();
        return successInitialization;
    }

    async Task<bool> InitializeProducer()
    {
        try
        {
            await StopProducer();

            if (string.IsNullOrWhiteSpace(_logFilePath)) return false;

            _eventChannel = Channel.CreateBounded<DeviceInputData>(
                new BoundedChannelOptions(_bufferSize)
                {
                    SingleReader = true,
                    SingleWriter = true,
                    FullMode = BoundedChannelFullMode.Wait
                });

            _reader = new StreamReader(new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));

            _cts = new();
            _producerTask = ProducerLoop(_cts.Token);

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error opening input log file: {ex.Message}");
            return false;
        }
    }

    public async Task Rewind()
    {
        await InitializeProducer();
    }

    private void CalculateKinectOffset()
    {
        if (_metadata is null || _metadata.SystemStopwatchTimestampAtKinectStart == 0 || _metadata.FirstKinectDeviceTimestampUs == 0)
        {
            _kinectToStopwatchOffset = 0;
            return;
        }

        // Offset = KinectTimestamp - SystemStopwatchTimestamp
        // This offset is added to a system stopwatch timestamp to get an equivalent Kinect timestamp.
        _kinectToStopwatchOffset = _metadata.SystemStopwatchTimestampAtKinectStart - _metadata.FirstKinectDeviceTimestampUs * TimeSpan.TicksPerMicrosecond;
    }

    async Task ProducerLoop(CancellationToken token)
    {
        if(_reader is null || _eventChannel is null || _metadata is null) return;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync();
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                DeviceInputData? inputEvent;
                try
                {
                    inputEvent = JsonSerializer.Deserialize<DeviceInputData>(line);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to parse JSON line. Error: {ex.Message}");
                    continue;
                }

                if (inputEvent?.Data is null) continue;

                var eventTimestamp = inputEvent.Data.RawStopwatchTimestamp;
                if (eventTimestamp < _metadata.SystemStopwatchTimestampAtKinectStart)
                    continue;

                await _eventChannel.Writer.WriteAsync(inputEvent);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _eventChannel?.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Finds all input events that occurred before or at the given Kinect device timestamp.
    /// </summary>
    /// <param name="kinectDeviceTimestampUs">The Kinect device timestamp in microseconds.</param>
    /// <returns>A list of input events that occurred up to the given timestamp.</returns>
    public bool TryRead(long kinectDeviceTimestampUs, out IList<DeviceInputData> results)
    {
        results = new List<DeviceInputData>();
        if (_reader is null || _metadata is null || _eventChannel is null) return false;

        // Convert Kinect timestamp to equivalent system stopwatch timestamp
        var targetSystemStopwatchTimestamp = kinectDeviceTimestampUs * TimeSpan.TicksPerMicrosecond + _kinectToStopwatchOffset;

        while (_eventChannel.Reader.TryRead(out var inputEvent))
        {
            if (inputEvent.Data is not IDeviceInput deviceInput)
                continue;

            if (deviceInput.RawStopwatchTimestamp <= targetSystemStopwatchTimestamp)
                results.Add(inputEvent);
            else
                break;
        }

        return true;
    }

    async Task StopProducer()
    {
        _cts?.Cancel();

        if(_producerTask is not null)
        {
            try
            {
                await _producerTask;
            }
            catch (OperationCanceledException) { }
        }

        _cts?.Dispose();
        _cts = null;
        _reader?.Dispose();
        _reader = null;
        _eventChannel?.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync()
    {
        await StopProducer();
        _metadata = null;
    }
}
