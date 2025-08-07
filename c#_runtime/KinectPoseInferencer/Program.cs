namespace KinectPoseInferencer
{
    class Program   
    {
        static string _videoFilePath = "KinectTestRecording.mkv";

        static void Main()
        {
            var appManager = new AppManager();
            //appManager.RunOfflineProcess(_videoFilePath);
            appManager.RunOnlineProcess();
        }
    }
}
