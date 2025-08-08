// Copyright(c) Microsoft Corporation. All rights reserved.
// Released under the MIT license
// https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

using K4AdotNet.BodyTracking;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Input;
using KinectPoseInferencer.PoseInference;
using KinectPoseInferencer.Renderers;
using System;
using System.IO;

namespace KinectPoseInferencer
{
    internal class AppManager
    {
        readonly KeyInputProvider _keyInputProvider;
        readonly UserActionService _userActionService;
        readonly LandmarkHandler _landmarkHandler;
        readonly TiltCorrector _tiltCorrector;

        Device _device;
        Tracker _tracker;

        public AppManager(
            KeyInputProvider keyInputProvider,
            UserActionService userActionService,
            LandmarkHandler landmarkHandler,
            TiltCorrector tiltCorrector)
        {
            _keyInputProvider = keyInputProvider ?? throw new ArgumentNullException(nameof(keyInputProvider));
            _userActionService = userActionService ?? throw new ArgumentNullException(nameof(userActionService));
            _landmarkHandler = landmarkHandler ?? throw new ArgumentNullException(nameof(landmarkHandler));
            _tiltCorrector = tiltCorrector ?? throw new ArgumentNullException(nameof(tiltCorrector));
        }

        public void RunOfflineProcess(string filePath)
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

        public void RunOnlineProcess()
        {
            // Setup classes
            using var visualizerData = new FrameManager();
            var renderer = new Renderer(visualizerData);
            renderer.StartVisualizationThread();
            using var imgWriter = new ImageWriter();

            // Setup a device
            _device = Device.Open();
            var deviceConfig = new DeviceConfiguration()
            {
                CameraFps = FrameRate.Thirty,
                ColorResolution = ColorResolution.R720p,
                DepthMode = DepthMode.NarrowViewUnbinned,
                WiredSyncMode = WiredSyncMode.Standalone,
                ColorFormat = ImageFormat.ColorBgra32,
            };
            _device.StartCameras(deviceConfig);
            Calibration deviceCalibration;
            _device.GetCalibration(deviceConfig.DepthMode, deviceConfig.ColorResolution, out deviceCalibration);
            var tracker = new Tracker(deviceCalibration, new TrackerConfiguration
            {
                SensorOrientation = SensorOrientation.Default,
                ProcessingMode = TrackerProcessingMode.Gpu,
                GpuDeviceId = 0,
                ModelPath = null
            });
            _device.StartImu();
            var imuSample = _device.GetImuSample();

            // Setup for this app which requires device settings
            PointCloud.ComputePointCloudCache(deviceCalibration);
            _tiltCorrector.UpdateTiltRotation(imuSample, deviceCalibration);
            _userActionService.SetKinectRuntimeData(imuSample, deviceCalibration);
            var landmarkHandlingActions = new Action<Skeleton>[]
            {
                SendLandmarks
            };
            while (renderer.IsActive)
            {
                using (Capture sensorCapture = _device.GetCapture())
                {
                    try
                    {
                        // Queue latest frame from the sensor.
                        tracker.EnqueueCapture(sensorCapture);
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (Console.KeyAvailable)
                {
                    _keyInputProvider.ReadInputAndNotify();
                }

                // Try getting latest tracker frame.
                using var frame = tracker.PopResult();
                if (frame is not null)
                {
                    visualizerData.Frame = frame.DuplicateReference();
                    WriteColorImageToSharedMemory(frame, imgWriter);
                    Inference(frame, landmarkHandlingActions);
                }
            }
        }

        void WriteColorImageToSharedMemory(BodyFrame frame, ImageWriter imgWriter)
        {
            try
            {
                var colorImg = frame.Capture.ColorImage;
                if (colorImg is not null)
                {
                    var bgraArr = colorImg.GetSpan<byte>().ToArray();
                    imgWriter.Write(bgraArr);
                }
            }
            catch { }
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

        void SendLandmarks(Skeleton skeleton)
        {
            _landmarkHandler.Update(skeleton);
            _landmarkHandler.SendResults();
        }

        public void Dispose()
        {
            _landmarkHandler?.Dispose();
            _device?.Dispose();
            _tracker?.Dispose();
        }
    }
}
