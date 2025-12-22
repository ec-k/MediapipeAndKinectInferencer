using System;
using System.Buffers.Binary;
using System.IO;
using Google.Protobuf;
using HumanLandmarks;
using HumanLandmarks.Log;

namespace KinectPoseInferencer.Logging
{
    internal class HolisticProtobufLogWriter : IResultLogWriter, IDisposable
    {
        FileStream _fileStream;
        bool _isInitialized;
        byte[] _lengthBuffer = new byte[4];

        /// <summary>
        /// Writes one frame of holistic landmark data and metadata to the log file.
        /// A 4-byte length prefix is written before the message.
        /// </summary>
        /// <param name="holisticLandmarks">The <see cref="HolisticLandmarks"/> object to write.</param>
        /// <param name="timestampMillis">The timestamp of the frame in milliseconds.</param>
        /// <param name="frameNumber">The sequence number of the frame.</param>
        public void Initialize(string filePath, LogHeader header)
        {
            if (_isInitialized)
                throw new InvalidOperationException("HolisticProtobufLogWriter has already been initialized.");

            if (header is null)
                throw new ArgumentNullException(nameof(header), "LogHeader cannot be null.");

            try
            {
                _fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

                var headerBytes = header.ToByteArray();
                WriteBytes(headerBytes);
                _fileStream.Flush();                        // Ensure the header is written immediately

                _isInitialized = true;
                Console.WriteLine($"HolisticProtobufLogWriter initialized and header written to: {filePath}");
                Console.WriteLine($"  Log Schema Version: {header.LogSchemaVersion}");
                Console.WriteLine($"  Coordinate System: Unit={header.CoordinateSystem.Unit}, Up={header.CoordinateSystem.UpAxis}, Right={header.CoordinateSystem.RightAxis}, Handedness={header.CoordinateSystem.Handedness}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize HolisticProtobufLogWriter. (path: {filePath}, error: {ex.Message})");
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Writes a byte array as a length-prefixed message to the file stream.
        /// The message length is written as a 4-byte little-endian prefix, followed by the message bytes.
        /// </summary>
        /// <param name="message">The byte array representing the Protobuf message.</param>
        public void Write(HolisticLandmarks holisticLandmarks, double timestampMillis, uint frameNumber)
        {
            if (!_isInitialized || _fileStream is null)
                throw new InvalidOperationException("HolisticProtobufLogWriter has not been initialized. Call Initialize() first.");

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

                byte[] bytes = frameData.ToByteArray();
                WriteBytes(bytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error. Failed to write HolisticLandmarks data (Frame: {frameNumber}, Timestamp: {timestampMillis} ms): {ex.Message}");
            }
        }

        void WriteBytes(byte[] message)
        {
            BinaryPrimitives.WriteInt32LittleEndian(_lengthBuffer, message.Length); // Set the length of the message in the first 4 bytes
            _fileStream.Write(_lengthBuffer, 0, 4);                                 // Write the length of the message
            _fileStream.Write(message, 0, message.Length);                          // Write the message
        }

        /// <summary>
        /// Releases the file handle and other resources.
        /// </summary>
        public void Dispose()
        {
            if (_fileStream is not null)
            {
                _fileStream.Flush();
                _fileStream.Dispose();
                _fileStream = null;
            }
            _isInitialized = false;
            GC.SuppressFinalize(this);
        }
    }
}