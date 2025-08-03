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
            using var visualizerData = new FrameManager();
            var renderer = new Renderer(visualizerData);

            renderer.StartVisualizationThread();

            // Open device.
            using Device device = Device.Open();
            device.StartCameras(new DeviceConfiguration()
            {
                CameraFPS = FPS.FPS30,
                ColorResolution = ColorResolution.R720p,
                DepthMode = DepthMode.NFOV_Unbinned,
                WiredSyncMode = WiredSyncMode.Standalone,
                ColorFormat = ImageFormat.ColorBGRA32,
            });

            var deviceCalibration = device.GetCalibration();
            PointCloud.ComputePointCloudCache(deviceCalibration);

            var tracker = Tracker.Create(
                deviceCalibration
                , new TrackerConfiguration() { 
                    ProcessingMode = TrackerProcessingMode.Gpu
                    , SensorOrientation = SensorOrientation.Default });
            using var imgWriter = new ImageWriter();

            device.StartImu();
            var imuSample = device.GetImuSample();
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
                using Frame frame = tracker.PopResult(TimeSpan.Zero, throwOnTimeout: false);
                if (frame != null)
                {
                    // Save this frame for visualization in Renderer.

                    // One can access frame data here and extract e.g. tracked bodies from it for the needed purpose.
                    // Instead, for simplicity, we transfer the frame object to the rendering background thread.
                    // This example shows t hat frame popped from tracker should be disposed. Since here it is used
                    // in a different thread, we use Reference method to prolong the lifetime of the frame object.
                    // For reference on how to read frame data, please take a look at Renderer.NativeWindow_Render().
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