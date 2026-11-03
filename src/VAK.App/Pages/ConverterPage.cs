using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using Microsoft.Win32;
using VAK.App.Services;
using VAK.App.Theme;
using VAK.Core.Converter;

namespace VAK.App.Pages;

public sealed class ConverterPage : UserControl
{
    private readonly AppHost _host;
    private ConverterService Service => _host.Converter;
    private readonly StackPanel _list = new();
    private readonly Rectangle _frame;
    private readonly TextBlock _ffmpeg;
    private readonly Button _go;
    private readonly TextBlock _goText;
    private readonly Button _quality, _size;

    public ConverterPage(AppHost host)
    {
        _host = host;
        AllowDrop = true;

        var root = new Grid { Margin = new Thickness(5, 0, 5, 0) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _frame = new Rectangle
        {
            RadiusX = 16, RadiusY = 16, StrokeThickness = 1, Stroke = (Brush)FindResource("Brush.Control"), StrokeDashArray = new DoubleCollection { 4, 4 },
            Fill = Brushes.Transparent, IsHitTestVisible = false,
        };
        var body = new Grid();
        body.Children.Add(_frame);
        body.Children.Add(new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Margin = new Thickness(10) });
        root.Children.Add(body);

        // bottom bar: options, ffmpeg status, convert
        _quality = Option("Quality", ChooseQuality);
        _size = Option("Size", ChooseSize);
        _ffmpeg = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        _goText = new TextBlock { Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.SemiBold };
        _go = new Button { Style = (Style)FindResource("Style.RoundButton"), Width = double.NaN, Height = 36, Background = (Brush)FindResource("Brush.Primary"), HorizontalAlignment = HorizontalAlignment.Right, Content = new Border { Padding = new Thickness(18, 0, 18, 0), Child = _goText } };
        _go.Click += async (_, _) => { if (Service.Running) Service.Cancel(); else await Service.RunAsync(); };
        var openFolder = Option("Open folder", OpenOutput);
        var add = Option("Choose files", ChooseFiles);

        var bar = new Grid { Margin = new Thickness(0, 8, 0, 6) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(add); left.Children.Add(_quality); left.Children.Add(_size); left.Children.Add(openFolder); left.Children.Add(_ffmpeg);
        bar.Children.Add(left);
        Grid.SetColumn(_go, 1); bar.Children.Add(_go);
        Grid.SetRow(bar, 1);
        root.Children.Add(bar);
        Content = root;

        DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; _frame.Stroke = (Brush)FindResource("Brush.Accent"); e.Handled = true; };
        DragLeave += (_, _) => _frame.Stroke = (Brush)FindResource("Brush.Control");
        Drop += (_, e) =>
        {
            _frame.Stroke = (Brush)FindResource("Brush.Control");
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) Service.Add(paths);
        };
        Loaded += (_, _) => { Service.Changed += OnChanged; Render(); };
        Unloaded += (_, _) => Service.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.Invoke(Render);

    private Button Option(string label, Action click)
    {
        var b = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = double.NaN, Height = 28, Margin = new Thickness(0, 0, 6, 0),
            Content = new TextBlock { Text = label, Style = (Style)FindResource("Style.Text"), FontSize = 12, FontWeight = FontWeights.Medium, Margin = new Thickness(10, 0, 10, 0) },
        };
        b.Click += (_, _) => click();
        return b;
    }

    private void SetOptionText(Button b, string text) => ((TextBlock)b.Content).Text = text;

    private void ChooseQuality()
    {
        var menu = new ContextMenu { PlacementTarget = _quality, Placement = PlacementMode.Top };
        foreach (var q in new[] { 100, 90, 75, 50 })
        {
            var item = new MenuItem { Header = q == 90 ? "90 (recommended)" : q.ToString() };
            var value = q;
            item.Click += (_, _) => { _host.Settings.Converter.Quality = value; _host.Save(); Render(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void ChooseSize()
    {
        var menu = new ContextMenu { PlacementTarget = _size, Placement = PlacementMode.Top };
        foreach (var (label, value) in new[] { ("Original size", 0), ("Max 3840 px", 3840), ("Max 1920 px", 1920), ("Max 1280 px", 1280), ("Max 800 px", 800) })
        {
            var item = new MenuItem { Header = label };
            var v = value;
            item.Click += (_, _) => { _host.Settings.Converter.MaxSide = v; _host.Save(); Render(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void ChooseFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "Choose files to convert" };
        if (dialog.ShowDialog() == true) Service.Add(dialog.FileNames);
    }

    private void OpenOutput()
    {
        var done = Service.Items.LastOrDefault(i => i.Output is not null);
        var path = done?.Output;
        if (path is null) return;
        try
        {
            var dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException) { }
    }

    private TextBlock T(string text, string style = "Style.Text", double size = 13, FontWeight? weight = null)
    {
        var t = new TextBlock { Text = text, Style = (Style)FindResource(style), FontSize = size };
        if (weight is { } w) t.FontWeight = w;
        return t;
    }

    private void Render()
    {
        var c = _host.Settings.Converter;
        SetOptionText(_quality, $"Quality {c.Quality}");
        SetOptionText(_size, c.MaxSide == 0 ? "Original size" : $"Max {c.MaxSide} px");
        _ffmpeg.Text = Service.FfmpegPath is null ? "ffmpeg not found (needed for audio/video)" : "ffmpeg ready";
        _ffmpeg.MouseLeftButtonUp -= RecheckFfmpeg;
        _ffmpeg.Cursor = Service.FfmpegPath is null ? Cursors.Hand : Cursors.Arrow;
        if (Service.FfmpegPath is null) { _ffmpeg.MouseLeftButtonUp += RecheckFfmpeg; _ffmpeg.ToolTip = "Install ffmpeg (for example: winget install Gyan.FFmpeg), then click here to check again."; }

        var waiting = Service.Items.Count(i => i.State is ItemState.Waiting or ItemState.Failed or ItemState.Cancelled);
        _goText.Text = Service.Running ? "Cancel" : waiting > 0 ? $"Convert {waiting} file{(waiting == 1 ? "" : "s")}" : "Convert";
        System.Windows.Automation.AutomationProperties.SetName(_go, _goText.Text);
        _go.IsEnabled = Service.Running || waiting > 0;
        _go.Opacity = _go.IsEnabled ? 1 : 0.5;

        _list.Children.Clear();
        _frame.Visibility = Service.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (Service.Items.Count == 0)
        {
            var empty = new StackPanel { Margin = new Thickness(0, 50, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            var line = T("Drop files or folders here to convert them", "Style.TextDim", 13);
            line.HorizontalAlignment = HorizontalAlignment.Center;
            var sub = T("Images, documents to PDF, audio, video and zip. Results go to a \"VAK Converted\" folder beside each file.", "Style.TextDim", 11);
            sub.HorizontalAlignment = HorizontalAlignment.Center; sub.Margin = new Thickness(0, 4, 0, 0);
            empty.Children.Add(line); empty.Children.Add(sub);
            _list.Children.Add(empty);
            return;
        }
        foreach (var item in Service.Items) _list.Children.Add(Row(item));
    }

    private void RecheckFfmpeg(object sender, MouseButtonEventArgs e) => Service.FindFfmpeg();

    private UIElement Row(ConvertItem item)
    {
        var icon = item.Kind switch { FileKind.Image => "image", FileKind.Folder => "folder", FileKind.Audio or FileKind.Video => "play", _ => "document" };
        var tile = new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("Brush.Control"),
            Child = new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = 15 },
        };
        var name = T(item.Name, "Style.Text", 13, FontWeights.Medium);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var status = StatusText(item);
        var text = new StackPanel { Margin = new Thickness(10, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(name); text.Children.Add(status);

        var choose = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = double.NaN, Height = 28, Background = (Brush)FindResource("Brush.Control"),
            IsEnabled = !Service.Running,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 8, 0),
                Children =
                {
                    new TextBlock { Text = "→ " + (item.Target?.Label ?? "—"), Style = (Style)FindResource("Style.Text"), FontSize = 12, FontWeight = FontWeights.SemiBold },
                    new TextBlock { Text = Icons.Glyph("chevron"), Style = (Style)FindResource("Style.Icon"), FontSize = 11, Margin = new Thickness(6, 0, 0, 0) },
                },
            },
        };
        System.Windows.Automation.AutomationProperties.SetName(choose, $"Target format for {item.Name}: {item.Target?.Label}");
        choose.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = choose, Placement = PlacementMode.Bottom };
            foreach (var t in item.Targets)
            {
                var note = t.Engine == Engine.Ffmpeg && Service.FfmpegPath is null ? "  (needs ffmpeg)" : t.Engine == Engine.Office && !Service.OfficeAvailable ? "  (needs Word or LibreOffice)" : "";
                var mi = new MenuItem { Header = t.Label + note };
                var target = t;
                mi.Click += (_, _) => Service.SetTarget(item, target);
                menu.Items.Add(mi);
            }
            menu.IsOpen = true;
        };

        var remove = T("×", "Style.TextDim", 16);
        remove.Cursor = Cursors.Hand; remove.Margin = new Thickness(10, 0, 4, 2); remove.VerticalAlignment = VerticalAlignment.Center; remove.ToolTip = "Remove from the list";
        remove.MouseLeftButtonUp += (_, _) => Service.Remove(item);

        var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(tile);
        Grid.SetColumn(text, 1); row.Children.Add(text);
        Grid.SetColumn(choose, 2); row.Children.Add(choose);
        Grid.SetColumn(remove, 3); row.Children.Add(remove);
        return new Border { Child = row, BorderBrush = (Brush)FindResource("Brush.Divider"), BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    private TextBlock StatusText(ConvertItem item)
    {
        var (text, brush) = item.State switch
        {
            ItemState.Running => ("Converting…", (Brush)FindResource("Brush.Accent")),
            ItemState.Done => (item.Message.Length > 0 && item.Message != "Done" ? "Done · " + item.Message : "Done · " + Path.GetFileName(item.Output ?? ""), (Brush)FindResource("Brush.Good")),
            ItemState.Failed => (item.Message, new SolidColorBrush(Color.FromRgb(0xEF, 0x6C, 0x4D))),
            ItemState.Cancelled => ("Cancelled", (Brush)FindResource("Brush.TextDim")),
            _ => (item.Kind.ToString(), (Brush)FindResource("Brush.TextDim")),
        };
        var t = T(text, "Style.TextDim", 11);
        t.Foreground = brush; t.TextTrimming = TextTrimming.CharacterEllipsis;
        if (item.State == ItemState.Failed) t.ToolTip = item.Message;
        return t;
    }
}
