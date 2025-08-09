// Copyright(c) Microsoft Corporation. All rights reserved.
// Released under the MIT license
// https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Input;
using KinectPoseInferencer.PoseInference;
using KinectPoseInferencer.Renderers;
using System;

namespace KinectPoseInferencer
{
    internal class KinectOnlineProcessor
    {
        readonly KeyInputProvider _keyInputProvider;
        readonly UserActionService _userActionService;
        readonly LandmarkHandler _landmarkHandler;
        readonly TiltCorrector _tiltCorrector;
        readonly Renderer _renderer;
        readonly FrameManager _frameManager;
        readonly ImageWriter _imageWriter;

        Device _device;
        Tracker _tracker;

        public KinectOnlineProcessor(
            KeyInputProvider keyInputProvider,
            UserActionService userActionService,
            LandmarkHandler landmarkHandler,
            TiltCorrector tiltCorrector,
            Renderer renderer,
            FrameManager frameManager,
            ImageWriter imageWriter)
        {
            _keyInputProvider = keyInputProvider ?? throw new ArgumentNullException(nameof(keyInputProvider));
            _userActionService = userActionService ?? throw new ArgumentNullException(nameof(userActionService));
            _landmarkHandler = landmarkHandler ?? throw new ArgumentNullException(nameof(landmarkHandler));
            _tiltCorrector = tiltCorrector ?? throw new ArgumentNullException(nameof(tiltCorrector));
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            _frameManager = frameManager ?? throw new ArgumentNullException(nameof(frameManager));
            _imageWriter = imageWriter ?? throw new ArgumentNullException(nameof(imageWriter));
        }

        public void Run()
        {
            // Setup classes
            _renderer.StartVisualizationThread();

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
            while (_renderer.IsActive)
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
                    _frameManager.Frame = frame.DuplicateReference();
                    var colorImg = frame.Capture.ColorImage;
                    try
                    {
                        _imageWriter.WriteImage(colorImg);
                    }
                    catch { }
                    Inference(frame, landmarkHandlingActions);
                }
            }
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
