using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.PoseInference;
using KinectPoseInferencer.Core.PoseInference.Filters;
using KinectPoseInferencer.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KinectPoseInferencer.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Core").Get<CoreSettings>()
                       ?? throw new InvalidOperationException("Core settings not found.");
        var receiverSettings          = settings.GetReceiverSettings();
        var resultReceiverEndPoint    = settings.GetResultReceiverEndPoint();
        var landmarkSenderEndPoint    = settings.GetLandmarkSenderEndPoint();
        var inputEventSenderEndPoints = settings.GetInputEventSenderEndPoints();
        var oneEuroFilterSettings     = settings.FilterSettings.OneEuroFilter;
        var appFps                    = settings.AppFrameRate;
        var mmfFilePath               = CreateMmfFile(settings.MmfFileName);

        // inferencer
        services.AddSingleton<KinectInferencer>();
        // result managers
        services.AddSingleton(sp =>
            new ResultManager(
                sp.GetRequiredService<UdpResultReceiver>(), receiverSettings)
            );
        services.AddSingleton(sp =>
            new UdpResultReceiver(resultReceiverEndPoint, receiverSettings)
            );
        // result processors
        services.AddSingleton<TiltCorrector>();
        services.AddSingleton<PoseInference.Utils.SkeletonToPoseLandmarksConverter>();
        services.AddSingleton(provider => new ImageWriter(mmfFilePath));
        // brokers
        services.AddSingleton<FrameManager>();
        services.AddSingleton<RecordDataBroker>();

        // Register filter chain
        services.AddSingleton<ILandmarkFilter, OneEuroFilter>(sp =>
            new OneEuroFilter(
                oneEuroFilterSettings.MinCutoff,
                oneEuroFilterSettings.Slope,
                oneEuroFilterSettings.DCutoff
                )
        );
        services.AddSingleton<ILandmarkFilter, MilimeterToMeter>();
        services.AddSingleton<ILandmarkFilter, TiltCorrector>(
            provider => provider.GetRequiredService<TiltCorrector>()
            );
        services.AddSingleton<ILandmarkFilter, TransformCoordinator>();

        // Register result users
        services.AddSingleton<ILandmarkUser>(sp => new LandmarkSender(landmarkSenderEndPoint));

        // Register input event users
        services.AddSingleton(sp => new InputEventSender(inputEventSenderEndPoints));

        // readers
        services.AddSingleton<KinectDeviceController>();
        services.AddSingleton<IPlaybackController, PlaybackController>(sp=>
            new PlaybackController(
                sp.GetRequiredService<IPlaybackReader>(),
                sp.GetRequiredService<InputLogReader>(),
                sp.GetRequiredService<RecordDataBroker>(),
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
                mmfFilePath)
        );

        return services;
    }

    static string CreateMmfFile(string fileName)
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

        return Path.Combine(appTmpDirectory, fileName);
    }
}
