using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VAK.Core.ScreenTime;

namespace VAK.App.Services;

/// <summary>Once a second, credits the time to whichever app has the foreground window. Nothing is counted while the
/// user is idle for two minutes, while the screen is locked, or while VAK itself is in front.</summary>
public sealed class ScreenTimeTracker
{
    private static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(2);

    private readonly ScreenTimeLog _log;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<int, (string Key, string Name)> _byPid = new();
    private readonly Dictionary<string, string> _pathByKey = new();
    private readonly Dictionary<string, ImageSource?> _icons = new();
    private readonly int _ownPid = Environment.ProcessId;
    private DateTime _last = DateTime.UtcNow;
    private int _ticks;

    public event Action? Updated;

    public ScreenTimeTracker(ScreenTimeLog log)
    {
        _log = log;
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        var now = DateTime.UtcNow;
        var elapsed = Math.Clamp((now - _last).TotalSeconds, 0, 2.5); // a sleep gap must not be credited as usage
        _last = now;

        if (IdleTime() < IdleLimit && ForegroundApp() is { } app)
        {
            _log.Add(app.Key, app.Name, elapsed);
            Updated?.Invoke();
        }
        if (++_ticks % 30 == 0) _log.Save();
    }

    /// <summary>Writes the log to disk now (called on exit).</summary>
    public void Flush() => _log.Save();

    private (string Key, string Name)? ForegroundApp()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == _ownPid) return null;
        if (_byPid.TryGetValue((int)pid, out var known)) return known;

        try
        {
            using var p = Process.GetProcessById((int)pid);
            var key = p.ProcessName.ToLowerInvariant();
            string? name = null, path = null;
            try
            {
                path = p.MainModule?.FileName;
                if (path is not null) name = FileVersionInfo.GetVersionInfo(path).FileDescription;
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException or FileNotFoundException) { } // protected process
            if (key == "explorer") name = "File Explorer";
            var result = (key, string.IsNullOrWhiteSpace(name) ? p.ProcessName : name.Trim());
            if (path is not null) _pathByKey[key] = path;
            return _byPid[(int)pid] = result;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return null; } // process already gone
    }

    /// <summary>The app's own icon, or null when it cannot be read.</summary>
    public ImageSource? IconFor(string appKey)
    {
        if (_icons.TryGetValue(appKey, out var cached)) return cached;
        ImageSource? image = null;
        if (_pathByKey.TryGetValue(appKey, out var path) && File.Exists(path))
        {
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon is not null)
                {
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    image = bmp;
                }
            }
            catch (Exception e) when (e is Win32Exception or ArgumentException or IOException) { }
        }
        return _icons[appKey] = image;
    }

    private static TimeSpan IdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
}
