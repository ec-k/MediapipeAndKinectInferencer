using Google.Protobuf;
using HumanLandmarks;
using HumanLandmarks.Log;
using System;
using System.IO;

namespace KinectPoseInferencer.Logging
{
    internal class HolisticJsonLogWriter : IResultLogWriter, IDisposable
    {
        StreamWriter _streamWriter;
        JsonFormatter _jsonFormatter;
        bool _isInitialized;

        public HolisticJsonLogWriter()
        {
            _jsonFormatter = new JsonFormatter(JsonFormatter.Settings.Default.WithIndentation());
        }

        public void Initialize(string filePath, LogHeader header)
        {
            if (_isInitialized)
                throw new InvalidOperationException("HolisticJsonLogWriter has already been initialized.");

            if (header is null)
                throw new ArgumentNullException(nameof(header), "LogHeader cannot be null.");

            try
            {
                _streamWriter = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None));

                var jsonString = _jsonFormatter.Format(header);
                _streamWriter.WriteLine(jsonString);
                _streamWriter.Flush();                              // Ensure the header is written immediately

                _isInitialized = true;
                Console.WriteLine($"HolisticJsonLogWriter initialized and header written to: {filePath}");
                Console.WriteLine($"  Log Schema Version: {header.LogSchemaVersion}");
                Console.WriteLine($"  Coordinate System: Unit={header.CoordinateSystem.Unit}, Up={header.CoordinateSystem.UpAxis}, Right={header.CoordinateSystem.RightAxis}, Handedness={header.CoordinateSystem.Handedness}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize a JSON log file. (path: {filePath}, error: {ex.Message})");
                Dispose();
                throw;
            }
        }

        public void Write(HolisticLandmarks holisticLandmarks, ulong timestampMillis, uint frameNumber)
        {
            if (!_isInitialized || _streamWriter is null)
                throw new InvalidOperationException("HolisticJsonLogWriter has not been initialized.");

            if (holisticLandmarks is null)
            {
                Console.WriteLine("Warning: Attempted to write null HolisticLandmarks. Skipping frame.");
                return;
            }

            try
            {
                var frameData = new LogFrameData
                {
                    TimestampMs = timestampMillis,
                    FrameNumber = frameNumber,
                    HolisticLandmarks = holisticLandmarks
                };

                string jsonString = _jsonFormatter.Format(frameData);
                _streamWriter.WriteLine(jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error. Failed to write HolisticLandmarks data (Frame: {frameNumber}, Timestamp: {timestampMillis} ms): {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_streamWriter is not null)
            {
                _streamWriter.Flush();
                _streamWriter.Dispose();
                _streamWriter = null;
            }
            _isInitialized = false;
            GC.SuppressFinalize(this);
        }
    }
}