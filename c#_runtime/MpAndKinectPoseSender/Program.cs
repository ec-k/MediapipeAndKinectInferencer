// Copyright(c) Microsoft Corporation. All rights reserved.
// Released under the MIT license
// https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;
using MpAndKinectPoseSender.Renderers;
using MpAndKinectPoseSender.PoseInference;
using System;

namespace MpAndKinectPoseSender
{
    class Program   
    {
        static void Main()
        {
            // Setup classes
            using var visualizerData = new FrameManager();
            var renderer = new Renderer(visualizerData);
            renderer.StartVisualizationThread();
            using var imgWriter = new ImageWriter();

            // Setup a device
            using var device = Device.Open();
            device.StartCameras(new DeviceConfiguration()
            {
                CameraFPS = FPS.FPS30,
                ColorResolution = ColorResolution.R720p,
                DepthMode = DepthMode.NFOV_Unbinned,
                WiredSyncMode = WiredSyncMode.Standalone,
                ColorFormat = ImageFormat.ColorBGRA32,
            });
            var deviceCalibration = device.GetCalibration();
            var tracker = Tracker.Create(
                deviceCalibration
                , new TrackerConfiguration() { 
                    ProcessingMode = TrackerProcessingMode.Gpu
                    , SensorOrientation = SensorOrientation.Default });
            device.StartImu();
            var imuSample = device.GetImuSample();

            // Setup for this app which requires device settings
            PointCloud.ComputePointCloudCache(deviceCalibration);
            using var landmarkHandler = new LandmarkHandler(imuSample, deviceCalibration);

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
                using var frame = tracker.PopResult(TimeSpan.Zero, throwOnTimeout: false);
                if (frame != null)
                {
                    visualizerData.Frame = frame.Reference();


                    // Write color image to thw Memory Mapped File
                    try{
                        var colorImg = frame.Capture.Color;
                        if (colorImg != null)
                        {
                            var bgraArr = colorImg.GetPixels<BGRA>().ToArray();
                            imgWriter.Write(bgraArr);
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    // Send Landmarks to a pose solver app
                    if (frame.NumberOfBodies > 0)
                    {
                        var skeleton = frame.GetBodySkeleton(0);
                        landmarkHandler.Update(skeleton);
                        landmarkHandler.SendResults();
                    }
                }
            }
        }
    }
}