using K4AdotNet.BodyTracking;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using System;
using System.IO;

namespace KinectPoseInferencer.KinectPoseInferencer
{
    internal class KinectOfflineProcessor
    {
        public void Run(string filePath)
        {
            // Read the file
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: {filePath} is not found.");
                return;
            }
            Console.WriteLine($"Reading a .mkv file '{filePath}' ...");

            // Initialize playeback and tracker
            var playback = new Playback(filePath);
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

            try
            {
                while (true)
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
                        Inference(frame, new Action<Skeleton>[] { });

                    }
                    frame.Dispose();
                }
            }
            finally
            {
                playback.Dispose();
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
