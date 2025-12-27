using Avalonia;
using KinectPoseInferencer.Renderers;
using KinectPoseInferencer.Core;
using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.PoseInference;
using KinectPoseInferencer.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Net;
using KinectPoseInferencer.Avalonia.Models;


namespace KinectPoseInferencer.Avalonia;

public static class AppHost
{
    public static IHost? Host { get; set; }
}

internal sealed class Program
{

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var mmfFilePath = CreateMMFFile();
        var host = CreateHostBuilder(mmfFilePath).Build();

        AppHost.Host = host;

        var app = BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    static string CreateMMFFile()
    {
        var appTmpDirectory = ProjectConstants.AppTmpDirecotry;
        if (!string.IsNullOrEmpty(appTmpDirectory) && !Directory.Exists(appTmpDirectory))
        {
            try
            {
                Directory.CreateDirectory(appTmpDirectory);
                Console.WriteLine($"Created directory for ImageWriter: {appTmpDirectory}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error creating directory '{appTmpDirectory}': {ex.Message}");
                Environment.Exit(1);
            }
        }

        return Path.Combine(appTmpDirectory, "kinect_color_image.dat");
    }

    static IHostBuilder CreateHostBuilder(string mmfFilePath) =>
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
                services.AddSingleton<Core.PoseInference.Filters.TiltCorrector>();
                services.AddSingleton<Core.PoseInference.Utils.SkeletonToPoseLandmarksConverter>();
                // renderers
                services.AddSingleton<Renderer>();
                services.AddSingleton(provider => new ImageWriter(mmfFilePath));
                // brokers
                services.AddSingleton<FrameManager>();
                services.AddSingleton<RecordDataBroker>();
                services.AddSingleton<Views.MainWindow>();
                // ui
                services.AddSingleton<ViewModels.MainWindowViewModel>();

                // Register filter chain
                services.AddSingleton<Core.PoseInference.Filters.ILandmarkFilter, Core.PoseInference.Filters.MilimeterToMeter>();
                services.AddSingleton<Core.PoseInference.Filters.ILandmarkFilter, Core.PoseInference.Filters.TiltCorrector>(
                    provider => provider.GetRequiredService<Core.PoseInference.Filters.TiltCorrector>()
                    );
                services.AddSingleton<Core.PoseInference.Filters.ILandmarkFilter, Core.PoseInference.Filters.TransformCoordinator>();

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

                // settings
                services.AddSingleton<SettingsManager>();
                services.Configure<MediaPipeSettings>(context.Configuration.GetSection("MediaPipeSettings"));
                services.AddSingleton<IMediaPipeConfiguration, MediaPipeConfigurationAdapter>();

                // misc
                services.AddSingleton<MediaPipeProcessManager>();
            });
}
