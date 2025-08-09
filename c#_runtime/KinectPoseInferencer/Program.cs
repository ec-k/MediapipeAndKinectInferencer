using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace KinectPoseInferencer
{
    internal static class Program
    {
        internal static void Main(string[] args)
        {
            var isOffline = args.Contains("--offline", StringComparer.OrdinalIgnoreCase);
            string videoFilePath = null;
            if (isOffline)
            {
                var offlineIndex = Array.FindIndex(args, arg => arg.Equals("--offline", StringComparison.OrdinalIgnoreCase));
                if(offlineIndex != -1 && offlineIndex + 1 < args.Length)
                    videoFilePath = args[offlineIndex + 1];
                if (string.IsNullOrWhiteSpace(videoFilePath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: OFFLINE mode requires a video file path. Please specify it after --offline argument.");
                    Console.WriteLine("Example: dotnet run -- --offline \"KinectTestRecording.mkv\"");
                    Console.ResetColor();
                    Environment.Exit(1);
                }
            }
            else
                Console.WriteLine("Running in online mode.");

            using var serviceProvider = Build();
            if (isOffline)
            {
                var appManager = serviceProvider.GetRequiredService<KinectOfflineProcessor>();
                appManager.Run(videoFilePath);
            }
            else
            {
                var appManager = serviceProvider.GetRequiredService<KinectOnlineProcessor>();
                appManager.Run();
            }
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
            services.AddSingleton<Logging.IPoseLogWriter, Logging.PoseLogWriter>();
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
