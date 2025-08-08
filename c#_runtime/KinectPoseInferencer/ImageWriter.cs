using K4AdotNet.Sensor;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Xml;

namespace KinectPoseInferencer
{
    public class ImageWriter: IDisposable
    {
        public int Height { get; } = 1280;
        public int Width { get; } = 720;
        string _filePath = "../../../../../../colorImg.dat";
        int _bufferSize => Height * Width * 4;

        MemoryMappedFile _mmf;
        MemoryMappedViewAccessor _accessor;

        public ImageWriter()
        {
            InitMmap();
        }
        public ImageWriter(int height, int width)
        {
            Height = height;
            Width = width;

            InitMmap();
        }

        void InitMmap()
        {
            if (File.Exists(_filePath) == false)
            {
                byte[] bs = new byte[_bufferSize];
                using (FileStream fs = File.Open(_filePath, FileMode.Create))
                {
                    fs.Write(bs, 0, bs.Length);
                }
            }

            _mmf = MemoryMappedFile.CreateFromFile(_filePath, FileMode.Open);
            _accessor = _mmf.CreateViewAccessor();
        }

        public void WriteImage(Image image)
        {
            if (image is null) return;
            
            var byteImg = image.GetSpan<byte>().ToArray();
            Write(byteImg);
        }

        void Write(byte[] data)
        {
            _accessor.WriteArray(0, data, 0, data.Length);
        }
        
        public void Dispose()
        {
            _mmf.Dispose();
            _accessor.Dispose();
        }
    }
}
