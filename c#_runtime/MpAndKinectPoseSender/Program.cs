// Copyright(c) Microsoft Corporation. All rights reserved.
// Released under the MIT license
// https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

using K4AdotNet.BodyTracking;
using K4AdotNet.Record;
using K4AdotNet.Sensor;
using MpAndKinectPoseSender.PoseInference;
using MpAndKinectPoseSender.Renderers;
using System;
using System.IO;

namespace MpAndKinectPoseSender
{
    class Program   
    {
        static void OnlineProcess()
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

        static void Main()
        {
            OnlineProcess();
        }
    }
}