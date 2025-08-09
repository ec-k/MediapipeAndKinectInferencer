using HumanLandmarks;
using System;
using System.Buffers.Binary;
using System.IO;
using Google.Protobuf;

namespace KinectPoseInferencer.Logging
{
    internal class PoseLogWriter: IPoseLogWriter, IDisposable
    {
        FileStream _fileStream;
        bool _isInitialized;
        byte[] _lengthBuffer = new byte[4];

        /// <summary>
        /// Initializes the log file and prepares it for writing.
        /// Creates the file if it does not exist, overwrites it if it does.
        /// Opens the file with exclusive access (FileShare.None) to prevent simultaneous access from other processes.
        /// </summary>
        /// <param name="filePath">The path of the log file to output.</param>
        public void Initialize(string filePath)
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("PoseLogWriter has been initialized.");
            }
            try
            {
                _fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initalize a log file. (path: {filePath}, error: {ex.Message})");
                throw;
            }
        }

        /// <summary>
        /// Writes one frame's pose landmark data to the log file.
        /// Before writing, the byte size of the message is written as a 4-byte prefix.
        /// </summary>
        /// <param name="poseLandmarks">The PoseLandmarks object to write.</param>
        public void Write(PoseLandmarks poseLandmarks)
        {
            if (!_isInitialized || _fileStream == null)
            {
                throw new InvalidOperationException("PoseLogWriter has not been initialized.");
            }

            try
            {
                byte[] bytes = poseLandmarks.ToByteArray();

                BinaryPrimitives.WriteInt32LittleEndian(_lengthBuffer, bytes.Length);   // Set the length of the message in the first 4 bytes
                _fileStream.Write(_lengthBuffer, 0, 4);                                 // Write the length of the message
                _fileStream.Write(bytes, 0, bytes.Length);                              // Write a main message data
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error. Failed to write pose data: {ex.Message}");
            }
        }

        /// <summary>
        /// Releases the file handle and other resources.
        /// </summary>
        public void Dispose()
        {
            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }
            _isInitialized = false;
            GC.SuppressFinalize(this);
        }
    }
}
