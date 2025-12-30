using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.PoseInference;
using KinectPoseInferencer.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

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
        var mmfFilePath               = CreateMmfFile(settings.MmfFileName);

        // inferencer
        services.AddSingleton<KinectInferencer>();
        // result managers
        services.AddSingleton(sp =>
            new ResultManager(
                sp.GetRequiredService<UdpResultReceiver>(), receiverSettings)
            );
        services.AddSingleton(serviceProvider =>
            new UdpResultReceiver(resultReceiverEndPoint, receiverSettings)
            );
        // result processors
        services.AddSingleton<PoseInference.Filters.TiltCorrector>();
        services.AddSingleton<PoseInference.Utils.SkeletonToPoseLandmarksConverter>();
        services.AddSingleton(provider => new ImageWriter(mmfFilePath));
        // brokers
        services.AddSingleton<FrameManager>();
        services.AddSingleton<RecordDataBroker>();

        // Register filter chain
        services.AddSingleton<PoseInference.Filters.ILandmarkFilter, PoseInference.Filters.MilimeterToMeter>();
        services.AddSingleton<PoseInference.Filters.ILandmarkFilter, PoseInference.Filters.TiltCorrector>(
            provider => provider.GetRequiredService<PoseInference.Filters.TiltCorrector>()
            );
        services.AddSingleton<PoseInference.Filters.ILandmarkFilter, PoseInference.Filters.TransformCoordinator>();

        // Register result users
        services.AddSingleton<ILandmarkUser>(serviceProvider => new LandmarkSender(landmarkSenderEndPoint));

        // Register input event users
        services.AddSingleton(serviceProvider => new InputEventSender(inputEventSenderEndPoints));

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

        // misc
        services.AddSingleton<MediaPipeProcessManager>();

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
