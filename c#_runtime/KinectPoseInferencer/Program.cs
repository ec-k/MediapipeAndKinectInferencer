using System;
using System.Collections.Generic;
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
            public string LogFileDestination { get; set; }
        }

        internal static void Main(string[] args)
        {
            var options = ParseCommandLineArguments(args);

            using var serviceProvider = Build();
            if (options.IsOffline)
            {
                Console.WriteLine($"Running in OFFLINE mode (from video file: '{options.VideoFilePath}').");
                var appManager = serviceProvider.GetRequiredService<KinectOfflineProcessor>();
                appManager.Run(options.VideoFilePath, options.LogFileDestination);
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
            if (!options.IsOffline)
            {
                options.IsOffline = false;
                return options; // In online mode, no further arguments are needed
            }

            var argMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.StartsWith("--") || arg.StartsWith("-"))
                {
                    var key = arg.TrimStart('-');
                    if (key.Equals("I", StringComparison.OrdinalIgnoreCase)) key = "input-video-path";
                    if (key.Equals("O", StringComparison.OrdinalIgnoreCase)) key = "output-log-destination";

                    // Get the next argument as value if it is a valid value
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--") && !args[i + 1].StartsWith("-"))
                    {
                        argMap[key] = args[i + 1];
                        i++; // Skip the next argument as it is the value for the current key
                    }
                    else
                    {
                        argMap[key] = string.Empty; // No value provided
                    }
                }
            }

            // Parse --input-video-path
            if (argMap.ContainsKey("input-video-path"))
            {
                options.VideoFilePath = argMap["input-video-path"];

                if (string.IsNullOrWhiteSpace(options.VideoFilePath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: --input-video-path requires a file path.");
                    Console.WriteLine("Example: dotnet run -- --input-video-path \"KinectTestRecording.mkv\"");
                    Console.ResetColor();
                    Environment.Exit(1);
                }
            }

            // Parse --output-log-destination
            if (argMap.ContainsKey("output-log-destination"))
            {
                options.LogFileDestination = argMap["output-log-destination"];
                if (string.IsNullOrWhiteSpace(options.LogFileDestination))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: --output-log-destination requires a file path.");
                    Console.WriteLine("Example: dotnet run -- --output-log-destination \"output.log\"");
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
