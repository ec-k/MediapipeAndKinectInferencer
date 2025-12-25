using CliWrap;
using KinectPoseInferencer.Core;
using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.PoseInference;
using KinectPoseInferencer.Core.Settings;
using KinectPoseInferencer.WPF.UI;
using KinectPoseInferencer.WPF.Renderers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace KinectPoseInferencer.WPF;

public partial class App : Application
{
    IHost _host;
    CancellationTokenSource _forcefulCts = new();
    CancellationTokenSource _gracefulCts = new();

    public App()
    {
        var mmfFilePath = CreateMMFFile();
        _host = CreateHostBuilder(mmfFilePath).Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = _host.Services;

        var mainWindow = services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        var renderer = services.GetRequiredService<Renderer>();
        renderer.StartVisualizationThread();

        services.GetRequiredService<LandmarkPresenter>();
        services.GetRequiredService<InputLogPresenter>();
        services.GetRequiredService<CapturePresenter>();

        // Start MediaPipe process
        var _ = StartMediapipeProcess();
    }

    async Task StartMediapipeProcess()
    {
        try
        {
            var config = _host.Services.GetRequiredService<IOptions<MediaPipeSettings>>().Value;
            var exePath = config.ExecutablePath;
            if(string.IsNullOrWhiteSpace(exePath))
            {
                MessageBox.Show("MediaPipe Inferencer .exe path is not specified at MediaPipeSettings:ExecutablePath.");
                return;
            }

            var fullPath = Path.GetFullPath(exePath, AppContext.BaseDirectory);

            if (!File.Exists(fullPath))
            {
                MessageBox.Show($"MediaPipe Inferencer .exe is not found: {fullPath}");
                return;
            }

                await Cli.Wrap(fullPath)
                         .ExecuteAsync(_forcefulCts.Token, _gracefulCts.Token);
        }
        catch (OperationCanceledException) { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _gracefulCts.Cancel();
        _forcefulCts.CancelAfter(TimeSpan.FromSeconds(3));

        if(_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
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
                services.AddSingleton<Core.PoseInference.Filters.TiltCorrector>();
                services.AddSingleton<Core.PoseInference.Utils.SkeletonToPoseLandmarksConverter>();
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
            });

    string CreateMMFFile()
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
}
