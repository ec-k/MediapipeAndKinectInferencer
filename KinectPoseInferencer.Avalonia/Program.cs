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
            var app = BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            await AppHost.Host.StopAsync();
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
