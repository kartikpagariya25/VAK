using System.Runtime.InteropServices;

namespace VAK.App.Shell;

internal static class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x80;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
}
