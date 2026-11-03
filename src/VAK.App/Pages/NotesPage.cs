using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VAK.App.Theme;
using VAK.Core.Notes;

namespace VAK.App.Pages;

public sealed class NotesPage : UserControl
{
    private readonly AppHost _host;
    private readonly StackPanel _list = new();
    private readonly TextBox _search;
    private readonly Grid _searchRow;
    private readonly TextBlock _title;
    private readonly Grid _editorHost = new();
    private readonly DispatcherTimer _autosave = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private TextBox? _editor;
    private string? _selectedId;
    private string? _pending;

    public NotesPage(AppHost host)
    {
        _host = host;
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // left column
        var left = new Grid { Margin = new Thickness(5, 0, 12, 0) };
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var head = new Grid { Height = 34 };
        _title = new TextBlock { Text = "All notes", Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(IconButton("search", "Search", () => ToggleSearch()));
        buttons.Children.Add(IconButton("add", "New note", NewNote));
        head.Children.Add(_title); head.Children.Add(buttons);
        left.Children.Add(head);

        _search = new TextBox
        {
            Background = (Brush)FindResource("Brush.Card"), Foreground = Brushes.White, CaretBrush = Brushes.White, BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 6, 10, 6), FontSize = 13, FontFamily = (FontFamily)FindResource("Font.Ui"),
        };
        _search.TextChanged += (_, _) => RebuildList();
        _searchRow = new Grid { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 6) };
        _searchRow.Children.Add(new Border { CornerRadius = new CornerRadius(10), Child = _search, Background = (Brush)FindResource("Brush.Card") });
        Grid.SetRow(_searchRow, 1);
        left.Children.Add(_searchRow);

        var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        Grid.SetRow(scroll, 2);
        left.Children.Add(scroll);
        root.Children.Add(left);

        var divider = new Border { Width = 1, Background = (Brush)FindResource("Brush.Divider"), HorizontalAlignment = HorizontalAlignment.Left };
        Grid.SetColumn(divider, 1);
        root.Children.Add(divider);
        _editorHost.Margin = new Thickness(18, 0, 6, 0);
        Grid.SetColumn(_editorHost, 1);
        root.Children.Add(_editorHost);
        Content = root;

        _autosave.Tick += (_, _) => Flush();
        Unloaded += (_, _) => Flush();
        RebuildList();
        ShowEditor();
    }

    private Button IconButton(string icon, string tip, Action click)
    {
        var b = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 30, Height = 30, ToolTip = tip,
            Content = new TextBlock { Text = Icons.Glyph(icon), Style = (Style)FindResource("Style.Icon"), FontSize = 16 },
        };
        b.Click += (_, _) => click();
        return b;
    }

    private void ToggleSearch()
    {
        _searchRow.Visibility = _searchRow.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if (_searchRow.Visibility == Visibility.Visible) _search.Focus(); else _search.Text = "";
    }

    private void NewNote()
    {
        Flush();
        _search.Text = "";
        var note = _host.Notes.Create("");
        _selectedId = note.Id;
        RebuildList();
        ShowEditor();
        _editor?.Focus();
    }

    private void RebuildList()
    {
        _list.Children.Clear();
        var notes = _host.Notes.Search(_search.Text).ToList();
        _title.Text = _host.Notes.All.Count == 1 ? "1 note" : $"{_host.Notes.All.Count} notes";
        if (notes.Count == 0)
        {
            var empty = new StackPanel { Margin = new Thickness(0, 60, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            empty.Children.Add(new TextBlock { Text = _search.Text.Length > 0 ? "No matching notes" : "No notes yet", Style = (Style)FindResource("Style.TextDim"), HorizontalAlignment = HorizontalAlignment.Center });
            if (_search.Text.Length == 0)
            {
                var link = new TextBlock { Text = "New note", Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
                link.MouseLeftButtonUp += (_, _) => NewNote();
                empty.Children.Add(link);
            }
            _list.Children.Add(empty);
            return;
        }
        foreach (var n in notes)
        {
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = NoteText.Title(n.Body), Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
            text.Children.Add(new TextBlock { Text = Preview(n) + "  ·  " + Ago(n.Updated), Style = (Style)FindResource("Style.TextDim"), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
            var id = n.Id;
            var row = new Border
            {
                Child = text, Padding = new Thickness(10, 8, 10, 8), CornerRadius = new CornerRadius(10), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 2),
                Background = id == _selectedId ? (Brush)FindResource("Brush.Card") : Brushes.Transparent,
            };
            row.MouseLeftButtonUp += (_, _) => { Flush(); _selectedId = id; RebuildList(); ShowEditor(); };
            _list.Children.Add(row);
        }
    }

    private static string Preview(NoteItem n)
    {
        var lines = n.Body.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Skip(1).FirstOrDefault();
        return lines ?? "No more text";
    }

    private static string Ago(DateTime t)
    {
        var d = DateTime.Now - t;
        if (d.TotalMinutes < 1) return "Just now";
        if (d.TotalHours < 1) return $"{(int)d.TotalMinutes}m";
        if (d.TotalDays < 1) return $"{(int)d.TotalHours}h";
        return t.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void ShowEditor()
    {
        _editorHost.Children.Clear();
        _editor = null;
        var note = _selectedId is null ? null : _host.Notes.All.FirstOrDefault(n => n.Id == _selectedId);
        if (note is null)
        {
            _editorHost.Children.Add(new TextBlock { Text = "Create a note to start writing", Style = (Style)FindResource("Style.TextDim"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            return;
        }
        _editor = new TextBox
        {
            Text = note.Body, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, CaretBrush = Brushes.White,
            FontFamily = (FontFamily)FindResource("Font.Ui"), FontSize = 14, Padding = new Thickness(0, 6, 40, 0),
        };
        var id = note.Id;
        _editor.TextChanged += (_, _) => { _pending = _editor.Text; _autosave.Stop(); _autosave.Start(); };
        _editorHost.Children.Add(_editor);

        var delete = IconButton("trash", "Delete note", () =>
        {
            _autosave.Stop(); _pending = null;
            _host.Notes.Delete(id);
            _selectedId = null;
            RebuildList(); ShowEditor();
        });
        delete.HorizontalAlignment = HorizontalAlignment.Right; delete.VerticalAlignment = VerticalAlignment.Top;
        _editorHost.Children.Add(delete);
    }

    /// <summary>Writes any unsaved edit to disk (called on pause, on switching notes and when the page closes).</summary>
    private void Flush()
    {
        _autosave.Stop();
        if (_pending is null || _selectedId is null) return;
        _host.Notes.Update(_selectedId, _pending);
        _pending = null;
        RebuildList();
    }
}
