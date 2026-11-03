using System.Windows.Threading;
using VAK.Core.Sys;

namespace VAK.App.Services;

/// <summary>Polls the system services once a second while at least one view is subscribed, and keeps sparkline history.</summary>
public sealed class SystemMonitor
{
    private readonly AppHost _host;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _subscribers;
    private bool _polling;

    public BatteryInfo? Battery { get; private set; }
    public NetworkInfo? Network { get; private set; }
    public PerfInfo? Perf { get; private set; }
    public VolumeState? Volume { get; private set; }

    public SparklineBuffer Down { get; } = new(40);
    public SparklineBuffer Up { get; } = new(40);
    public SparklineBuffer Cpu { get; } = new(40);
    public SparklineBuffer Ram { get; } = new(40);
    public SparklineBuffer Disk { get; } = new(40);

    public event Action? Updated;

    public SystemMonitor(AppHost host)
    {
        _host = host;
        _timer.Tick += async (_, _) => await PollAsync();
    }

    /// <summary>Starts polling (with an immediate first read) until the returned handle is disposed.</summary>
    public IDisposable Subscribe()
    {
        if (_subscribers++ == 0)
        {
            _timer.Start();
            _ = PollAsync();
        }
        return new Handle(this);
    }

    private async Task PollAsync()
    {
        if (_polling) return;
        _polling = true;
        try
        {
            // Battery, network and counters are safe off the UI thread; the volume COM objects stay on it.
            var (battery, network, perf) = await Task.Run(() => (_host.Battery.Read(), _host.Network.Read(), _host.Performance.Read()));
            Battery = battery; Network = network; Perf = perf;
            Volume = _host.Volume.Get();
            Down.Add(network?.DownBytesPerSec ?? 0);
            Up.Add(network?.UpBytesPerSec ?? 0);
            Cpu.Add(perf?.Cpu ?? 0);
            Ram.Add(perf?.Ram ?? 0);
            Disk.Add(perf?.Disk ?? 0);
            Updated?.Invoke();
        }
        finally { _polling = false; }
    }

    private void Release()
    {
        if (--_subscribers == 0) _timer.Stop();
    }

    private sealed class Handle : IDisposable
    {
        private SystemMonitor? _owner;
        public Handle(SystemMonitor owner) => _owner = owner;
        public void Dispose() { _owner?.Release(); _owner = null; }
    }
}
