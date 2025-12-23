using System.IO;
using System.Text.Json;

namespace KinectPoseInferencer.Settings;

public class SettingsManager
{
    const string SettingFileName = "settings.json";
    readonly string _settingFilePath = Path.Combine(ProjectConstants.AppDataDirectory, SettingFileName);
    readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public void Save(SettingData data)
    {
        if(!Directory.Exists(ProjectConstants.AppDataDirectory))
            Directory.CreateDirectory(ProjectConstants.AppDataDirectory);

        var json = JsonSerializer.Serialize<SettingData>(data, _options);
        File.WriteAllText(_settingFilePath, json);
    }

    public SettingData Load()
    {
        if (!File.Exists(_settingFilePath))
            return new SettingData();

        var json = File.ReadAllText(_settingFilePath);
        return JsonSerializer.Deserialize<SettingData>(json) ?? new SettingData();
    }
}
