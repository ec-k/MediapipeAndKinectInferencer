using K4AdotNet.Sensor;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;

namespace KinectPoseInferencer.Core
{
    public class ImageWriter: IDisposable
    {
        public int Height { get; } = 1280;
        public int Width { get; } = 720;
        readonly string _filePath;
        int _bufferSize => Height * Width * 4;

        MemoryMappedFile _mmf;
        MemoryMappedViewStream _stream;

        readonly ILogger<ImageWriter> _logger;

        public ImageWriter(
            string filePath,
            ILogger<ImageWriter> logger)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitMmap();
        }

        [MemberNotNull(nameof(_mmf), nameof(_stream))]
        void InitMmap()
        {
            _logger.LogInformation($"MMF Target Path: {_filePath}");

            var directoryPath = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                try
                {
                    Directory.CreateDirectory(directoryPath);
                    _logger.LogInformation($"Created directory for MMF: {directoryPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating directory '{directoryPath}': {ex.Message}");
                    throw;
                }
            }

            if (File.Exists(_filePath) == false)
            {
                try
                {
                    byte[] bs = new byte[_bufferSize];
                    using (FileStream fs = File.Open(_filePath, FileMode.Create))
                    {
                        fs.Write(bs, 0, bs.Length);
                    }
                    _logger.LogInformation($"Created new MMF file at: {_filePath}");
                }
                catch(Exception ex)
                {
                    _logger.LogError($"Error creating MMF file '{_filePath}': {ex.Message}");
                    throw;
                }
            }

            try
            {
                _mmf = MemoryMappedFile.CreateFromFile(_filePath, FileMode.Open);
                _stream = _mmf.CreateViewStream();
                _logger.LogInformation($"Successfully opened MMF: {_filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error opening MemoryMappedFile '{_filePath}': {ex.Message}");
                _logger.LogError(ex.StackTrace);
                throw;
            }
        }

        public void WriteImage(Image image)
        {
            if (image is null) return;
            
            var byteImg = image.GetSpan<byte>();

            _stream.Seek(0, SeekOrigin.Begin);
            _stream.Write(byteImg);
        }
        
        public void Dispose()
        {
            _mmf.Dispose();
            _stream.Dispose();
        }
    }
}
