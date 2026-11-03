using System.IO;
using System.Windows.Threading;
using VAK.Core.AiUsage;

namespace VAK.App.Services;

/// <summary>Reads the local Claude Code / Codex logs in the background and refreshes every minute while a view is open.</summary>
public sealed class AiUsageService
{
    private readonly AiUsageCollector _collector = AiUsageCollector.ForCurrentUser(() => DateTime.Now);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private int _subscribers;
    private bool _busy;

    public IReadOnlyList<UsageEvent> Events { get; private set; } = Array.Empty<UsageEvent>();
    public bool Loaded { get; private set; }

    public event Action? Changed;

    public AiUsageService() => _timer.Tick += async (_, _) => await RefreshAsync();

    public IDisposable Subscribe()
    {
        if (_subscribers++ == 0) { _timer.Start(); _ = RefreshAsync(); }
        return new Handle(this);
    }

    public async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            Events = await Task.Run(() => _collector.Collect());
            Loaded = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Loaded = true; }
        finally { _busy = false; }
        Changed?.Invoke();
    }

    private void Release() { if (--_subscribers == 0) _timer.Stop(); }

    private sealed class Handle : IDisposable
    {
        private AiUsageService? _owner;
        public Handle(AiUsageService owner) => _owner = owner;
        public void Dispose() { _owner?.Release(); _owner = null; }
    }
}
