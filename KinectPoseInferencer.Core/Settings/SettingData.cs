namespace KinectPoseInferencer.Core.Settings;

public record SettingData(
    string VideoFilePath = "",
    string InputLogFilePath = "",
    string MetaFilePath = ""
    );
