using System;
using System.Runtime.InteropServices;

namespace KinectPoseInferencer.InputHook;

// Defines Windows API P/Invoke declarations.
// Uses C# PascalCase naming conventions while providing original Windows API names in comments for reference.
internal static class WinApi
{
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    internal static extern short GetKeyState(int nVirtKey);


    // Hook Types (idHook parameter)
    internal const int HookKeyboardLowLevel = 13;      // WH_KEYBOARD_LL: Low-level keyboard hook
    internal const int HookMouseLowLevel = 14;         // WH_MOUSE_LL   : Low-level mouse hook

    // Keyboard Messages
    internal const int MessageKeydown = 0x0100;        // WM_KEYDOWN    : Key pressed
    internal const int MessageKeyup = 0x0101;          // WM_KEYUP      : Key released
    internal const int MessageSystemKeydown = 0x0104;  // WM_SYSKEYDOWN : Alt key pressed (system key)
    internal const int MessageSystemKeyup = 0x0105;    // WM_SYSKEYUP   : Alt key released (system key)

    // Mouse Messages
    internal const int MessageMousemove = 0x0200;      // WM_MOUSEMOVE  : Mouse moved
    internal const int MessageLButtonDown = 0x0201;    // WM_LBUTTONDOWN: Left mouse button pressed
    internal const int MessageLButtonUp = 0x0202;      // WM_LBUTTONUP  : Left mouse button released
    internal const int MessageRButtonDown = 0x0204;    // WM_RBUTTONDOWN: Right mouse button pressed
    internal const int MessageRButtonUp = 0x0205;      // WM_RBUTTONUP  : Right mouse button released
    internal const int MessageMButtonDown = 0x0207;    // WM_MBUTTONDOWN: Middle mouse button pressed
    internal const int MessageMButtonUp = 0x0208;      // WM_MBUTTONUP  : Middle mouse button released
    internal const int MessageMousewheel = 0x020A;     // WM_MOUSEWHEEL : Mouse wheel scrolled
    internal const int MessageXButtonDown = 0x020B;    // WM_XBUTTONDOWN: X button pressed (Forward/Back)
    internal const int MessageXButtonUp = 0x020C;      // WM_XBUTTONUP  : X button released

    internal delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardHookStruct  // KBDLLHOOKSTRUCT
    {
        public uint VirtualKeyCode;     // vkCode
        public uint ScanCode;           // scanCode
        public uint Flags;              // flags
        public uint Time;               // time
        public IntPtr ExtraInfo;        // dwExtraInfo
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseHookStruct     // MSLLHOOKSTRUCT
    {
        public Point Pt;                // pt
        public uint MouseData;          // mouseData (wheel rotation amount or X button identifier)
        public uint Flags;              // flags
        public uint Time;               // time
        public IntPtr ExtraInfo;        // dwExtraInfo
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point // POINT
    {
        public int X;
        public int Y;
    }
}
