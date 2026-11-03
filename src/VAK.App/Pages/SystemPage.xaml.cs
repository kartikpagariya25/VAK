using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using VAK.App.Controls;
using VAK.App.Theme;
using VAK.Core.Sys;

namespace VAK.App.Pages;

public partial class SystemPage : UserControl
{
    private readonly AppHost _host;
    private IDisposable? _subscription;
    private bool _updatingSlider;

    public SystemPage(AppHost host)
    {
        InitializeComponent();
        _host = host;

        BatteryTitle.Children.Add(new CardHeader("battery", "Battery"));
        NetworkTitle.Children.Add(new CardHeader("wifi", "Network"));
        PerfTitle.Children.Add(new CardHeader("system", "Performance"));
        VolumeTitle.Children.Add(new CardHeader("volume", "Volume"));
        DownIcon.Text = Icons.Glyph("down");
        UpIcon.Text = Icons.Glyph("up");
        DeviceChevron.Text = Icons.Glyph("chevron");

        DownSpark.Stroke = (System.Windows.Media.Brush)FindResource("Brush.Accent");
        UpSpark.Stroke = (System.Windows.Media.Brush)FindResource("Brush.Accent");
        foreach (var s in new[] { CpuSpark, RamSpark, DiskSpark }) s.Stroke = (System.Windows.Media.Brush)FindResource("Brush.Accent");

        VolumeSlider.ValueChanged += (_, e) =>
        {
            if (_updatingSlider) return;
            _host.Volume.SetVolume((int)Math.Round(e.NewValue));
            VolumePercent.Text = (int)Math.Round(e.NewValue) + "%";
        };
        MuteButton.Click += (_, _) =>
        {
            var muted = _host.Volume.Get()?.Muted ?? false;
            _host.Volume.SetMute(!muted);
            Render();
        };
        DeviceButton.Click += (_, _) => ShowDevices();

        Loaded += (_, _) =>
        {
            _subscription = _host.Monitor.Subscribe();
            _host.Monitor.Updated += Render;
            Render();
        };
        Unloaded += (_, _) =>
        {
            _host.Monitor.Updated -= Render;
            _subscription?.Dispose();
        };
    }

    private void Render()
    {
        var m = _host.Monitor;

        if (m.Battery is { } b)
        {
            BatteryPercent.Text = b.Percent + "%";
            BatteryBolt.Text = b.Charging ? Icons.Glyph("charge") : "";
            BatteryState.Text = b.Charging ? "Charging" : b.PluggedIn ? "Plugged in" : "On battery";
            BatteryHealth.Text = b.HealthPercent is { } h ? $"Health {h}%" : "";
            BarFill.Width = new GridLength(b.Percent, GridUnitType.Star);
            BarRest.Width = new GridLength(100 - b.Percent, GridUnitType.Star);
        }
        else
        {
            BatteryPercent.Text = "-";
            BatteryBolt.Text = "";
            BatteryState.Text = "No battery";
            BatteryHealth.Text = "";
            BarFill.Width = new GridLength(0, GridUnitType.Star);
            BarRest.Width = new GridLength(100, GridUnitType.Star);
        }

        if (m.Network is { } n)
        {
            NetworkName.Text = n.Name;
            NetworkSub.Text = (n.IsWireless ? "Wi-Fi" : "Ethernet") + (n.LinkMbps is { } mbps ? $" · {mbps} Mbps" : "");
            DownText.Text = RateFormatter.Format(n.DownBytesPerSec);
            UpText.Text = RateFormatter.Format(n.UpBytesPerSec);
        }
        else
        {
            NetworkName.Text = "-";
            NetworkSub.Text = "Not connected";
            DownText.Text = UpText.Text = "-";
        }
        DownSpark.Values = m.Down.Values; DownSpark.InvalidateVisual();
        UpSpark.Values = m.Up.Values; UpSpark.InvalidateVisual();

        CpuText.Text = m.Perf is { } p ? p.Cpu + "%" : "-";
        RamText.Text = m.Perf is { } p2 ? p2.Ram + "%" : "-";
        DiskText.Text = m.Perf is { } p3 ? p3.Disk + "%" : "-";
        CpuSpark.Values = m.Cpu.Values; CpuSpark.InvalidateVisual();
        RamSpark.Values = m.Ram.Values; RamSpark.InvalidateVisual();
        DiskSpark.Values = m.Disk.Values; DiskSpark.InvalidateVisual();

        if (m.Volume is { } v)
        {
            VolumePercent.Text = v.Percent + "%";
            MuteIcon.Text = Icons.Glyph(v.Muted ? "mute" : "volume");
            if (!VolumeSlider.IsMouseCaptureWithin)
            {
                _updatingSlider = true;
                VolumeSlider.Value = v.Percent;
                _updatingSlider = false;
            }
            DeviceName.Text = _host.Volume.Devices().FirstOrDefault(d => d.Id == v.DeviceId)?.Name ?? "Output";
        }
        else
        {
            VolumePercent.Text = "-";
            MuteIcon.Text = Icons.Glyph("mute");
            DeviceName.Text = "No output";
        }
    }

    private void ShowDevices()
    {
        var menu = new ContextMenu { PlacementTarget = DeviceButton, Placement = PlacementMode.Bottom };
        var devices = _host.Volume.Devices();
        if (devices.Count == 0) menu.Items.Add(new MenuItem { Header = "No audio devices", IsEnabled = false });
        foreach (var d in devices)
        {
            var item = new MenuItem { Header = d.Name };
            var id = d.Id;
            item.Click += (_, _) => { _host.Volume.SelectDevice(id); _updatingSlider = false; Render(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
}
