using System.Windows;
using System.Windows.Threading;

namespace VAK.App.Shell;

/// <summary>Polls the global cursor position (the collapsed pill is too small to rely on mouse events)
/// and asks the shell to expand or collapse. All rectangles are in physical screen pixels.</summary>
public sealed class HoverWatcher : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Func<bool> _isExpanded;
    private readonly Func<(Rect Trigger, Rect Panel)> _regions;
    private readonly Action _expand;
    private readonly Action _collapse;
    private DateTime? _outsideSince;
    private DateTime _expandedAt = DateTime.MinValue;
    private bool _enteredPanel;

    public TimeSpan CollapseDelay { get; set; } = TimeSpan.FromMilliseconds(250);
    public bool Enabled { get; set; } = true;

    /// <summary>How long a panel opened without the cursor (hotkey, tray) stays open before auto-collapsing.</summary>
    public TimeSpan OpenGrace { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>Call whenever the panel starts expanding, so the grace period restarts.</summary>
    public void NotifyExpanded() { _expandedAt = DateTime.UtcNow; _enteredPanel = false; _outsideSince = null; }

    public HoverWatcher(Func<bool> isExpanded, Func<(Rect Trigger, Rect Panel)> regions, Action expand, Action collapse)
    {
        _isExpanded = isExpanded; _regions = regions; _expand = expand; _collapse = collapse;
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    private void Poll()
    {
        if (!Enabled || !NativeMethods.GetCursorPos(out var p)) return;
        var cursor = new Point(p.X, p.Y);
        var (trigger, panel) = _regions();
        if (!_isExpanded())
        {
            _outsideSince = null;
            if (trigger.Contains(cursor)) _expand();
            return;
        }
        if (panel.Contains(cursor)) { _enteredPanel = true; _outsideSince = null; return; }
        if (!_enteredPanel && DateTime.UtcNow - _expandedAt < OpenGrace) return;
        _outsideSince ??= DateTime.UtcNow;
        if (DateTime.UtcNow - _outsideSince >= CollapseDelay) { _outsideSince = null; _collapse(); }
    }

    public void Dispose() => _timer.Stop();
}
