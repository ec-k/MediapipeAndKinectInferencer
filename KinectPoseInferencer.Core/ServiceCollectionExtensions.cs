using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.PoseInference;
using KinectPoseInferencer.Core.PoseInference.Filters;
using KinectPoseInferencer.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KinectPoseInferencer.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
        });
        var logger = loggerFactory.CreateLogger(typeof(ServiceCollectionExtensions));

        var settings = configuration.GetSection("Core").Get<CoreSettings>()
                       ?? throw new InvalidOperationException("Core settings not found.");
        var receiverSettings          = settings.GetReceiverSettings();
        var resultReceiverEndPoint    = settings.GetResultReceiverEndPoint();
        var landmarkSenderEndPoint    = settings.GetLandmarkSenderEndPoint();
        var inputEventSenderEndPoints = settings.GetInputEventSenderEndPoints();
        var oneEuroFilterSettings     = settings.FilterSettings.OneEuroFilter;
        var appFps                    = settings.AppFrameRate;
        var mmfFilePath               = CreateMmfFile(settings.MmfFileName, logger);

        // inferencer
        services.AddSingleton<KinectInferencer>();
        // result managers
        services.AddSingleton(sp =>
            new ResultManager(
                sp.GetRequiredService<UdpResultReceiver>(), receiverSettings)
            );
        services.AddSingleton(sp =>
            new UdpResultReceiver(
                resultReceiverEndPoint,
                receiverSettings,
                sp.GetRequiredService<ILogger<UdpResultReceiver>>())
            );
        // result processors
        services.AddSingleton<TiltCorrector>();
        services.AddSingleton<PoseInference.Utils.SkeletonToPoseLandmarksConverter>();
        services.AddSingleton(sp => 
        new ImageWriter(
            mmfFilePath,
            sp.GetRequiredService<ILogger<ImageWriter>>())
        );
        // brokers
        services.AddSingleton<FrameManager>();
        services.AddSingleton<RecordDataBroker>();

        // filters
        services.AddSingleton(oneEuroFilterSettings);
        services.AddSingleton<LandmarkFilterFactory>();
        services.AddSingleton<MilimeterToMeter>();
        services.AddSingleton<TransformCoordinator>();

        // Register result users
        services.AddSingleton<ILandmarkUser>(sp => new LandmarkSender(landmarkSenderEndPoint));

        // Register input event users
        services.AddSingleton(sp => 
        new InputEventSender(
            inputEventSenderEndPoints,
            sp.GetRequiredService<ILogger<InputEventSender>>())
        );

        // readers
        services.AddSingleton<KinectDeviceController>();
        services.AddSingleton<IPlaybackController, PlaybackController>(sp=>
            new PlaybackController(
                sp.GetRequiredService<IPlaybackReader>(),
                sp.GetRequiredService<InputLogReader>(),
                sp.GetRequiredService<RecordDataBroker>(),
                sp.GetRequiredService<ILogger<PlaybackController>>(),
                sp.GetRequiredService<KinectInferencer>(),
                appFps)
        );
        services.AddSingleton<IPlaybackReader, PlaybackReader>();
        services.AddSingleton<InputLogReader>();

        // presenters
        services.AddSingleton<LandmarkPresenter>();
        services.AddSingleton<CapturePresenter>();
        services.AddSingleton<InputLogPresenter>();

        // settings
        services.AddSingleton<SettingsManager>();

        // misc
        services.AddSingleton(sp => 
            new MediaPipeProcessManager(
                sp.GetRequiredService<IMediaPipeConfiguration>(),
                mmfFilePath,
                sp.GetRequiredService<ILogger<MediaPipeProcessManager>>())
        );

        return services;
    }

    static string CreateMmfFile(string fileName, ILogger logger)
    {
        var appTmpDirectory = ProjectConstants.AppTmpDirecotry;
        if (!string.IsNullOrEmpty(appTmpDirectory) && !Directory.Exists(appTmpDirectory))
        {
            try
            {
                Directory.CreateDirectory(appTmpDirectory);
                logger.LogInformation($"Created directory for ImageWriter: {appTmpDirectory}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error creating directory '{appTmpDirectory}': {ex.Message}");
                Environment.Exit(1);
            }
        }

        return Path.Combine(appTmpDirectory, fileName);
    }
}
