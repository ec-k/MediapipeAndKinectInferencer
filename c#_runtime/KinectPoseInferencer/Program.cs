using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace KinectPoseInferencer
{
    internal static class Program
    {
        class AppStartupOptoins
        {
            public bool IsOffline { get; set; }
            public string VideoFilePath { get; set; }
        }

        internal static void Main(string[] args)
        {
            var options = ParseCommandLineArguments(args);

            using var serviceProvider = Build();
            if (options.IsOffline)
            {
                Console.WriteLine($"Running in OFFLINE mode (from video file: '{options.VideoFilePath}').");
                var appManager = serviceProvider.GetRequiredService<KinectOfflineProcessor>();
                appManager.Run(options.VideoFilePath);
            }
            else
            {
                Console.WriteLine("Running in ONLINE mode (from live Kinect stream).");
                var appManager = serviceProvider.GetRequiredService<KinectOnlineProcessor>();
                appManager.Run();
            }
        }

        static AppStartupOptoins ParseCommandLineArguments(string[] args)
        {
            var options = new AppStartupOptoins();
            options.IsOffline = args.Contains("--offline", StringComparer.OrdinalIgnoreCase);
            if (options.IsOffline)
            {
                var offlineIndex = Array.FindIndex(args, arg => arg.Equals("--offline", StringComparison.OrdinalIgnoreCase));
                if (offlineIndex != -1 && offlineIndex + 1 < args.Length)
                    options.VideoFilePath = args[offlineIndex + 1];

                if (string.IsNullOrWhiteSpace(options.VideoFilePath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: OFFLINE mode requires a video file path. Please specify it after --offline argument.");
                    Console.WriteLine("Example: dotnet run -- --offline \"KinectTestRecording.mkv\"");
                    Console.ResetColor();
                    Environment.Exit(1);
                }
            }

            return options;
        }

        static ServiceProvider Build()
        {
            var services = new ServiceCollection();

            // Register all services
            services.AddSingleton<Input.ActionMap>();
            services.AddSingleton<Input.KeyInputProvider>();
            services.AddSingleton<Input.UserActionService>();
            services.AddSingleton<Input.UserAction>();
            services.AddSingleton<PoseInference.LandmarkHandler>();
            services.AddSingleton<PoseInference.Filters.TiltCorrector>();
            services.AddSingleton<PoseInference.SkeletonToPoseLandmarksConverter>();
            services.AddSingleton<Renderers.Renderer>();
            services.AddSingleton<Logging.IResultLogWriter, Logging.HolisticJsonLogWriter>();
            services.AddSingleton<ImageWriter>();
            services.AddSingleton<FrameManager>();
            services.AddSingleton<KinectOnlineProcessor>();
            services.AddSingleton<KinectOfflineProcessor>();

            // Register filter chain
            services.AddSingleton<PoseInference.Filters.IPositionFilter, PoseInference.Filters.MilimeterToMeter>();
            services.AddSingleton<PoseInference.Filters.IPositionFilter, PoseInference.Filters.TiltCorrector>(
                provider => provider.GetRequiredService<PoseInference.Filters.TiltCorrector>()
                );
            services.AddSingleton<PoseInference.Filters.IPositionFilter, PoseInference.Filters.TransformCoordinator>();

            return services.BuildServiceProvider();
        }
    }
}
