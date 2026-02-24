using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using KinectPoseInferencer.Avalonia.ViewModels;
using KinectPoseInferencer.Avalonia.Views;
using KinectPoseInferencer.Avalonia.Models;
using KinectPoseInferencer.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace KinectPoseInferencer.Avalonia
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (AppHost.Host is null)
                return;

            var services = AppHost.Host!.Services;
            services.GetRequiredService<Core.PoseInference.LandmarkPresenter>();
            services.GetRequiredService<Core.PoseInference.InputLogPresenter>();
            services.GetRequiredService<Core.Playback.CapturePresenter>();
            var logger = services.GetRequiredService<ILogger<App>>();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var lifetime = services.GetRequiredService<IHostApplicationLifetime>();

                var viewSettings = services.GetRequiredService<IOptions<ViewSettings>>().Value;
                Renderers.Renderer? renderer = null;
                if (viewSettings.EnableSkeletonVisualization)
                {
                    renderer = services.GetRequiredService<Renderers.Renderer>();
                    renderer.StartVisualizationThread();
                }

                var mediapipe = services.GetRequiredService<MediaPipeProcessManager>();
                lifetime.ApplicationStopping.Register(mediapipe.StopProcess);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await mediapipe.StartMediapipeProcessAsync(lifetime.ApplicationStopping);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"MediaPipe Process Error: {ex.Message}");
                    }
                });

                // Stop renderer when application exits
                desktop.Exit += (sender, args) => renderer?.Stop();

                // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = services.GetRequiredService<MainWindowViewModel>()
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}