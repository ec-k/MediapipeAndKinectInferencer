using System;

namespace KinectPoseInferencer.Input
{
    internal class KeyInputProvider
    {
        public event Action<string> OnKeyPressed;

        public void ReadInputAndNotify()
        {
            var key = Console.ReadKey(true).KeyChar.ToString().ToUpperInvariant();
            OnKeyInput(key);
        }

        void OnKeyInput(string key)
        {
            OnKeyPressed?.Invoke(key);
        }
    }
}
