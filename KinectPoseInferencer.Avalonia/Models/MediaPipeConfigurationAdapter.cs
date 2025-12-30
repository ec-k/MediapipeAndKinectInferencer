using KinectPoseInferencer.Core;
using KinectPoseInferencer.Core.Settings;
using Microsoft.Extensions.Options;

namespace KinectPoseInferencer.Avalonia.Models;

public class MediaPipeConfigurationAdapter : IMediaPipeConfiguration
{
    readonly MediaPipeSettings _settings;

    public MediaPipeConfigurationAdapter(IOptions<MediaPipeSettings> options)
    {
        _settings = options.Value;
    }

    public string ExecutablePath => _settings.ExecutablePath;
    public bool EnablePoseInference => _settings.EnablePoseInference;
    public bool EnableVisualizationWindow => _settings.EnableVisualizationWindow;
}
