using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace KinectPoseInferencer.RemoteControl;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteControlServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RemoteControlOptions>(configuration.GetSection(RemoteControlOptions.SectionName));

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RemoteControlOptions>>().Value;
            return ActivatorUtilities.CreateInstance<RemoteControlServer>(sp, options.Port);
        });

        services.AddHostedService<RemoteControlBackgroundService>();

        return services;
    }
}
