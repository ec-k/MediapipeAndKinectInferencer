using K4AdotNet.Sensor;
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

        public ImageWriter(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            InitMmap();
        }

        [MemberNotNull(nameof(_mmf), nameof(_stream))]
        void InitMmap()
        {
            Console.WriteLine($"MMF Target Path: {_filePath}");

            var directoryPath = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                try
                {
                    Directory.CreateDirectory(directoryPath);
                    Console.WriteLine($"Created directory for MMF: {directoryPath}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error creating directory '{directoryPath}': {ex.Message}");
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
                    Console.WriteLine($"Created new MMF file at: {_filePath}");
                }
                catch(Exception ex)
                {
                    Console.Error.WriteLine($"Error creating MMF file '{_filePath}': {ex.Message}");
                    throw;
                }
            }

            try
            {
                _mmf = MemoryMappedFile.CreateFromFile(_filePath, FileMode.Open);
                _stream = _mmf.CreateViewStream();
                Console.WriteLine($"Successfully opened MMF: {_filePath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error opening MemoryMappedFile '{_filePath}': {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
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
