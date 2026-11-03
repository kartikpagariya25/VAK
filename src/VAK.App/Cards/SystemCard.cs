using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using VAK.App.Controls;

namespace VAK.App.Cards;

/// <summary>Home card: three ring gauges for CPU, RAM and disk.</summary>
public sealed class SystemCard : UserControl
{
    private readonly AppHost _host;
    private readonly (RingGauge Ring, TextBlock Text)[] _gauges;
    private IDisposable? _subscription;

    public SystemCard(AppHost host)
    {
        _host = host;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(new CardHeader("system", "System"));

        var row = new UniformGrid { Columns = 3, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
        Grid.SetRow(row, 1);
        _gauges = new[] { Gauge("CPU", row), Gauge("RAM", row), Gauge("DISK", row) };
        root.Children.Add(row);
        Content = root;

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

    private (RingGauge, TextBlock) Gauge(string label, Panel parent)
    {
        var ring = new RingGauge { Width = 50, Height = 50, Thickness = 5 };
        var text = new TextBlock
        {
            Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.SemiBold, FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        var holder = new Grid { Width = 50, Height = 50 };
        holder.Children.Add(ring);
        holder.Children.Add(text);

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(holder);
        stack.Children.Add(new TextBlock
        {
            Text = label, Style = (Style)FindResource("Style.TextDim"), FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0),
        });
        parent.Children.Add(stack);
        return (ring, text);
    }

    private void Render()
    {
        var p = _host.Monitor.Perf;
        var values = p is null ? new int?[3] : new int?[] { p.Cpu, p.Ram, p.Disk };
        for (var i = 0; i < 3; i++)
        {
            _gauges[i].Ring.Value = values[i] ?? 0;
            _gauges[i].Text.Text = values[i] is { } v ? v + "%" : "-";
        }
    }
}
