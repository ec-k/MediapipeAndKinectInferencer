using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Windows;

using KinectPoseInferencer.Playback;
using KinectPoseInferencer.UI;
using KinectPoseInferencer.PoseInference;
using System.Net;
using KinectPoseInferencer.Renderers.Unused;

namespace KinectPoseInferencer;

public partial class App : Application
{
    IHost _host;

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

        var mainWindow = services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        var renderer = services.GetRequiredService<Renderer>();
        renderer.StartVisualizationThread();

        services.GetRequiredService<LandmarkPresenter>();
        services.GetRequiredService<InputLogPresenter>();
        services.GetRequiredService<CapturePresenter>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        using var scope = _host.Services.CreateScope();
        var services = scope.ServiceProvider;
        services.GetService<IPlaybackController>()?.Dispose();
        services.GetService<LandmarkPresenter>()?.Dispose();
    }

    IHostBuilder CreateHostBuilder(string mmfFilePath) =>
        Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<App>();

                // inferencer
                services.AddSingleton<KinectInferencer>();
                // result managers
                services.AddSingleton(sp =>
                    new ResultManager(
                        sp.GetRequiredService<UdpResultReceiver>(),
                        ReceiverEventSettings.Face | ReceiverEventSettings.LeftHand | ReceiverEventSettings.RightHand)
                    );
                services.AddSingleton(serviceProvider =>
                    new UdpResultReceiver(ReceiverEventSettings.Face | ReceiverEventSettings.LeftHand | ReceiverEventSettings.RightHand, 9001)
                    );
                // result processors
                services.AddSingleton<PoseInference.Filters.TiltCorrector>();
                services.AddSingleton<PoseInference.Utils.SkeletonToPoseLandmarksConverter>();
                // renderers
                services.AddSingleton<Renderer>();
                services.AddSingleton(provider => new ImageWriter(mmfFilePath));
                // brokers
                services.AddSingleton<FrameManager>();
                services.AddSingleton<RecordDataBroker>();
                services.AddSingleton<MainWindow>();
                // ui
                services.AddSingleton<MainWindowViewModel>();

                // Register filter chain
                services.AddSingleton<PoseInference.Filters.ILandmarkFilter, PoseInference.Filters.MilimeterToMeter>();
                services.AddSingleton<PoseInference.Filters.ILandmarkFilter, PoseInference.Filters.TiltCorrector>(
                    provider => provider.GetRequiredService<PoseInference.Filters.TiltCorrector>()
                    );
                services.AddSingleton<PoseInference.Filters.ILandmarkFilter, PoseInference.Filters.TransformCoordinator>();

                // Register result users
                services.AddSingleton<ILandmarkUser>(serviceProvider => new LandmarkSender("127.0.0.1", 22000));

                // Register input event users
                services.AddSingleton(serviceProvider => 
                    new InputEventSender(
                        new IPEndPoint[] {
                            new(IPAddress.Parse("127.0.0.1"), 9002 ),
                            new(IPAddress.Parse("127.0.0.1"), 9003 )
                        })
                    );

                // readers
                services.AddSingleton<KinectDeviceController>();
                services.AddSingleton<IPlaybackController, PlaybackController>();
                services.AddSingleton<IPlaybackReader, PlaybackReader>();
                services.AddSingleton<InputLogReader>();

                // presenters
                services.AddSingleton<LandmarkPresenter>();
                services.AddSingleton<CapturePresenter>();
                services.AddSingleton<InputLogPresenter>();
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
