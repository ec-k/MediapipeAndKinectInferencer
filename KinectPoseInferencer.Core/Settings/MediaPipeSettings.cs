namespace KinectPoseInferencer.Core.Settings;

public class MediaPipeSettings
{
    public string ExecutablePath { get; set; } = string.Empty;
    public bool EnablePoseInference { get; set; } = false;
    public bool EnableVisualizationWindow { get; set; } = false;
}
