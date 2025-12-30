namespace KinectPoseInferencer.Core;

public interface IMediaPipeConfiguration
{
    string ExecutablePath { get; }
    bool EnablePoseInference { get; }
    bool EnableVisualizationWindow { get; }
}
