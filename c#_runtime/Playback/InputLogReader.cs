using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Playback;


public class InputLogReader : IDisposable
{
    readonly List<IInputLogEvent> _events = new();
    long _kinectToSystemStopwatchOffsetUs = 0; // Offset = KinectTimestamp - SystemStopwatchTimestamp

    public IReadOnlyList<IInputLogEvent> Events => _events;
    public LogMetadata? Metadata { get; private set; }

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

        _events.Clear();

        try
        {
            foreach (string line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var logEvent = JsonSerializer.Deserialize<InputLogEvent>(line);
                    if (logEvent != null)
                    {
                        _events.Add(logEvent);
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
            _events.Clear();
            return false;
        }
    }

    private void CalculateKinectOffset()
    {
        if (Metadata is null || Metadata.SystemStopwatchTimestampAtKinectStart == 0 || Metadata.FirstKinectDeviceTimestampUs == 0)
        {
            _kinectToSystemStopwatchOffsetUs = 0;
            return;
        }

        // Offset = KinectTimestamp - SystemStopwatchTimestamp
        // This offset is added to a system stopwatch timestamp to get an equivalent Kinect timestamp.
        _kinectToSystemStopwatchOffsetUs = Metadata.FirstKinectDeviceTimestampUs - Metadata.SystemStopwatchTimestampAtKinectStart;
    }

    /// <summary>
    /// Finds all input events that occurred before or at the given Kinect device timestamp.
    /// </summary>
    /// <param name="kinectDeviceTimestampUs">The Kinect device timestamp in microseconds.</param>
    /// <returns>A list of input events that occurred up to the given timestamp.</returns>
    public IEnumerable<IInputLogEvent> GetEventsUpToKinectTimestamp(long kinectDeviceTimestampUs)
    {
        // Convert Kinect timestamp to equivalent system stopwatch timestamp
        var targetSystemStopwatchTimestamp = kinectDeviceTimestampUs - _kinectToSystemStopwatchOffsetUs;

        // Binary search to find the first event whose stopwatch timestamp is greater than targetSystemStopwatchTimestamp
        int index = _events.BinarySearch(
            new InputLogEvent { Data = new InputEventData { RawStopwatchTimestamp = (ulong)targetSystemStopwatchTimestamp } },
            Comparer<IInputLogEvent>.Create((a, b) => a.RawStopwatchTimestamp.CompareTo(b.RawStopwatchTimestamp)));

        if (index < 0)
        {
            index = ~index; // If not found, BinarySearch returns the bitwise complement of the next element's index
        }
        
        // Return all events from the beginning up to this index
        return _events.Take(index);
    }

    public void Dispose()
    {
        _events.Clear();
        Metadata = null;
    }
}