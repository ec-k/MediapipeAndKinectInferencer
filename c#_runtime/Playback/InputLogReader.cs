using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Playback;


public class InputLogReader : IDisposable
{
    long _stopwatchToKinectTimeOffset = 0;

    public List<InputLogEvent> InputEvents = new();
    public LogMetadata? Metadata { get; private set; }

    int _currentIndex = 0;

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
            Metadata = await JsonSerializer.DeserializeAsync<LogMetadata>(openStream);
            
            if (Metadata is not null)
            {
                CalculateKinectOffset();
                return true;
            }
            else
            {
                Console.Error.WriteLine("Error: Failed to deserialize LogMetadata.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading input log metadata file: {ex.Message}");
            Metadata = null;
            return false;
        }
    }

    /// <summary>
    /// Reads input log data from the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the input log file.</param>
    /// <returns>True if the file was read successfully, false otherwise.</returns>
    public bool LoadLogFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: Input log file not found at {filePath}");
            return false;
        }

        InputEvents.Clear();

        try
        {
            foreach (string line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var rawLogEvent = JsonSerializer.Deserialize<RawInputLogEvent>(line);
                    if (rawLogEvent is not null)
                    {
                        var logEvent = ParseRawLogEvent(rawLogEvent);

                        if (logEvent.Data is null) continue;

                        // Ignore events that occurred before Kinect started
                        if ((long)logEvent.Data.RawStopwatchTimestamp < _stopwatchToKinectTimeOffset)
                        {
                            continue;
                        }

                        InputEvents.Add(logEvent);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Could not deserialize log event from line: {line}");
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Warning: Failed to parse JSON line: {line}. Error: {ex.Message}");
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading input log file: {ex.Message}");
            return false;
        }
    }

    public void Rewind()
    {
        _currentIndex = 0;
    }

    InputLogEvent ParseRawLogEvent(RawInputLogEvent rawLogEvent)
    {
        var eventType = rawLogEvent.EventType switch
        {
            "Keyboard" => InputEventType.Keyboard,
            "Mouse" => InputEventType.Mouse,
            _ => InputEventType.Unknown
        };

        DeviceInputEvent? data = eventType switch
        {
            InputEventType.Keyboard => rawLogEvent.Data.Deserialize<KeyboardEventData>(),
            InputEventType.Mouse => rawLogEvent.Data.Deserialize<MouseEventData>(),
            _ => null
        };

        return new()
        {
            Timestamp = rawLogEvent.Timestamp,
            EventType = eventType,
            Data = data,
        };
    }

    private void CalculateKinectOffset()
    {
        if (Metadata is null || Metadata.SystemStopwatchTimestampAtKinectStart == 0 || Metadata.FirstKinectDeviceTimestampUs == 0)
        {
            _stopwatchToKinectTimeOffset = 0;
            return;
        }

        // Offset = KinectTimestamp - SystemStopwatchTimestamp
        // This offset is added to a system stopwatch timestamp to get an equivalent Kinect timestamp.
        _stopwatchToKinectTimeOffset = Metadata.SystemStopwatchTimestampAtKinectStart - Metadata.FirstKinectDeviceTimestampUs * TimeSpan.TicksPerMicrosecond;
    }

    /// <summary>
    /// Finds all input events that occurred before or at the given Kinect device timestamp.
    /// </summary>
    /// <param name="kinectDeviceTimestampUs">The Kinect device timestamp in microseconds.</param>
    /// <returns>A list of input events that occurred up to the given timestamp.</returns>
    public IEnumerable<InputLogEvent> GetEventsUpToKinectTimestamp(long kinectDeviceTimestampUs)
    {
        // Convert Kinect timestamp to equivalent system stopwatch timestamp
        var targetSystemStopwatchTimestamp = (ulong)(kinectDeviceTimestampUs * TimeSpan.TicksPerMicrosecond + _stopwatchToKinectTimeOffset);

        var returnQueue = new Queue<InputLogEvent>();

        if (InputEvents.Count == 0 || _currentIndex >= InputEvents.Count)
            return returnQueue;

        while (_currentIndex < InputEvents.Count)
        {
            var currentEvent = InputEvents[_currentIndex];

            if (currentEvent.Data is null) continue;
            if (currentEvent.Data.RawStopwatchTimestamp > targetSystemStopwatchTimestamp)
                break;

            returnQueue.Enqueue(InputEvents[_currentIndex]);
            _currentIndex++;
        }
        return returnQueue;
        // Binary search to find the first event whose stopwatch timestamp is greater than targetSystemStopwatchTimestamp
        //int index = InputEvents.BinarySearch(
        //    new InputLogEvent { Data = new DeviceInputEvent { RawStopwatchTimestamp = (ulong)targetSystemStopwatchTimestamp} },
        //    Comparer<InputLogEvent>.Create((a, b) => a.Data.RawStopwatchTimestamp.CompareTo(b.Data.RawStopwatchTimestamp)));

        //if (index < 0)
        //{
        //    index = ~index; // If not found, BinarySearch returns the bitwise complement of the next element's index
        //}
        
        //// Return all events from the beginning up to this index
        //return InputEvents.Take(index);
    }

    public void Dispose()
    {
        InputEvents.Clear();
        Metadata = null;
    }
}