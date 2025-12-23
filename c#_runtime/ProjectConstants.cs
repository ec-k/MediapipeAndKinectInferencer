using System;
using System.IO;

namespace KinectPoseInferencer;

public static class ProjectConstants
{
    public const string StudioName = "AtelierRC";
    public const string AppName = "MediapipeAndKinectInferencer";
    public static string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        StudioName,
        AppName);
    public static string AppTmpDirecotry => Path.Combine(
        Path.GetTempPath(),
        StudioName,
        AppName);
};