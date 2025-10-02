using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace KinectPoseInferencer
{
    public enum LogOutputFormat
    {
        Protobuf,
        Json
    }

    internal static class Program
    {
        class AppStartupOptoins
        {
            public bool IsOffline { get; set; }
            public string VideoFilePath { get; set; }
            public string LogFileDestination { get; set; }
            public LogOutputFormat LogOutputFormat { get; set; } = LogOutputFormat.Protobuf;
            public string ImageOutputPath { get; set; }
        }

        internal static void Main(string[] args)
        {
            var options = ParseCommandLineArguments(args);

            using var serviceProvider = Build(options);
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

            var argMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.StartsWith("--") || arg.StartsWith("-"))
                {
                    var key = arg.TrimStart('-');
                    if (key.Equals("I", StringComparison.OrdinalIgnoreCase)) key = "input-video-path";
                    if (key.Equals("O", StringComparison.OrdinalIgnoreCase)) key = "output-log-destination";
                    if (key.Equals("F", StringComparison.OrdinalIgnoreCase)) key = "log-format";
                    if (key.Equals("IOP", StringComparison.OrdinalIgnoreCase)) key = "image-output-path";

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

            // Parsing logics

            // --offline
            if (argMap.ContainsKey("offline"))
            {
                options.IsOffline = true;
            }

            // --image-output-path
            if (argMap.ContainsKey("image-output-path"))
            {
                options.ImageOutputPath = argMap["image-output-path"];
                if (string.IsNullOrWhiteSpace(options.ImageOutputPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: --image-output-path requires a file path.");
                    Console.WriteLine("Example: dotnet run -- --image-output-path \"C:\\temp\\colorImg.dat\"");
                    Console.ResetColor();
                    Environment.Exit(1);
                }
            }

            if (!options.IsOffline)
                return options;

            // --input-video-path
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

            // --output-log-destination
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

            // --log-format
            if (argMap.ContainsKey("log-format"))
            {
                var formatString = argMap["log-format"];
                if (string.IsNullOrWhiteSpace(formatString))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: --log-format requires a format type (protobuf or json).");
                    Console.WriteLine("Example: dotnet run -- --log-format json");
                    Console.ResetColor();
                    Environment.Exit(1);
                }

                if (Enum.TryParse(formatString, true, out LogOutputFormat format))
                {
                    options.LogOutputFormat = format;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: Unknown log format '{formatString}'. Supported formats are 'protobuf' and 'json'.");
                    Console.ResetColor();
                    Environment.Exit(1);
                }
            }

            return options;
        }

        static ServiceProvider Build(AppStartupOptoins options)
        {
            var services = new ServiceCollection();

            // Get memory mapped file path
            var finalImageOutputPath = options.ImageOutputPath;
            Console.WriteLine($"ImageWriter path (from command-line): {finalImageOutputPath}");
            var directoryPath = Path.GetDirectoryName(finalImageOutputPath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                try
                {
                    Directory.CreateDirectory(directoryPath);
                    Console.WriteLine($"Created directory for ImageWriter: {directoryPath}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error creating directory '{directoryPath}': {ex.Message}");
                    Environment.Exit(1);
                }
            }

            // Register all services
            services.AddSingleton<Input.ActionMap>();
            services.AddSingleton<Input.KeyInputProvider>();
            services.AddSingleton<Input.UserActionService>();
            services.AddSingleton<Input.UserAction>();
            services.AddSingleton<PoseInference.LandmarkHandler>();
            services.AddSingleton<PoseInference.Filters.TiltCorrector>();
            services.AddSingleton<PoseInference.SkeletonToPoseLandmarksConverter>();
            services.AddSingleton<Renderers.Renderer>();
            services.AddSingleton(provider => new ImageWriter(finalImageOutputPath));
            services.AddSingleton<FrameManager>();
            services.AddSingleton<KinectOnlineProcessor>();
            services.AddSingleton<KinectOfflineProcessor>();

            switch (options.LogOutputFormat)
            {
                case LogOutputFormat.Protobuf:
                    services.AddSingleton<Logging.IResultLogWriter, Logging.HolisticProtobufLogWriter>();
                    Console.WriteLine("Log output format set to Protobuf.");
                    break;
                case LogOutputFormat.Json:
                    services.AddSingleton<Logging.IResultLogWriter, Logging.HolisticJsonLogWriter>();
                    Console.WriteLine("Log output format set to JSON.");
                    break;
                default:
                    services.AddSingleton<Logging.IResultLogWriter, Logging.HolisticProtobufLogWriter>();
                    Console.WriteLine("Invalid log output format. Defaulting to Protobuf.");
                    break;
            }

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
