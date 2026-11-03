using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using QRCoder;
using VAK.App.Services;
using VAK.App.Theme;
using VAK.Core.Drop;
using VAK.Core.Shelf;

namespace VAK.App.Pages;

public sealed class DropPage : UserControl
{
    private readonly AppHost _host;
    private readonly Grid _root = new();
    private readonly DispatcherTimer _recheck = new() { Interval = TimeSpan.FromSeconds(3) };
    private NetworkState _state;
    private bool _busy;

    public DropPage(AppHost host)
    {
        _host = host;
        Content = _root;
        AllowDrop = true;
        DragOver += (_, e) => { e.Effects = _host.Drop.Running && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        Drop += (_, e) => { if (_host.Drop.Running && e.Data.GetData(DataFormats.FileDrop) is string[] paths) foreach (var p in paths) _host.Drop.AddShared(p); };

        _recheck.Tick += (_, _) => { var s = NetworkCategory.Check().State; if (s != _state) Render(); };
        Loaded += (_, _) => { _host.Drop.Changed += OnChanged; _recheck.Start(); Render(); };
        Unloaded += (_, _) => { _host.Drop.Changed -= OnChanged; _recheck.Stop(); };
    }

    private void OnChanged() => Dispatcher.Invoke(Render);

    private TextBlock T(string text, string style = "Style.Text", double size = 13, FontWeight? weight = null)
    {
        var t = new TextBlock { Text = text, Style = (Style)FindResource(style), FontSize = size };
        if (weight is { } w) t.FontWeight = w;
        return t;
    }

    private Button Pill(string label, Brush bg, Action click, double width = double.NaN)
    {
        var b = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = width, Height = 38, Background = bg, HorizontalAlignment = HorizontalAlignment.Left,
            Content = new TextBlock { Text = label, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(18, 0, 18, 0) },
        };
        b.Click += (_, _) => click();
        return b;
    }

    private void Render()
    {
        _root.Children.Clear();
        var (state, address) = NetworkCategory.Check();
        _state = state;

        if (state != NetworkState.Private) { _root.Children.Add(Message(state)); return; }
        _root.Children.Add(_host.Drop.Running ? Running() : Ready(address!));
    }

    private UIElement Message(NetworkState state)
    {
        var isPublic = state == NetworkState.Public;
        var tile = new Border
        {
            Width = 64, Height = 64, CornerRadius = new CornerRadius(32), Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x1E, 0x0E)),
            Child = new TextBlock { Text = Icons.Glyph("globe"), Style = (Style)FindResource("Style.Icon"), FontSize = 26, Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xA6, 0x23)) },
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 20, 0),
        };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(T(isPublic ? "Your Wi-Fi is set to Public" : "No private network found", "Style.Text", 17, FontWeights.SemiBold));
        text.Children.Add(T(isPublic ? "VAK Drop only works on private networks for your safety." : "Connect to a home or office network to use VAK Drop.", "Style.TextDim", 13));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        actions.Children.Add(Pill("Open Wi-Fi settings", (Brush)FindResource("Brush.Primary"), () =>
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:network-wifi") { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception) { }
        }));
        if (_host.Drop.Running)
        {
            var stopNow = Pill("Stop Drop", (Brush)FindResource("Brush.Control"), async () => { await _host.Drop.StopAsync(); Render(); });
            stopNow.Margin = new Thickness(10, 0, 0, 0);
            actions.Children.Add(stopNow);
        }
        var hint = T("● Checking again automatically", "Style.TextDim", 12);
        hint.Margin = new Thickness(14, 0, 0, 0); hint.VerticalAlignment = VerticalAlignment.Center;
        actions.Children.Add(hint);
        text.Children.Add(actions);

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(tile); row.Children.Add(text);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = row, Margin = new Thickness(5, 5, 5, 30) };
    }

    private UIElement Ready(System.Net.IPAddress address)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 460 };
        stack.Children.Add(T("Share files with phones on this network", "Style.Text", 17, FontWeights.SemiBold));
        stack.Children.Add(T($"Starting Drop opens a private web page at {address}. Phones scan a QR code to send files here or download files you share. Only people who scan that code can connect.", "Style.TextDim", 13));
        ((TextBlock)stack.Children[1]).TextWrapping = TextWrapping.Wrap;
        ((TextBlock)stack.Children[1]).Margin = new Thickness(0, 6, 0, 14);
        var start = Pill(_busy ? "Starting…" : "Start Drop", (Brush)FindResource("Brush.Primary"), async () =>
        {
            if (_busy) return;
            _busy = true; Render();
            await _host.Drop.StartAsync(address);
            _busy = false; Render();
        });
        stack.Children.Add(start);
        stack.Children.Add(T("Windows may ask to allow VAK through the firewall: choose Private networks.", "Style.TextDim", 11));
        ((TextBlock)stack.Children[^1]).Margin = new Thickness(0, 10, 0, 0);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = stack, Margin = new Thickness(5, 5, 5, 30), Padding = new Thickness(24) };
    }

    private UIElement Running()
    {
        var server = _host.Drop;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // left: QR card
        var qr = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        qr.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), Padding = new Thickness(8), Child = new Image { Source = QrImage(server.Url!), Width = 150, Height = 150, SnapsToDevicePixels = true } });
        var scan = T("Scan with your phone's camera", "Style.TextDim", 12);
        scan.Margin = new Thickness(0, 10, 0, 0); scan.HorizontalAlignment = HorizontalAlignment.Center;
        qr.Children.Add(scan);
        var stop = Pill("Stop Drop", (Brush)FindResource("Brush.Control"), async () => { await server.StopAsync(); Render(); });
        stop.Margin = new Thickness(0, 10, 0, 0); stop.HorizontalAlignment = HorizontalAlignment.Center;
        qr.Children.Add(stop);
        grid.Children.Add(new Border { Style = (Style)FindResource("Style.Card"), Child = qr, Margin = new Thickness(5) });

        // right: two lists
        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.Children.Add(FilesCard("Send to phones", "Drop files here or choose files", server.Shared.Select<SharedFile, (string Name, string Size, Action? Remove)>(s => (s.Name, ShelfStore.FormatSize(s.Size), () => server.RemoveShared(s.Id))), AddShared));
        var received = FilesCard("Received", "Nothing received yet", server.Received.Select<ReceivedFile, (string Name, string Size, Action? Remove)>(r => (r.Name, ShelfStore.FormatSize(r.Size), null)), OpenReceivedFolder, "Open folder");
        Grid.SetRow(received, 1);
        right.Children.Add(received);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private UIElement FilesCard(string title, string empty, IEnumerable<(string Name, string Size, Action? Remove)> items, Action action, string actionLabel = "Choose files")
    {
        var head = new Grid();
        head.Children.Add(T(title, "Style.TextDim", 12, FontWeights.SemiBold));
        var act = T(actionLabel, "Style.Text", 12, FontWeights.Medium);
        act.Foreground = (Brush)FindResource("Brush.Accent"); act.Cursor = Cursors.Hand; act.HorizontalAlignment = HorizontalAlignment.Right;
        act.MouseLeftButtonUp += (_, _) => action();
        head.Children.Add(act);

        var list = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        var any = false;
        foreach (var (name, size, remove) in items)
        {
            any = true;
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var n = T(name, "Style.Text", 13);
            n.TextTrimming = TextTrimming.CharacterEllipsis;
            row.Children.Add(n);
            var tail = new StackPanel { Orientation = Orientation.Horizontal };
            tail.Children.Add(T(size, "Style.TextDim", 11));
            if (remove is not null)
            {
                var x = T("×", "Style.TextDim", 15); x.Margin = new Thickness(10, 0, 0, 2); x.Cursor = Cursors.Hand;
                x.MouseLeftButtonUp += (_, _) => remove();
                tail.Children.Add(x);
            }
            Grid.SetColumn(tail, 1); row.Children.Add(tail);
            list.Children.Add(row);
        }
        if (!any) list.Children.Add(T(empty, "Style.TextDim", 12));

        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.Children.Add(head);
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        Grid.SetRow(scroll, 1);
        panel.Children.Add(scroll);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = panel, Margin = new Thickness(5) };
    }

    private void AddShared()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "Share files with phones" };
        if (dialog.ShowDialog() == true) foreach (var f in dialog.FileNames) _host.Drop.AddShared(f);
    }

    private void OpenReceivedFolder()
    {
        try
        {
            Directory.CreateDirectory(_host.Drop.ReceivedDir);
            Process.Start(new ProcessStartInfo(_host.Drop.ReceivedDir) { UseShellExecute = true });
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { }
    }

    private static BitmapImage QrImage(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(10);
        var image = new BitmapImage();
        using var stream = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
