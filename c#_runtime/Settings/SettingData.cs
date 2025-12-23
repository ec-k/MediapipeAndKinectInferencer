namespace KinectPoseInferencer.Settings;

public record SettingData(
    string VideoFilePath = "",
    string InputLogFilePath = "",
    string MetaFilePath = ""
    );
