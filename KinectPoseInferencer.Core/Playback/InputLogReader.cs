using System.Text.Json;
using System.Threading.Channels;

namespace KinectPoseInferencer.Core.Playback;


public class InputLogReader : IInputLogReader
{
    public TimeSpan FirstFrameTime { get; set; }

    string?                   _logFilePath;
    StreamReader?             _reader;
                              
    Channel<DeviceInputData>? _eventChannel;
    readonly int              _bufferSize = 100;
    Task?                     _producerTask;
    CancellationTokenSource?  _cts;

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
            if(_producerTask is not null)
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
            _producerTask = Task.Run(() => ProducerLoop(_cts.Token));

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

    /// <summary>
    /// Retrieves input events that occurred between the last read operation and the specified system timestamp.
    /// </summary>
    /// <param name="targetTime">The upper bound system timestamp to read up to, corresponding to the system timestamp of a Kinect capture.</param>
    /// <param name="results">When this method returns, contains the list of input events found since the last read until the <paramref name="targetTime"/>.</param>
    /// <returns><c>true</c> if the operation was executed; <c>false</c> if the reader or channel is not initialized.</returns>
    public bool TryRead(TimeSpan targetTime, out IList<DeviceInputData> results)
    {
        results = new List<DeviceInputData>();
        if (_reader is null || _eventChannel is null) return false;

        while (_eventChannel.Reader.TryPeek(out var inputEvent))
        {
            if (inputEvent.Data is not IDeviceInput)
                continue;

            if (inputEvent.Timestamp <= targetTime)
            {
                if (_eventChannel.Reader.TryRead(out inputEvent))
                    results.Add(inputEvent);
            }
            else
                break;
        }

        return true;
    }

    async Task ProducerLoop(CancellationToken token)
    {
        if(_reader is null || _eventChannel is null) return;

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!await _eventChannel.Writer.WaitToWriteAsync(token))
                    break;

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

                if (inputEvent.Timestamp < FirstFrameTime)
                    continue;

                _eventChannel.Writer.TryWrite(inputEvent);
            }
        }
        catch (OperationCanceledException) { }
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
    }
}
