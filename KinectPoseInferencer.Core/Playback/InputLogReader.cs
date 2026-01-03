using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace KinectPoseInferencer.Core.Playback;


public class InputLogReader : IInputLogReader
{
    public TimeSpan FirstFrameTime { private get; set; }

    string?                   _logFilePath;
    StreamReader?             _reader;
                              
    Channel<DeviceInputData>? _eventChannel;
    readonly int              _bufferSize = 100;
    Task?                     _producerTask;
    CancellationTokenSource?  _cts;

    record struct SeekCommand(TimeSpan Target, TaskCompletionSource Tcs);
    readonly ConcurrentQueue<SeekCommand> _seekQueue = new();
    readonly SemaphoreSlim _loopSignal = new(0);

    readonly ILogger<InputLogReader> _logger;

    public InputLogReader(ILogger<InputLogReader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> LoadLogFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogInformation($"Error: Input log file not found at {filePath}");
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
            _producerTask = Task.Run(() => ProducerLoop(_cts.Token).ConfigureAwait(false));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error opening input log file: {ex.Message}");
            return false;
        }
    }

    public async Task RewindAsync() => await SeekAsync(TimeSpan.Zero);

    public async Task SeekAsync(TimeSpan position)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _seekQueue.Enqueue(new SeekCommand(position, tcs));

        _loopSignal.Release();

        await tcs.Task;
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

        Task<bool>? waitWriterTask = null;
        Task? waitSignalTask = null;

        try
        {
            while (!token.IsCancellationRequested)
            {
                while (_seekQueue.TryDequeue(out var cmd))
                {
                    ExecuteSeek(cmd.Target);
                    cmd.Tcs.SetResult();
                    waitWriterTask = null;
                    continue;
                }

                if (waitWriterTask is null)
                    waitWriterTask = _eventChannel.Writer.WaitToWriteAsync(token).AsTask();
                if (waitSignalTask is null)
                    waitSignalTask = _loopSignal.WaitAsync(token);
                var completedTask = await Task.WhenAny(waitWriterTask, waitSignalTask);

                if(completedTask == waitSignalTask)
                {
                    waitSignalTask = null;
                    continue;
                }

                if (!_seekQueue.IsEmpty) continue;

                if (!await waitWriterTask) break;
                waitWriterTask = null;

                var line = await _reader.ReadLineAsync();
                if (line is null)
                {
                    _logger.LogInformation("Input log reached to EOF.");

                    waitWriterTask = null;

                    if(waitSignalTask is null)
                        waitSignalTask = _loopSignal.WaitAsync(token);
                    await waitSignalTask;
                    waitSignalTask = null;
                }
                else
                    ProcessLine(line);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation("Operation cancelled{ex}", ex);
        }
    }

    void ProcessLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        try
        {
            var inputEvent = JsonSerializer.Deserialize<DeviceInputData>(line);
            if (inputEvent?.Data is not null && inputEvent.Timestamp >= FirstFrameTime)
            {
                _eventChannel!.Writer.TryWrite(inputEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to parse JSON: {ex.Message}");
        }
    }

    void ExecuteSeek(TimeSpan targetTime)
    {
        if (_reader is null || _eventChannel is null) return;

        _reader.BaseStream.Seek(0, SeekOrigin.Begin);
        _reader.DiscardBufferedData();

        while (_eventChannel.Reader.TryRead(out _)) { }

        while (true)
        {
            var line = _reader.ReadLine();
            if (line is null) break;

            try
            {
                var inputEvent = JsonSerializer.Deserialize<DeviceInputData>(line);
                if (inputEvent is not null && inputEvent.Timestamp >= targetTime)
                {
                    _eventChannel.Writer.TryWrite(inputEvent);
                    break;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError("Json Exception was thown at ExecuteSeek {ex}", ex);
                continue;
            }
        }
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
            catch (OperationCanceledException ex)
            {
                _logger.LogInformation("Operation cancelled{ex}", ex);
            }
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
