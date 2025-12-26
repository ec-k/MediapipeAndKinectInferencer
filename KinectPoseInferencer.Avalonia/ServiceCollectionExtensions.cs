using System.Net;
using Microsoft.Extensions.DependencyInjection;

using KinectPoseInferencer.Renderers;
using KinectPoseInferencer.Core;
using KinectPoseInferencer.Core.PoseInference;
using KinectPoseInferencer.Core.Playback;
using KinectPoseInferencer.Core.Settings;
using Microsoft.Extensions.Configuration;

namespace KinectPoseInferencer.Avalonia;

internal static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection, IConfiguration configuration, string mmfFilePath)
    {
        collection.AddSingleton<App>();

        // inferencer
        collection.AddSingleton<KinectInferencer>();
        // result managers
        collection.AddSingleton(sp =>
            new ResultManager(
                sp.GetRequiredService<UdpResultReceiver>(),
                ReceiverEventSettings.Face | ReceiverEventSettings.LeftHand | ReceiverEventSettings.RightHand)
            );
        collection.AddSingleton(serviceProvider =>
            new UdpResultReceiver(ReceiverEventSettings.Face | ReceiverEventSettings.LeftHand | ReceiverEventSettings.RightHand, 9001)
            );
        // result processors
        collection.AddSingleton<Core.PoseInference.Filters.TiltCorrector>();
        collection.AddSingleton<Core.PoseInference.Utils.SkeletonToPoseLandmarksConverter>();
        // renderers
        collection.AddSingleton<Renderer>();
        collection.AddSingleton(provider => new ImageWriter(mmfFilePath));
        // brokers
        collection.AddSingleton<FrameManager>();
        collection.AddSingleton<RecordDataBroker>();
        collection.AddSingleton<Views.MainWindow>();
        // ui
        collection.AddSingleton<ViewModels.MainWindowViewModel>();

        // Register filter chain
        collection.AddSingleton<Core.PoseInference.Filters.ILandmarkFilter, Core.PoseInference.Filters.MilimeterToMeter>();
        collection.AddSingleton<Core.PoseInference.Filters.ILandmarkFilter, Core.PoseInference.Filters.TiltCorrector>(
            provider => provider.GetRequiredService<Core.PoseInference.Filters.TiltCorrector>()
            );
        collection.AddSingleton<Core.PoseInference.Filters.ILandmarkFilter, Core.PoseInference.Filters.TransformCoordinator>();

        // Register result users
        collection.AddSingleton<ILandmarkUser>(serviceProvider => new LandmarkSender("127.0.0.1", 22000));

        // Register input event users
        collection.AddSingleton(serviceProvider =>
            new InputEventSender(
                new IPEndPoint[] {
                    new(IPAddress.Parse("127.0.0.1"), 9002 ),
                    new(IPAddress.Parse("127.0.0.1"), 9003 )
                })
            );

        // readers
        collection.AddSingleton<KinectDeviceController>();
        collection.AddSingleton<IPlaybackController, PlaybackController>();
        collection.AddSingleton<IPlaybackReader, PlaybackReader>();
        collection.AddSingleton<InputLogReader>();

        // presenters
        collection.AddSingleton<LandmarkPresenter>();
        collection.AddSingleton<CapturePresenter>();
        collection.AddSingleton<InputLogPresenter>();

        // settings
        collection.AddSingleton<SettingsManager>();

        // MediaPipe
        collection.AddSingleton<MediaPipeProcessManager>();
        collection.Configure<MediaPipeSettings>(configuration.GetSection("MediaPipeSettings"));
    }
}
