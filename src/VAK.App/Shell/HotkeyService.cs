using System.Windows;
using System.Windows.Interop;
using VAK.Core.Hotkeys;

namespace VAK.App.Shell;

/// <summary>Registers one global hotkey on a window. Registration failure (key already taken) returns false and never throws.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int Id = 0x5641; // "VA"
    private readonly HwndSource _source;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyService(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle);
        _source.AddHook(WndProc);
    }

    public bool Register(string hotkey)
    {
        Unregister();
        if (HotkeyParser.TryParse(hotkey) is not var (mods, vk)) return false;
        _registered = NativeMethods.RegisterHotKey(_source.Handle, Id, mods, vk);
        return _registered;
    }

    private void Unregister()
    {
        if (_registered) NativeMethods.UnregisterHotKey(_source.Handle, Id);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == Id)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
    }
}
