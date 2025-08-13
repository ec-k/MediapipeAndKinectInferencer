using HumanLandmarks.Log;
using K4AdotNet.BodyTracking;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Logging;
using KinectPoseInferencer.PoseInference;
using KinectPoseInferencer.Renderers;
using System;
using System.IO;
using System.Threading;

namespace KinectPoseInferencer
{
    internal class KinectOfflineProcessor
    {
        readonly Renderer _renderer;
        readonly FrameManager _frameManager;
        readonly IResultLogWriter _resultLogWriter;
        readonly LandmarkHandler _landmarkHandler;

        uint _frameCount = 1;

        public KinectOfflineProcessor(
            Renderer renderer,
            FrameManager frameManager,
            IResultLogWriter resultLogWriter,
            LandmarkHandler landmarkHandler)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            _frameManager = frameManager ?? throw new ArgumentNullException(nameof(frameManager));
            _resultLogWriter = resultLogWriter ?? throw new ArgumentNullException(nameof(resultLogWriter));
            _landmarkHandler = landmarkHandler ?? throw new ArgumentNullException(nameof(landmarkHandler));
        }

        public void Run(string videlFilePath, string logFileDestination)
        {
            // Read the file
            if (!File.Exists(videlFilePath))
            {
                Console.WriteLine($"Error: {videlFilePath} is not found.");
                return;
            }
            Console.WriteLine($"Reading a .mkv file '{videlFilePath}' ...");

            _renderer.StartVisualizationThread();
            // Initialize playeback and tracker
            using var playback = new Playback(videlFilePath);
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

            // define a header
            var coordinationSystem = new CoordinateSystem();
            coordinationSystem.Unit = "milli meter";
            coordinationSystem.UpAxis = CoordinateSystem.Types.Direction.YPlus;
            coordinationSystem.RightAxis = CoordinateSystem.Types.Direction.XPlus;
            coordinationSystem.Handedness = CoordinateSystem.Types.Handedness.LeftHanded;

            var header = new LogHeader();
            header.LogSchemaVersion = "1.0";
            header.CaptureFramerateFps = Utils.IntCameraFps(recordConfig.CameraFps);
            header.CoordinateSystem = coordinationSystem;

            _resultLogWriter.Initialize(logFileDestination, header);
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
                    var timestampMs = frame.DeviceTimestamp.TotalMilliseconds;
                    var nullableSkeleton = Inference(frame, null);
                    if (nullableSkeleton is Skeleton skeleton)
                        WriteLog(skeleton, timestampMs);
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

        Skeleton? Inference(BodyFrame frame, Action<Skeleton>[] actions)
        {
            if (frame.BodyCount > 0)
            {
                Skeleton skeleton;
                frame.GetBodySkeleton(0, out skeleton);

                if (actions is not null) 
                    foreach (var action in actions)
                        action?.Invoke(skeleton);

                return skeleton;
            }
            return null;
        }

        void WriteLog(Skeleton skeleton, double timestampMs)
        {
            _landmarkHandler.Update(skeleton);
            var result = _landmarkHandler.Result;
            _resultLogWriter.Write(result, timestampMs, _frameCount);
            _frameCount++;
        }
    }
}
