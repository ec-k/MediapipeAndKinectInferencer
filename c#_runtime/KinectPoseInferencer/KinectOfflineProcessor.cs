using K4AdotNet.BodyTracking;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using System;
using System.IO;
using KinectPoseInferencer.Renderers;
using System.Threading;

namespace KinectPoseInferencer
{
    internal class KinectOfflineProcessor
    {
        readonly Renderer _renderer;
        readonly FrameManager _frameManager;
        readonly ImageWriter _imageWriter;

        public KinectOfflineProcessor(
            Renderer renderer,
            FrameManager frameManager,
            ImageWriter imageWriter)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            _frameManager = frameManager ?? throw new ArgumentNullException(nameof(frameManager));
            _imageWriter = imageWriter ?? throw new ArgumentNullException(nameof(imageWriter));
        }

        public void Run(string filePath)
        {
            // Read the file
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: {filePath} is not found.");
                return;
            }
            Console.WriteLine($"Reading a .mkv file '{filePath}' ...");

            _renderer.StartVisualizationThread();
            // Initialize playeback and tracker
            using var playback = new Playback(filePath);
            RecordConfiguration recordConfig;
            Calibration calibration;
            playback.GetRecordConfiguration(out recordConfig);
            playback.GetCalibration(out calibration);
            var tracker = new Tracker(
                calibration
                , new TrackerConfiguration
                {
                    SensorOrientation = SensorOrientation.Default,
                    ProcessingMode = TrackerProcessingMode.Gpu,
                    GpuDeviceId = 0,
                    ModelPath = null
                });
            PointCloud.ComputePointCloudCache(calibration);
            var frameInterval = TimeSpan.FromSeconds(1f / (float)recordConfig.CameraFps);
            while (_renderer.IsActive)
            {
                Capture capture;
                var waitResult = playback.TryGetNextCapture(out capture);
                if (!waitResult)
                {
                    Console.WriteLine("Error: Failed to get a capture.");
                    break;
                }

                tracker.EnqueueCapture(capture);
                capture.Dispose();

                using var frame = tracker.PopResult();
                if (frame is not null)
                {
                    _frameManager.Frame = frame.DuplicateReference();
                    Inference(frame, new Action<Skeleton>[] { });
                }
                else
                {
                    Thread.Sleep(frameInterval);
                }
                frame.Dispose();
            }

            Console.WriteLine("ボディトラッキング処理が完了しました。");
            Console.WriteLine("任意のキーを押して終了します...");
            Console.ReadKey();
        }

        void Inference(BodyFrame frame, Action<Skeleton>[] actions)
        {
            if (frame.BodyCount > 0)
            {
                Skeleton skeleton;
                frame.GetBodySkeleton(0, out skeleton);
                foreach (var action in actions)
                    action?.Invoke(skeleton);
            }
        }
    }
}
