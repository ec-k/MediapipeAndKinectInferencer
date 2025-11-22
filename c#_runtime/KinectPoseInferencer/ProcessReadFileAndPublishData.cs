// Copyright(c) Microsoft Corporation. All rights reserved.
// Released under the MIT license
// https://github.com/microsoft/Azure-Kinect-Samples/blob/master/LICENSE

using K4AdotNet.BodyTracking;
using K4AdotNet.Sensor;
using KinectPoseInferencer.Renderers;
using System;
using KinectPoseInferencer.Playback;
using K4AdotNet.Record;
using System.IO;
using System.Threading;

namespace KinectPoseInferencer;

// This is temporal classes for run app with KinectPoseInferencer.Playback components.
internal class ProcessReadFileAndPublishData
{
    readonly IPlaybackController _playbackController;
    readonly Renderer _renderer;

    Device _device;
    Tracker _tracker;

    public ProcessReadFileAndPublishData(
        IPlaybackController playbackController,
        Renderer renderer)
    {
        _playbackController = playbackController??throw new ArgumentNullException(nameof(playbackController));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public void Run()
    {
        // Setup classes
        _renderer.StartVisualizationThread();

        // Setup a device
        var testVideoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "KinectAndInputRecorder",
            "test_video.mkv");                          // This should specified by settings or user (via UI)
        var recordConfig = new RecordConfiguration()    // This should specified by metafile
        {
            ColorFormat = ImageFormat.ColorBgra32,
            ColorResolution = ColorResolution.R720p,
            DepthMode = DepthMode.NarrowViewUnbinned,
            CameraFps = FrameRate.Thirty,
        };
        var playbackDesc = new PlaybackDescriptor(testVideoPath, recordConfig);
        _playbackController.Descriptor = playbackDesc;
        
        _playbackController.Start();

        // TODO: Add awaiting while cancel requested.
        while (true) {
            Thread.Sleep(100);
        }
    }

    public void Dispose()
    {
        _playbackController.Dispose();
        _device?.Dispose();
        _tracker?.Dispose();
    }
}
