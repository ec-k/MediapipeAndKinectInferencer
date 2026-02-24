using Avalonia;
using KinectPoseInferencer.Renderers;
using KinectPoseInferencer.RemoteControl;
using KinectPoseInferencer.Core;
using KinectPoseInferencer.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using KinectPoseInferencer.Avalonia.Models;
using Microsoft.Extensions.Logging;

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
    public static async Task Main(string[] args)
    {
        AppHost.Host = CreateHostBuilder().Build();
        AppHost.Host.Start();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            var logger = AppHost.Host.Services.GetRequiredService<ILogger<Program>>();

            // Stop MediaPipe process before force exit
            var mediapipe = AppHost.Host.Services.GetService<MediaPipeProcessManager>();
            mediapipe?.StopProcess();

            // Force exit after timeout if Host.StopAsync hangs
            using var forceExitTimer = new System.Threading.Timer(
                _ => Environment.Exit(0), null, 2000, System.Threading.Timeout.Infinite);

            try
            {
                await AppHost.Host.StopAsync(TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Host.StopAsync did not complete gracefully");
            }

            AppHost.Host.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    static IHostBuilder CreateHostBuilder() =>
        Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<App>();

                services.AddSingleton<Views.MainWindow>();
                services.AddSingleton<ViewModels.MainWindowViewModel>();
                services.AddSingleton<Renderer>();

                services.Configure<MediaPipeSettings>(context.Configuration.GetSection("MediaPipeSettings"));
                services.AddSingleton<IMediaPipeConfiguration, MediaPipeConfigurationAdapter>();

                services.Configure<ViewSettings>(context.Configuration.GetSection("ViewSettings"));

                services.AddCoreServices(context.Configuration);
                services.AddRemoteControlServer(context.Configuration);
            });
}
