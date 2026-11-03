using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Theme;
using VAK.Core.Units;

namespace VAK.App.Pages;

public sealed class UnitsPage : UserControl
{
    private readonly AppHost _host;
    private readonly StackPanel _categoryList = new();
    private readonly TextBox _input;
    private readonly TextBlock _result;
    private readonly Button _fromButton, _toButton;
    private readonly StackPanel _others = new();
    private readonly TextBlock _recent;
    private readonly Grid _body = new();
    private readonly TextBlock _notice;

    private UnitCategory _category = UnitCatalog.All[0];
    private UnitDef _from, _to;
    private bool _suspend;

    public UnitsPage(AppHost host)
    {
        _host = host;
        _from = _category.Units.Single(u => u.Id == "m");
        _to = _category.Units.Single(u => u.Id == "ft");

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _categoryList.Margin = new Thickness(5, 0, 5, 0);
        root.Children.Add(new ScrollViewer { Content = _categoryList, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden });

        _input = new TextBox
        {
            Text = "1", Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White,
            CaretBrush = Brushes.White, FontFamily = (FontFamily)FindResource("Font.Display"), FontSize = 36, FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Center,
        };
        _input.TextChanged += (_, _) => { if (!_suspend) Recompute(); };
        _result = new TextBlock { Style = (Style)FindResource("Style.Big"), FontSize = 36, TextTrimming = TextTrimming.CharacterEllipsis };
        _fromButton = UnitButton(() => _from, u => { _from = u; Recompute(); });
        _toButton = UnitButton(() => _to, u => { _to = u; Recompute(); });
        _recent = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 12 };
        _notice = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 13, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };

        _body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Put(0, FromRow());
        Put(1, SwapRow());
        Put(2, ToRow());
        Put(3, new ScrollViewer { Content = _others, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Margin = new Thickness(0, 4, 0, 0) });
        Put(4, _recent);

        var right = new Grid { Margin = new Thickness(16, 0, 10, 0) };
        right.Children.Add(_body);
        right.Children.Add(_notice);
        Grid.SetColumn(right, 1);
        root.Children.Add(right);
        Content = root;

        BuildCategories();
        Recompute();
        Loaded += async (_, _) => await LoadCurrencyAsync();
    }

    private void Put(int row, UIElement e) { Grid.SetRow(e, row); _body.Children.Add(e); }

    private UIElement FromRow()
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        g.Children.Add(new TextBlock { Text = "From", Style = (Style)FindResource("Style.TextDim"), FontSize = 12, VerticalAlignment = VerticalAlignment.Top });
        var row = new Grid { Margin = new Thickness(0, 18, 0, 0), Height = 48 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(_input);
        Grid.SetColumn(_fromButton, 1); row.Children.Add(_fromButton);
        g.Children.Add(row);
        g.Height = 70;
        return g;
    }

    private UIElement SwapRow()
    {
        var line = new Border { Height = 1, Background = (Brush)FindResource("Brush.Divider"), VerticalAlignment = VerticalAlignment.Center };
        var swap = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 32, Height = 32, Background = (Brush)FindResource("Brush.Control"),
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(60, 0, 0, 0), ToolTip = "Swap",
            Content = new TextBlock { Text = Icons.Glyph("swap"), Style = (Style)FindResource("Style.Icon"), FontSize = 15 },
        };
        swap.Click += (_, _) => { (_from, _to) = (_to, _from); _suspend = true; _input.Text = _result.Text; _suspend = false; Recompute(); };
        var g = new Grid { Height = 34 };
        g.Children.Add(line);
        g.Children.Add(swap);
        return g;
    }

    private UIElement ToRow()
    {
        var g = new Grid { Height = 70 };
        g.Children.Add(new TextBlock { Text = "To", Style = (Style)FindResource("Style.TextDim"), FontSize = 12 });
        var row = new Grid { Margin = new Thickness(0, 18, 0, 0), Height = 48 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(_result);
        var copy = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 26, Height = 26, Margin = new Thickness(8, 4, 0, 0), ToolTip = "Copy result",
            Content = new TextBlock { Text = Icons.Glyph("copy"), Style = (Style)FindResource("Style.Icon"), FontSize = 14, Foreground = (Brush)FindResource("Brush.TextDim") },
        };
        copy.Click += (_, _) => { try { Clipboard.SetText(_result.Text); } catch (System.Runtime.InteropServices.ExternalException) { } };
        Grid.SetColumn(copy, 1); copy.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(copy);
        Grid.SetColumn(_toButton, 2); row.Children.Add(_toButton);
        g.Children.Add(row);
        return g;
    }

    private Button UnitButton(Func<UnitDef> current, Action<UnitDef> pick)
    {
        var name = new TextBlock { Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 14 };
        var sym = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 14, Margin = new Thickness(8, 0, 6, 0) };
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(name); panel.Children.Add(sym);
        panel.Children.Add(new TextBlock { Text = Icons.Glyph("chevron"), Style = (Style)FindResource("Style.Icon"), FontSize = 12, Foreground = (Brush)FindResource("Brush.TextDim") });
        var button = new Button { Style = (Style)FindResource("Style.RoundButton"), Width = double.NaN, Height = 30, Padding = new Thickness(10, 0, 10, 0), Content = panel, VerticalAlignment = VerticalAlignment.Center };
        button.Tag = (name, sym);
        button.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
            foreach (var u in _category.Units)
            {
                var item = new MenuItem { Header = $"{u.Name}  ({u.Symbol})" };
                var unit = u;
                item.Click += (_, _) => pick(unit);
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        };
        return button;
    }

    private void BuildCategories()
    {
        _categoryList.Children.Clear();
        var cats = UnitCatalog.All.ToList();
        if (_host.Currency.Category is { } cur) cats.Add(cur);
        else cats.Add(new UnitCategory("currency", "Currency", "globe", Array.Empty<UnitDef>()));
        foreach (var c in cats)
        {
            var row = new Border
            {
                Padding = new Thickness(12, 9, 12, 9), CornerRadius = new CornerRadius(12), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 2),
                Background = c.Id == _category.Id ? (Brush)FindResource("Brush.Card") : Brushes.Transparent,
            };
            var line = new StackPanel { Orientation = Orientation.Horizontal };
            line.Children.Add(new TextBlock { Text = Icons.Glyph(c.IconKey), Style = (Style)FindResource("Style.Icon"), FontSize = 16, Width = 24 });
            line.Children.Add(new TextBlock { Text = c.Name, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 14, Margin = new Thickness(8, 0, 0, 0) });
            row.Child = line;
            var cat = c;
            row.MouseLeftButtonUp += (_, _) => SelectCategory(cat);
            _categoryList.Children.Add(row);
        }
    }

    private void SelectCategory(UnitCategory c)
    {
        if (c.Units.Count == 0)
        {
            _category = c;
            _notice.Text = "Currency rates need an internet connection. Connect and reopen this page.";
            _notice.Visibility = Visibility.Visible; _body.Visibility = Visibility.Collapsed;
            BuildCategories();
            return;
        }
        _notice.Visibility = Visibility.Collapsed; _body.Visibility = Visibility.Visible;
        _category = c;
        _from = c.Units[0];
        _to = c.Units.Count > 1 ? c.Units[1] : c.Units[0];
        if (c.Id == "currency") { _from = c.Units.FirstOrDefault(u => u.Id == "USD") ?? c.Units[0]; _to = c.Units.FirstOrDefault(u => u.Id == "INR") ?? c.Units[^1]; }
        else if (c.Id == "length") { _from = c.Units.Single(u => u.Id == "m"); _to = c.Units.Single(u => u.Id == "ft"); }
        BuildCategories();
        Recompute();
    }

    private async Task LoadCurrencyAsync()
    {
        if (_host.Currency.Category is not null) return;
        await _host.Currency.LoadAsync();
        if (_host.Currency.Category is not null && IsLoaded) BuildCategories();
    }

    private void Recompute()
    {
        SetButton(_fromButton, _from); SetButton(_toButton, _to);
        _others.Children.Clear();
        if (!TryParse(_input.Text, out var value)) { _result.Text = "-"; _recent.Text = ""; return; }
        var converted = UnitConverter.Convert(value, _from, _to);
        _result.Text = UnitConverter.Format(converted);
        _recent.Text = $"Recent   {UnitConverter.Format(value)} {_from.Symbol} → {_result.Text} {_to.Symbol}";

        foreach (var u in _category.Units.Where(u => u.Id != _from.Id && u.Id != _to.Id).Take(4))
        {
            var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            g.Children.Add(new TextBlock { Text = u.Name, Style = (Style)FindResource("Style.Text"), FontSize = 13 });
            var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            right.Children.Add(new TextBlock { Text = UnitConverter.Format(UnitConverter.Convert(value, _from, u)), Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.Medium, FontSize = 13 });
            right.Children.Add(new TextBlock { Text = u.Symbol, Style = (Style)FindResource("Style.TextDim"), FontSize = 13, Margin = new Thickness(8, 0, 0, 0), Width = 28 });
            g.Children.Add(right);
            _others.Children.Add(new Border { Child = g, BorderBrush = (Brush)FindResource("Brush.Divider"), BorderThickness = new Thickness(0, 1, 0, 0) });
        }
    }

    private static void SetButton(Button b, UnitDef u)
    {
        var (name, sym) = ((TextBlock, TextBlock))b.Tag!;
        name.Text = u.Name; sym.Text = u.Symbol;
    }

    private static bool TryParse(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value);
}
