using HumanLandmarks;
using System;
using System.IO;
using Google.Protobuf;

namespace KinectPoseInferencer.Logging
{
    internal class JsonPoseLogWriter : IPoseLogWriter, IDisposable
    {
        private StreamWriter _streamWriter;
        private JsonFormatter _jsonFormatter;
        private bool _isInitialized;

        public JsonPoseLogWriter()
        {
            _jsonFormatter = new JsonFormatter(JsonFormatter.Settings.Default.WithIndentation());
        }

        public void Initialize(string filePath)
        {
            if (_isInitialized)
                throw new InvalidOperationException("JsonPoseLogWriter has already been initialized.");

            try
            {
                _streamWriter = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None));
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize a JSON log file. (path: {filePath}, error: {ex.Message})");
                throw;
            }
        }

        public void Write(KinectPoseLandmarks poseLandmarks)
        {
            if (!_isInitialized || _streamWriter == null)
            {
                throw new InvalidOperationException("JsonPoseLogWriter has not been initialized.");
            }

            try
            {
                string jsonString = _jsonFormatter.Format(poseLandmarks);
                _streamWriter.WriteLine(jsonString);
                _streamWriter.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error. Failed to write JSON pose data: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_streamWriter != null)
            {
                _streamWriter.Dispose();
                _streamWriter = null;
            }
            _isInitialized = false;
            GC.SuppressFinalize(this);
        }
    }
}