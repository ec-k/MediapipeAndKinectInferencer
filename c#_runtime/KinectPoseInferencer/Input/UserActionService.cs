using System;

namespace KinectPoseInferencer.Input
{
    internal class UserActionService: IDisposable
    {
        readonly ActionMap _actionMap;
        readonly KeyInputProvider _keyInputProvider;
        readonly UserAction _userAction;

        internal UserActionService(
            ActionMap actionMap,
            KeyInputProvider keyInputProvider,
            UserAction userAction)
        {
            _actionMap = actionMap ?? throw new ArgumentNullException(nameof(actionMap));
            _keyInputProvider = keyInputProvider ?? throw new ArgumentNullException(nameof(keyInputProvider));
            _userAction = userAction ?? throw new ArgumentNullException(nameof(userAction));

            Initizlie();
        }

        void Initizlie()
        {
            _actionMap.RegisterAction("C", _userAction.Calibrate);
            _actionMap.RegisterAction("R", _userAction.ResetCalibrationSetting);

            _keyInputProvider.OnKeyPressed += HandleKeyPressed;
        }

        void HandleKeyPressed(string key)
        {
            if(_actionMap.KeyActions.TryGetValue(key, out var action))
            {
                action?.Invoke();
            }
            else
            {
                Console.WriteLine($"No action registered for key: {key}");
            }
        }

        void IDisposable.Dispose()
        {
            _keyInputProvider.OnKeyPressed -= HandleKeyPressed;

            _actionMap.UnregisterAction("C");
            _actionMap.UnregisterAction("R");
        }
    }
}
