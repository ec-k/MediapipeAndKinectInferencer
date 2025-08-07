using System;
using System.Collections.Generic;

namespace KinectPoseInferencer.Input
{
    internal class ActionMap
    {
        public IReadOnlyDictionary<string, Action> KeyActions => _keyActions;
        Dictionary<string, Action> _keyActions = new();

        public void RegisterAction(string key, Action action)
        {
            if (_keyActions is null)
                _keyActions = new Dictionary<string, Action>();

            if (_keyActions.ContainsKey(key))
                _keyActions[key] = action;
            else
                _keyActions.Add(key, action);
        }

        public void UnregisterAction(string key)
        {
            if (_keyActions is not null && _keyActions.ContainsKey(key))
                _keyActions.Remove(key);
        }
    }
}
