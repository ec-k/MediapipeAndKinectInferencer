using Microsoft.Extensions.DependencyInjection;

namespace KinectPoseInferencer
{
    class Program   
    {
        static string _videoFilePath = "KinectTestRecording.mkv";

        static void Main()
        {
            using var serviceProvider = Build();
            var appManager = serviceProvider.GetRequiredService<AppManager>();
            appManager.RunOnlineProcess();
        }

        static ServiceProvider Build()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Input.ActionMap>();
            services.AddSingleton<Input.KeyInputProvider>();
            services.AddSingleton<Input.UserActionService>();
            services.AddSingleton<Input.UserAction>();
            services.AddSingleton<PoseInference.LandmarkHandler>();
            services.AddSingleton<PoseInference.TiltCorrector>();
            services.AddSingleton<Renderers.Renderer>();
            services.AddSingleton<ImageWriter>();
            services.AddSingleton<FrameManager>();
            services.AddSingleton<AppManager>();
            return services.BuildServiceProvider();
        }
    }
}
