using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.App.Services;
using VAK.App.Theme;
using VAK.Core.Links;

namespace VAK.App.Pages;

public sealed class LinksPage : UserControl
{
    private readonly AppHost _host;
    private readonly TextBox _query;
    private readonly WrapPanel _chips = new();
    private readonly StackPanel _saved = new();
    private readonly TextBlock _count;
    private readonly StackPanel _addForm = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 4) };
    private TextBox _addUrl = null!, _addTitle = null!; // created in the constructor, after the Add button that refers to them

    public LinksPage(AppHost host)
    {
        _host = host;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // search card
        _query = Box("Search the web");
        _query.FontSize = 16;
        _query.KeyDown += (_, e) => { if (e.Key == Key.Enter) Search(); };
        var go = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 96, Height = 38, Background = (Brush)FindResource("Brush.Primary"),
            Content = new TextBlock { Text = "Search ↗", Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.SemiBold },
        };
        go.Click += (_, _) => Search();
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _query.Background = Brushes.Transparent;
        top.Children.Add(new Border { CornerRadius = new CornerRadius(19), Background = (Brush)FindResource("Brush.Control"), Margin = new Thickness(0, 0, 8, 0), Child = _query });
        Grid.SetColumn(go, 1); top.Children.Add(go);

        var searchCard = new StackPanel();
        searchCard.Children.Add(new CardHeader("search", "Search"));
        searchCard.Children.Add(new Border { Child = top, Margin = new Thickness(0, 6, 0, 8) });
        searchCard.Children.Add(_chips);
        root.Children.Add(new Border { Style = (Style)FindResource("Style.Card"), Child = searchCard, Margin = new Thickness(5, 5, 5, 5) });

        // saved links card
        var saved = new Grid();
        saved.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        saved.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        saved.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _count = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 12, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var head = new Grid();
        var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new CardHeader("link", "Saved links")); title.Children.Add(_count);
        var add = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 96, Height = 30, Background = (Brush)FindResource("Brush.Primary"), HorizontalAlignment = HorizontalAlignment.Right,
            Content = new TextBlock { Text = "+ Add Link", Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.SemiBold, FontSize = 12 },
        };
        add.Click += (_, _) => { _addForm.Visibility = _addForm.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; _addUrl.Focus(); };
        head.Children.Add(title); head.Children.Add(add);
        saved.Children.Add(head);

        _addUrl = Box("Address, for example example.com");
        _addTitle = Box("Title (optional)");
        var save = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 70, Height = 30, Background = (Brush)FindResource("Brush.Control"),
            Content = new TextBlock { Text = "Save", Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 12 },
        };
        save.Click += (_, _) => AddLink();
        _addUrl.KeyDown += (_, e) => { if (e.Key == Key.Enter) AddLink(); };
        var formRow = new Grid();
        formRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        formRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        formRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        formRow.Children.Add(_addUrl);
        Grid.SetColumn(_addTitle, 1); _addTitle.Margin = new Thickness(8, 0, 8, 0); formRow.Children.Add(_addTitle);
        Grid.SetColumn(save, 2); formRow.Children.Add(save);
        _addForm.Children.Add(formRow);
        Grid.SetRow(_addForm, 1);
        saved.Children.Add(_addForm);

        var scroll = new ScrollViewer { Content = _saved, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetRow(scroll, 2);
        saved.Children.Add(scroll);
        var savedCard = new Border { Style = (Style)FindResource("Style.Card"), Child = saved, Margin = new Thickness(5) };
        Grid.SetRow(savedCard, 1);
        root.Children.Add(savedCard);
        Content = root;

        BuildChips();
        RebuildSaved();
    }

    private TextBox Box(string hint)
    {
        var tb = new TextBox
        {
            Background = (Brush)FindResource("Brush.Control"), Foreground = Brushes.White, CaretBrush = Brushes.White, BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8), FontSize = 13, FontFamily = (FontFamily)FindResource("Font.Ui"), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = hint,
        };
        return tb;
    }

    private void BuildChips()
    {
        _chips.Children.Clear();
        foreach (var e in SearchEngines.All)
        {
            var selected = e.Id == _host.Settings.SearchEngine;
            var chip = new Border
            {
                CornerRadius = new CornerRadius(16), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand,
                Background = selected ? new SolidColorBrush(Color.FromRgb(0x12, 0x1C, 0x2E)) : Brushes.Transparent,
                BorderBrush = selected ? (Brush)FindResource("Brush.Accent") : Brushes.Transparent, BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = e.Name, Style = (Style)FindResource("Style.Text"), FontSize = 12, FontWeight = FontWeights.Medium },
            };
            var id = e.Id;
            chip.MouseLeftButtonUp += (_, _) => { _host.Settings.SearchEngine = id; _host.Save(); BuildChips(); };
            _chips.Children.Add(chip);
        }
    }

    private void Search()
    {
        var q = _query.Text.Trim();
        if (q.Length == 0) return;
        if (SearchEngines.BuildUrl(_host.Settings.SearchEngine, q) is { } url) Launcher.OpenUrl(url);
    }

    private void AddLink()
    {
        if (_host.Links.Add(_addUrl.Text, _addTitle.Text) is null)
        {
            _addUrl.BorderBrush = (Brush)FindResource("Brush.Accent"); _addUrl.BorderThickness = new Thickness(1); // invalid or duplicate
            return;
        }
        _addUrl.Text = ""; _addTitle.Text = ""; _addUrl.BorderThickness = new Thickness(0);
        _addForm.Visibility = Visibility.Collapsed;
        RebuildSaved();
    }

    private void RebuildSaved()
    {
        _saved.Children.Clear();
        var links = _host.Links.All;
        _count.Text = links.Count == 1 ? "1 link" : $"{links.Count} links";
        if (links.Count == 0)
        {
            _saved.Children.Add(new TextBlock { Text = "No saved links yet. Use + Add Link to keep one here.", Style = (Style)FindResource("Style.TextDim"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) });
            return;
        }
        foreach (var l in links)
        {
            var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            text.Children.Add(new TextBlock { Text = l.Title, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
            text.Children.Add(new TextBlock { Text = l.Url, Style = (Style)FindResource("Style.TextDim"), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
            var remove = new TextBlock { Text = "×", Style = (Style)FindResource("Style.TextDim"), FontSize = 16, Cursor = Cursors.Hand, ToolTip = "Remove", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 2) };
            var id = l.Id; var url = l.Url;
            remove.MouseLeftButtonUp += (_, e) => { e.Handled = true; _host.Links.Remove(id); RebuildSaved(); };
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4), Cursor = Cursors.Hand, Background = Brushes.Transparent };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = Icons.Glyph("link"), Style = (Style)FindResource("Style.Icon"), FontSize = 16, Foreground = (Brush)FindResource("Brush.TextDim") });
            Grid.SetColumn(text, 1); row.Children.Add(text);
            Grid.SetColumn(remove, 2); row.Children.Add(remove);
            row.MouseLeftButtonUp += (_, _) => Launcher.OpenUrl(url);
            _saved.Children.Add(new Border { Child = row, BorderBrush = (Brush)FindResource("Brush.Divider"), BorderThickness = new Thickness(0, 0, 0, 1) });
        }
    }
}
