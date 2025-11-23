using K4AdotNet.Record;
using K4AdotNet.Sensor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Windows;

using KinectPoseInferencer.Playback;
using KinectPoseInferencer.Playback.States;
using KinectPoseInferencer.Renderers;

namespace KinectPoseInferencer;

public partial class App : Application
{
    IHost? _host;

    public App()
    {
        var mmfFilePath = CreateMMFFile();
        _host = CreateHostBuilder(mmfFilePath).Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        using var scope = _host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var renderer = services.GetRequiredService<Renderer>();
        
        renderer.StartVisualizationThread();

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
        var playbackController = services.GetRequiredService<IPlaybackController>();
        playbackController.Descriptor = playbackDesc;
        playbackController.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        using var scope = _host.Services.CreateScope();
        var services = scope.ServiceProvider;
        services.GetService<IPlaybackController>()?.Dispose();
    }

    IHostBuilder CreateHostBuilder(string mmfFilePath) =>
        Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<App>();

                // Register all services
                services.AddSingleton<Input.ActionMap>();
                services.AddSingleton<Input.KeyInputProvider>();
                services.AddSingleton<Input.UserActionService>();
                services.AddSingleton<Input.UserAction>();
                services.AddSingleton<PoseInference.LandmarkHandler>();
                services.AddSingleton<PoseInference.Filters.TiltCorrector>();
                services.AddSingleton<PoseInference.SkeletonToPoseLandmarksConverter>();
                services.AddSingleton<Renderer>();
                services.AddSingleton(provider => new ImageWriter(mmfFilePath));
                services.AddSingleton<FrameManager>();

                // Register filter chain
                services.AddSingleton<PoseInference.Filters.IPositionFilter, PoseInference.Filters.MilimeterToMeter>();
                services.AddSingleton<PoseInference.Filters.IPositionFilter, PoseInference.Filters.TiltCorrector>(
                    provider => provider.GetRequiredService<PoseInference.Filters.TiltCorrector>()
                    );
                services.AddSingleton<PoseInference.Filters.IPositionFilter, PoseInference.Filters.TransformCoordinator>();

                services.AddSingleton<IPlaybackController, PlaybackController>();
                services.AddSingleton<IPlaybackReader, PlaybackReader>();
                services.AddTransient<IdleState>();
                services.AddTransient<PlayingState>();

                //services.AddSingleton<AppTrayIconViewModel>(sp =>
                //{
                //    var host = sp.GetRequiredService<IHost>();
                //    var engine = sp.GetRequiredService<IApplicationEngine>();

                //    Func<StatusSettingsWindow> factory = () =>
                //    {
                //        var viewModel = sp.GetRequiredService<StatusSettingsViewModel>();
                //        return new StatusSettingsWindow(viewModel);
                //    };

                //    return new AppTrayIconViewModel(host, factory, engine);
                //});
            });

    string CreateMMFFile()
    {
        var appTempDirectory = Path.Combine(Path.GetTempPath(), ProjectNameConstants.StudioName, ProjectNameConstants.AppName);
        var mmfFilePath = Path.Combine(appTempDirectory, "kinect_color_image.dat");
        if (!string.IsNullOrEmpty(appTempDirectory) && !Directory.Exists(appTempDirectory))
        {
            try
            {
                Directory.CreateDirectory(appTempDirectory);
                Console.WriteLine($"Created directory for ImageWriter: {appTempDirectory}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error creating directory '{appTempDirectory}': {ex.Message}");
                Environment.Exit(1);
            }
        }

        return mmfFilePath;
    }
}
