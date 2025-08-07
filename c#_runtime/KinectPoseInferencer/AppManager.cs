// Copyright(c) Microsoft Corporation. All rights reserved.
// Released under the MIT license
// https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

using K4AdotNet.Record;
using K4AdotNet.Sensor;
using K4AdotNet.BodyTracking;
using KinectPoseInferencer.PoseInference;
using KinectPoseInferencer.Renderers;
using System;
using System.IO;

namespace KinectPoseInferencer
{
    internal class AppManager
    {
        public void RunOfflineProcess(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: {filePath} is not found.");
                return;
            }
            Console.WriteLine($"Reading a .mkv file '{filePath}' ...");

            var playback = new Playback(filePath);
            try
            {
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

                int frameCount = 0;
                while (true)
                {
                    Capture capture;
                    var waitResult = playback.TryGetNextCapture(out capture);
                    if (!waitResult)
                    {
                        Console.WriteLine("Error: Failed to get a capture.");
                        break;
                    }

                    frameCount++;
                    Console.WriteLine($"フレーム {frameCount} を処理中...");

                    tracker.EnqueueCapture(capture);
                    capture.Dispose();

                    var frame = tracker.PopResult();


                    if (frame.BodyCount > 0)
                    {
                        Skeleton skeleton;
                        frame.GetBodySkeleton(0, out skeleton);
                        //landmarkHandler.Update(skeleton);
                        //landmarkHandler.SendResults();
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
            using var device = Device.Open();
            var deviceConfig = new DeviceConfiguration()
            {
                CameraFps = FrameRate.Thirty,
                ColorResolution = ColorResolution.R720p,
                DepthMode = DepthMode.NarrowViewUnbinned,
                WiredSyncMode = WiredSyncMode.Standalone,
                ColorFormat = ImageFormat.ColorBgra32,
            };
            device.StartCameras(deviceConfig);
            Calibration deviceCalibration;
            device.GetCalibration(deviceConfig.DepthMode, deviceConfig.ColorResolution, out deviceCalibration);
            var tracker = new Tracker(
                    deviceCalibration,
                    new TrackerConfiguration
                    {
                        SensorOrientation = SensorOrientation.Default,
                        ProcessingMode = TrackerProcessingMode.Gpu,
                        GpuDeviceId = 0,
                        ModelPath = null
                    });
            device.StartImu();
            var imuSample = device.GetImuSample();

            // Setup for this app which requires device settings
            PointCloud.ComputePointCloudCache(deviceCalibration);
            var tiltCorrector = new TiltCorrector(imuSample, deviceCalibration);
            using var landmarkHandler = new LandmarkHandler(tiltCorrector);

            var userInputChar = "-";
            while (renderer.IsActive)
            {
                using (Capture sensorCapture = device.GetCapture())
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
                    userInputChar = Console.ReadKey().Key.ToString();
                    if (userInputChar == "C")
                        landmarkHandler.UpdateTiltRotation();
                    if (userInputChar == "R")
                        landmarkHandler.ResetTiltRotation();
                }

                // Try getting latest tracker frame.
                using var frame = tracker.PopResult();
                if (frame != null)
                {
                    visualizerData.Frame = frame.DuplicateReference();


                    // Write color image to thw Memory Mapped File
                    try
                    {
                        var colorImg = frame.Capture.ColorImage;
                        if (colorImg != null)
                        {
                            var bgraArr = colorImg.GetSpan<byte>().ToArray();
                            imgWriter.Write(bgraArr);
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    // Send Landmarks to a pose solver app
                    if (frame.BodyCount > 0)
                    {
                        Skeleton skeleton;
                        frame.GetBodySkeleton(0, out skeleton);
                        landmarkHandler.Update(skeleton);
                        landmarkHandler.SendResults();
                    }
                }
            }
        }
    }
}
