using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;


namespace KinectPoseInferencer.Core.InputHook;

/// <summary>
/// A static utility class for detecting global mouse and keyboard input.
/// This functionality works even when the application is in the background.
/// Detected events are published as static Action events on Unity's main thread.
/// Note: This functionality is available only on Windows platforms.
/// </summary>
public static class GlobalInputHook
{
    public static event Action<KeyboardEventData>? OnKeyboardEvent;
    public static event Action<MouseEventData>? OnMouseEvent;

    static IntPtr _keyboardHookId = IntPtr.Zero;
    static IntPtr _mouseHookId = IntPtr.Zero;

    // Keep references to delegates to prevent garbage collection.
    static WinApi.HookProc? _keyboardHookProc;
    static WinApi.HookProc? _mouseHookProc;

    // Use ConcurrentQueue for thread-safe event queuing, as hook callbacks might be invoked from separate threads.
    readonly static ConcurrentQueue<KeyboardEventData> _keyboardEventsQueue = new();
    readonly static ConcurrentQueue<MouseEventData> _mouseEventsQueue = new();

    public static bool IsHookActive { get; private set; }

    // ManualResetEvent to signal when new events are ready for processing
    readonly static ManualResetEvent _eventReady = new(false);

    static Task? _eventProcessingTask;
    static CancellationTokenSource? _processingCts;

    /// <summary>
    /// Initializes and starts global mouse and keyboard hooks.
    /// This method should be called once at application startup (e.g., from Start() of any MonoBehaviour in the scene).
    /// </summary>
    public static void StartHooks()
    {
        if (IsHookActive)
        {
            return;
        }

        _keyboardHookProc = KeyboardHookCallback;
        _mouseHookProc = MouseHookCallback;
        try
        {
            // Set up the keyboard hook (HookKeyboardLowLevel is a low-level global hook).
            // dwThreadId = 0 (all threads), hMod = IntPtr.Zero (hook procedure is in the executable).
            _keyboardHookId = WinApi.SetWindowsHookEx(WinApi.HookKeyboardLowLevel, _keyboardHookProc, IntPtr.Zero, 0);
            if (_keyboardHookId == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to start keyboard hook. Error code: {Marshal.GetLastWin32Error()}");
            }

            // Set up the mouse hook (HookMouseLowLevel is a low-level global hook).
            _mouseHookId = WinApi.SetWindowsHookEx(WinApi.HookMouseLowLevel, _mouseHookProc, IntPtr.Zero, 0);
            if (_mouseHookId == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to start mouse hook. Error code: {Marshal.GetLastWin32Error()}");
            }

            if (_keyboardHookId != IntPtr.Zero && _mouseHookId != IntPtr.Zero)
            {
                IsHookActive = true;
            }
            else
            {
                throw new InvalidOperationException("Neither global hook could be started.");
            }
        }
        catch (Exception e)
        {
            StopHooks();
            throw new InvalidOperationException($"An unexpected error occurred during initialization: {e.Message}");
        }
    }

    /// <summary>
    /// Releases global mouse and keyboard hooks.
    /// Typically called when the application quits or when global input is no longer needed.
    /// </summary>
    public static void StopHooks()
    {
        // If hooks are not active or already released
        if (!IsHookActive && _keyboardHookId == IntPtr.Zero && _mouseHookId == IntPtr.Zero)
        {
            return;
        }

        // Release keyboard hook
        if (_keyboardHookId != IntPtr.Zero)
        {
            bool success = WinApi.UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero; // Clear ID regardless of success
        }

        // Release mouse hook
        if (_mouseHookId != IntPtr.Zero)
        {
            bool success = WinApi.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero; // Clear ID regardless of success
        }

        IsHookActive = false;
    }

    public static void StartProcessingEvents()
    {
        if (_eventProcessingTask is not null && !_eventProcessingTask.IsCompleted)
            return;

        _processingCts = new();
        _eventProcessingTask = Task.Run(() => EventProcessingLoop(_processingCts.Token));
    }

    public static void StopProcessingEvents()
    {
        if(_processingCts is not null)
        {
            _processingCts.Cancel();
            _processingCts.Dispose();
            _processingCts = null;
        }
        _eventProcessingTask = null;
    }

    public static void EventProcessingLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _eventReady.WaitOne();
            _eventReady.Reset();

            while (_keyboardEventsQueue.TryDequeue(out var eventData))
                OnKeyboardEvent?.Invoke(eventData);
            while (_mouseEventsQueue.TryDequeue(out var eventData))
                OnMouseEvent?.Invoke(eventData);
        }
    }

    // Invoked by the OS, possibly on a non-Unity main thread
    private static IntPtr KeyboardHookCallback(int hookCode, IntPtr messageType, IntPtr dataPointer)
    {
        if (hookCode >= 0)
        {
            var keyboardHookStruct = Marshal.PtrToStructure<WinApi.KeyboardHookStruct>(dataPointer);

            var eventData = new KeyboardEventData
            {
                RawStopwatchTimestamp = Stopwatch.GetTimestamp(),
                KeyCode = (VirtualKeyCode)keyboardHookStruct.VirtualKeyCode,
                IsKeyDown = (messageType == (IntPtr)WinApi.MessageKeydown || messageType == (IntPtr)WinApi.MessageSystemKeydown)
            };

            // Determine modifier keys (Shift, Ctrl, Alt) state using GetKeyState.
            if ((WinApi.GetKeyState((int)VirtualKeyCode.LShift) & 0x8000) != 0 || (WinApi.GetKeyState((int)VirtualKeyCode.RShift) & 0x8000) != 0)
                eventData.ModifiersFlags |= ModifierKeyState.SHIFT;
            if ((WinApi.GetKeyState((int)VirtualKeyCode.LControl) & 0x8000) != 0 || (WinApi.GetKeyState((int)VirtualKeyCode.RControl) & 0x8000) != 0)
                eventData.ModifiersFlags |= ModifierKeyState.CONTROL;
            if ((WinApi.GetKeyState((int)VirtualKeyCode.LMenu) & 0x8000) != 0 || (WinApi.GetKeyState((int)VirtualKeyCode.RMenu) & 0x8000) != 0)
                eventData.ModifiersFlags |= ModifierKeyState.ALT;

            _keyboardEventsQueue.Enqueue(eventData);
            _eventReady.Set(); // Signal that new events are ready
        }

        // Pass the event to the next hook procedure.
        return WinApi.CallNextHookEx(_keyboardHookId, hookCode, messageType, dataPointer);
    }

    private static IntPtr MouseHookCallback(int hookCode, IntPtr messageType, IntPtr dataPointer)
    {
        if (hookCode >= 0)
        {
            var mouseHookStruct = Marshal.PtrToStructure<WinApi.MouseHookStruct>(dataPointer);

            var eventData = new MouseEventData
            {
                RawStopwatchTimestamp = Stopwatch.GetTimestamp(),
                X = mouseHookStruct.Pt.X,
                Y = mouseHookStruct.Pt.Y,
                IsMouseMoving = (messageType == (IntPtr)WinApi.MessageMousemove),
                IsWheelMoving = (messageType == (IntPtr)WinApi.MessageMousewheel)
            };

            switch ((uint)messageType)
            {
                case WinApi.MessageLButtonDown:
                    eventData.Button = MouseButton.LEFT;
                    eventData.IsButtonDown = true;
                    break;
                case WinApi.MessageLButtonUp:
                    eventData.Button = MouseButton.LEFT;
                    eventData.IsButtonDown = false;
                    break;
                case WinApi.MessageRButtonDown:
                    eventData.Button = MouseButton.RIGHT;
                    eventData.IsButtonDown = true;
                    break;
                case WinApi.MessageRButtonUp:
                    eventData.Button = MouseButton.RIGHT;
                    eventData.IsButtonDown = false;
                    break;
                case WinApi.MessageMButtonDown:
                    eventData.Button = MouseButton.MIDDLE;
                    eventData.IsButtonDown = true;
                    break;
                case WinApi.MessageMButtonUp:
                    eventData.Button = MouseButton.MIDDLE;
                    eventData.IsButtonDown = false;
                    break;
                case WinApi.MessageXButtonDown:
                    eventData.IsButtonDown = true;
                    eventData.Button = ((mouseHookStruct.MouseData >> 16) == 1) ? MouseButton.XBUTTON1 : MouseButton.XBUTTON2;
                    break;
                case WinApi.MessageXButtonUp:
                    eventData.IsButtonDown = false;
                    eventData.Button = ((mouseHookStruct.MouseData >> 16) == 1) ? MouseButton.XBUTTON1 : MouseButton.XBUTTON2;
                    break;
                case WinApi.MessageMousewheel:
                    eventData.WheelDelta = (short)(mouseHookStruct.MouseData >> 16);
                    break;
            }

            _mouseEventsQueue.Enqueue(eventData);
            _eventReady.Set(); // Signal that new events are ready
        }

        // Pass the event to the next hook procedure.
        return WinApi.CallNextHookEx(_mouseHookId, hookCode, messageType, dataPointer);
    }
}
