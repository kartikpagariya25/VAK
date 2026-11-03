using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Theme;

namespace VAK.App.Shell;

public partial class AddToDockView : UserControl
{
    private AppHost? _host;
    private Action? _changed;

    public AddToDockView()
    {
        InitializeComponent();
        Search.TextChanged += (_, _) =>
        {
            Placeholder.Visibility = Search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        };
    }

    public void Bind(AppHost host, Action changed)
    {
        _host = host;
        _changed = changed;
        Refresh();
    }

    public void ResetSearch() => Search.Text = "";

    public void Refresh()
    {
        if (_host is null) return;
        var dock = _host.Settings.Dock;
        var full = dock.Count + 1 >= DockView.Capacity; // +1 for the fixed Home button
        FullNote.Visibility = full ? Visibility.Visible : Visibility.Collapsed;

        Rows.Children.Clear();
        foreach (var info in _host.Registry.Search(Search.Text))
        {
            var isHome = info.Id == Navigator.Home;
            var inDock = isHome || dock.Contains(info.Id);
            var blurb = _host.Modules[info.Id].Blurb;

            var tile = new Border
            {
                Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = (Brush)FindResource("Brush.Control"),
                Child = new TextBlock { Text = Icons.Glyph(info.IconKey), Style = (Style)FindResource("Style.Icon") },
            };
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            text.Children.Add(new TextBlock { Text = info.Title, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 14 });
            text.Children.Add(new TextBlock { Text = blurb, Style = (Style)FindResource("Style.TextDim"), FontSize = 12 });
            var tag = new TextBlock
            {
                Text = inDock ? "In dock" : "Add", Style = (Style)FindResource(inDock ? "Style.TextDim" : "Style.Text"),
                Foreground = inDock ? (Brush)FindResource("Brush.TextDim") : (Brush)FindResource("Brush.Accent"),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
            };

            var grid = new Grid { Margin = new Thickness(8, 5, 12, 5) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(tile);
            Grid.SetColumn(text, 1); grid.Children.Add(text);
            Grid.SetColumn(tag, 2); grid.Children.Add(tag);

            var row = new Border { Child = grid, CornerRadius = new CornerRadius(12), Background = Brushes.Transparent, Cursor = isHome ? Cursors.Arrow : Cursors.Hand };
            row.MouseEnter += (_, _) => { if (!isHome) row.Background = (Brush)FindResource("Brush.Card"); };
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            if (!isHome)
            {
                var id = info.Id;
                row.MouseLeftButtonUp += (_, _) => Toggle(id);
            }
            Rows.Children.Add(row);
        }
    }

    private void Toggle(string id)
    {
        var dock = _host!.Settings.Dock;
        if (dock.Contains(id)) dock.Remove(id);
        else if (dock.Count + 1 < DockView.Capacity) dock.Add(id);
        else return;
        _changed?.Invoke();
        Refresh();
    }
}
