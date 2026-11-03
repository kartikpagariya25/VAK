using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using VAK.App.Theme;
using VAK.Core.Shelf;

namespace VAK.App.Pages;

public sealed class FileShelfPage : UserControl
{
    private readonly AppHost _host;
    private readonly StackPanel _shelves = new();
    private readonly Grid _right = new() { AllowDrop = true, Background = Brushes.Transparent };
    private readonly StackPanel _files = new();
    private readonly TextBlock _title, _count;
    private readonly Rectangle _dropFrame;
    private string _shelf = ShelfStore.DefaultShelf;
    private ShelfSort _sort = ShelfSort.Recent;
    private bool _selecting;
    private readonly HashSet<string> _selected = new();
    private Point _dragStart;

    public FileShelfPage(AppHost host)
    {
        _host = host;
        Focusable = true;
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new Grid { Margin = new Thickness(5, 0, 10, 0) };
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.Children.Add(new TextBlock { Text = "SHELVES", Style = (Style)FindResource("Style.TextDim"), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 4, 0, 8) });
        var shelfScroll = new ScrollViewer { Content = _shelves, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        Grid.SetRow(shelfScroll, 1);
        left.Children.Add(shelfScroll);
        var add = new Border { Padding = new Thickness(8, 10, 8, 4), BorderBrush = (Brush)FindResource("Brush.Divider"), BorderThickness = new Thickness(0, 1, 0, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent };
        var addLine = new StackPanel { Orientation = Orientation.Horizontal };
        addLine.Children.Add(new TextBlock { Text = Icons.Glyph("add"), Style = (Style)FindResource("Style.Icon"), FontSize = 15, Margin = new Thickness(0, 0, 8, 0) });
        addLine.Children.Add(new TextBlock { Text = "New shelf", Style = (Style)FindResource("Style.Text"), FontSize = 13 });
        add.Child = addLine;
        add.MouseLeftButtonUp += (_, _) => NewShelf(add);
        Grid.SetRow(add, 2);
        left.Children.Add(add);
        root.Children.Add(left);

        _right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _right.Margin = new Thickness(0, 0, 5, 0);
        _title = new TextBlock { Style = (Style)FindResource("Style.Text"), FontSize = 18, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        _count = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 13, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var head = new Grid { Height = 40, Margin = new Thickness(0, 0, 0, 6) };
        var titleLine = new StackPanel { Orientation = Orientation.Horizontal };
        titleLine.Children.Add(_title); titleLine.Children.Add(_count);
        head.Children.Add(titleLine);
        head.Children.Add(Toolbar());
        _right.Children.Add(head);

        _dropFrame = new Rectangle
        {
            RadiusX = 16, RadiusY = 16, StrokeThickness = 1, Stroke = (Brush)FindResource("Brush.Control"), StrokeDashArray = new DoubleCollection { 4, 4 },
            Fill = Brushes.Transparent, IsHitTestVisible = false,
        };
        var body = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        body.Children.Add(_dropFrame);
        body.Children.Add(new ScrollViewer { Content = _files, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Margin = new Thickness(8) });
        Grid.SetRow(body, 1);
        _right.Children.Add(body);
        Grid.SetColumn(_right, 1);
        root.Children.Add(_right);
        Content = root;

        _right.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; _dropFrame.Stroke = (Brush)FindResource("Brush.Accent"); e.Handled = true; };
        _right.DragLeave += (_, _) => _dropFrame.Stroke = (Brush)FindResource("Brush.Control");
        _right.Drop += (_, e) =>
        {
            _dropFrame.Stroke = (Brush)FindResource("Brush.Control");
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) AddPaths(paths);
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && Clipboard.ContainsFileDropList())
            {
                AddPaths(Clipboard.GetFileDropList().Cast<string>());
                e.Handled = true;
            }
        };
        MouseDown += (_, _) => Focus();
        Refresh();
    }

    private UIElement Toolbar()
    {
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Button Text(string label, Action click)
        {
            var b = new Button
            {
                Style = (Style)FindResource("Style.RoundButton"), Width = double.NaN, Height = 28, Margin = new Thickness(2, 0, 0, 0),
                Content = new TextBlock { Text = label, Style = (Style)FindResource("Style.Text"), FontSize = 12, FontWeight = FontWeights.Medium, Margin = new Thickness(10, 0, 10, 0) },
            };
            b.Click += (_, _) => click();
            return b;
        }
        Button? sort = null;
        sort = Text("Recent ˅", () =>
        {
            var menu = new ContextMenu { PlacementTarget = sort, Placement = PlacementMode.Bottom };
            foreach (var s in Enum.GetValues<ShelfSort>())
            {
                var item = new MenuItem { Header = s.ToString() };
                var chosen = s;
                item.Click += (_, _) => { _sort = chosen; ((TextBlock)sort!.Content).Text = chosen + " ˅"; Refresh(); };
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        });
        bar.Children.Add(sort);
        var select = Text("Select", () => { _selecting = !_selecting; _selected.Clear(); Refresh(); });
        bar.Children.Add(select);
        bar.Children.Add(Text("Copy all", CopyAll));
        bar.Children.Add(Text("Clear", ClearShelf));
        return bar;
    }

    // ---- actions -----------------------------------------------------------------------------

    private void AddPaths(IEnumerable<string> paths)
    {
        foreach (var p in paths) _host.Shelf.AddFile(_shelf, p);
        Refresh();
    }

    private void ChooseFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "Add files to " + _shelf };
        if (dialog.ShowDialog() == true) AddPaths(dialog.FileNames);
    }

    private IEnumerable<ShelfItem> Targets()
    {
        var all = _host.Shelf.Items(_shelf, _sort);
        return _selected.Count > 0 ? all.Where(i => _selected.Contains(i.Name)) : all;
    }

    private void CopyAll()
    {
        var paths = new StringCollection();
        foreach (var i in Targets()) paths.Add(i.Path);
        if (paths.Count == 0) return;
        try { Clipboard.SetFileDropList(paths); } catch (System.Runtime.InteropServices.ExternalException) { }
    }

    private void ClearShelf()
    {
        foreach (var i in Targets().ToList()) _host.Shelf.Remove(_shelf, i.Name);
        _selected.Clear();
        Refresh();
    }

    private void NewShelf(FrameworkElement anchor)
    {
        var box = new TextBox { Width = 160, Background = (Brush)FindResource("Brush.Control"), Foreground = Brushes.White, CaretBrush = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6), FontSize = 13 };
        var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Top, StaysOpen = false, AllowsTransparency = true, VerticalOffset = -4 };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Shelf name", Style = (Style)FindResource("Style.TextDim"), FontSize = 11 });
        panel.Children.Add(box);
        popup.Child = new Border { Padding = new Thickness(10), CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)), BorderBrush = (Brush)FindResource("Brush.Control"), BorderThickness = new Thickness(1), Child = panel };
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            if (_host.Shelf.CreateShelf(box.Text) is { } name) { _shelf = name; popup.IsOpen = false; Refresh(); }
        };
        popup.IsOpen = true;
        box.Focus();
    }

    // ---- rendering ---------------------------------------------------------------------------

    private void Refresh()
    {
        RebuildShelves();
        var items = _host.Shelf.Items(_shelf, _sort);
        _title.Text = _shelf;
        _count.Text = items.Count == 0 ? "Empty" : $"{items.Count} file{(items.Count == 1 ? "" : "s")} · {ShelfStore.FormatSize(items.Sum(i => i.Size))}";
        _files.Children.Clear();
        _dropFrame.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (items.Count == 0)
        {
            var empty = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 60, 0, 0) };
            empty.Children.Add(new TextBlock { Text = $"Nothing on {_shelf} yet · drop files or paste with Ctrl+V", Style = (Style)FindResource("Style.TextDim"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            var choose = new TextBlock { Text = "Choose files", Style = (Style)FindResource("Style.Text"), FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(16, 0, 0, 0), Cursor = Cursors.Hand };
            choose.MouseLeftButtonUp += (_, _) => ChooseFiles();
            empty.Children.Add(choose);
            _files.Children.Add(empty);
            return;
        }
        foreach (var i in items) _files.Children.Add(Row(i));
    }

    private void RebuildShelves()
    {
        _shelves.Children.Clear();
        foreach (var name in _host.Shelf.Shelves())
        {
            var count = _host.Shelf.Items(name, ShelfSort.Name).Count;
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.Children.Add(new TextBlock { Text = Icons.Glyph(name == _shelf ? "folder-open" : "folder"), Style = (Style)FindResource("Style.Icon"), FontSize = 17, Margin = new Thickness(0, 0, 10, 0) });
            var label = new TextBlock { Text = name, Style = (Style)FindResource("Style.Text"), FontSize = 14, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 1); line.Children.Add(label);
            var n = new TextBlock { Text = count.ToString(), Style = (Style)FindResource("Style.TextDim"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(n, 2); line.Children.Add(n);

            var row = new Border
            {
                Child = line, Padding = new Thickness(10, 9, 12, 9), CornerRadius = new CornerRadius(12), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 2),
                Background = name == _shelf ? (Brush)FindResource("Brush.Card") : Brushes.Transparent,
            };
            var shelf = name;
            row.MouseLeftButtonUp += (_, _) => { _shelf = shelf; _selected.Clear(); Refresh(); };
            if (name != ShelfStore.DefaultShelf)
            {
                var menu = new ContextMenu();
                var del = new MenuItem { Header = "Delete shelf (removes its copies)" };
                del.Click += (_, _) => { if (_host.Shelf.DeleteShelf(shelf)) { if (_shelf == shelf) _shelf = ShelfStore.DefaultShelf; Refresh(); } };
                menu.Items.Add(del);
                row.ContextMenu = menu;
            }
            _shelves.Children.Add(row);
        }
    }

    private UIElement Row(ShelfItem item)
    {
        var selected = _selected.Contains(item.Name);
        var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = item.Name, Style = (Style)FindResource("Style.Text"), FontSize = 13, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = ShelfStore.FormatSize(item.Size), Style = (Style)FindResource("Style.TextDim"), FontSize = 11 });
        var remove = new TextBlock { Text = "×", Style = (Style)FindResource("Style.TextDim"), FontSize = 16, Cursor = Cursors.Hand, ToolTip = "Remove from shelf", Margin = new Thickness(8, 0, 4, 2), VerticalAlignment = VerticalAlignment.Center };
        var name = item.Name;
        remove.MouseLeftButtonUp += (_, e) => { e.Handled = true; _host.Shelf.Remove(_shelf, name); Refresh(); };
        var tile = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(8), Background = (Brush)FindResource("Brush.Control"),
            Child = new TextBlock { Text = Icons.Glyph(_selecting ? (selected ? "check" : "add-circle") : "document"), Style = (Style)FindResource("Style.Icon"), FontSize = 16, Foreground = selected ? (Brush)FindResource("Brush.Accent") : Brushes.White },
        };
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(tile);
        Grid.SetColumn(text, 1); line.Children.Add(text);
        Grid.SetColumn(remove, 2); line.Children.Add(remove);

        var row = new Border { Child = line, Padding = new Thickness(6), CornerRadius = new CornerRadius(10), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 2), Background = selected ? (Brush)FindResource("Brush.Card") : Brushes.Transparent };
        row.MouseEnter += (_, _) => { if (!selected) row.Background = (Brush)FindResource("Brush.Card"); };
        row.MouseLeave += (_, _) => { if (!selected) row.Background = Brushes.Transparent; };
        row.PreviewMouseLeftButtonDown += (_, e) => _dragStart = e.GetPosition(null);
        row.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _selecting) return;
            var d = e.GetPosition(null) - _dragStart;
            if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            var data = new DataObject(DataFormats.FileDrop, new[] { item.Path });
            DragDrop.DoDragDrop(row, data, DragDropEffects.Copy); // dragging out copies the file; the shelf keeps its own copy
        };
        row.MouseLeftButtonUp += (_, _) =>
        {
            if (_selecting) { if (!_selected.Add(name)) _selected.Remove(name); Refresh(); return; }
            try { Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        };
        return row;
    }
}
