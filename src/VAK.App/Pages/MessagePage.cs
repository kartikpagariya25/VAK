using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.App.Theme;
using VAK.Core.Message;
using VAK.Core.Settings;

namespace VAK.App.Pages;

public sealed class MessagePage : UserControl
{
    private static readonly string[] AnimationNames = { "Slide", "Fade", "Type-on", "Scroll", "Blur", "Pulse" };
    private static readonly string[] FinishNames = { "Solid", "Silver", "Ocean", "Glow" };

    private readonly AppHost _host;
    private readonly MessageSettings _m;
    private readonly TextBox _input;
    private readonly TextBlock _counter;
    private readonly BannerView _preview = new() { Height = 34, Width = 320 };
    private readonly Border _notch;
    private readonly TextBlock _shows;
    private readonly WrapPanel _anim = new(), _swatches = new(), _finish = new(), _duration = new();
    private readonly TextBlock _speedLabel;
    private readonly Slider _speed;
    private readonly CheckBox _repeat;

    public MessagePage(AppHost host)
    {
        _host = host;
        _m = host.Settings.Message;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // live preview
        _notch = new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(0, 0, 16, 16), Height = 34, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Child = _preview, BorderBrush = (Brush)FindResource("Brush.Control"), BorderThickness = new Thickness(1, 0, 1, 1) };
        _shows = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        var previewGrid = new Grid { Height = 44 };
        previewGrid.Children.Add(new CardHeader("message", "Live preview"));
        previewGrid.Children.Add(_notch);
        previewGrid.Children.Add(_shows);
        root.Children.Add(new Border { Style = (Style)FindResource("Style.Card"), Child = previewGrid, Margin = new Thickness(5, 3, 5, 3), Padding = new Thickness(14, 6, 14, 0) });

        // input row
        _input = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, CaretBrush = Brushes.White, FontFamily = (FontFamily)FindResource("Font.Ui"), FontSize = 14, MaxLength = BannerSpec.MaxText, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(0) };
        var hint = new TextBlock { Text = "Type a message", Style = (Style)FindResource("Style.TextDim"), FontSize = 14, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
        _counter = new TextBlock { Text = $"0/{BannerSpec.MaxText}", Style = (Style)FindResource("Style.TextDim"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        _input.TextChanged += (_, _) => { hint.Visibility = _input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; _counter.Text = $"{_input.Text.Length}/{BannerSpec.MaxText}"; };
        _input.KeyDown += (_, e) => { if (e.Key == Key.Enter) Flash(); };
        var field = new Grid();
        field.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        field.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        field.Children.Add(new Grid { Children = { hint, _input } });
        Grid.SetColumn(_counter, 1); field.Children.Add(_counter);
        var fieldBox = new Border { Style = (Style)FindResource("Style.Card"), Child = field, Padding = new Thickness(14, 0, 14, 0), Height = 38 };

        var flash = Button("Flash", (Brush)FindResource("Brush.Primary"), 100, Flash);
        var clear = Button("Clear", (Brush)FindResource("Brush.Control"), 76, () => { _host.ClearBanner(); _preview.Stop(); _input.Text = ""; });
        var inputRow = new Grid { Margin = new Thickness(5, 0, 5, 0) };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputRow.Children.Add(fieldBox);
        flash.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(flash, 1); inputRow.Children.Add(flash);
        clear.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(clear, 2); inputRow.Children.Add(clear);
        Grid.SetRow(inputRow, 1);
        root.Children.Add(inputRow);

        // option cards
        var cards = new Grid { Margin = new Thickness(0, 3, 0, 0) };
        cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _speedLabel = new TextBlock { Style = (Style)FindResource("Style.Text"), FontSize = 12, FontWeight = FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Right };
        _speed = new Slider { Style = (Style)FindResource("Style.Slider"), Minimum = 0.5, Maximum = 2, Value = _m.Speed, Margin = new Thickness(8, 0, 8, 0) };
        _speed.ValueChanged += (_, e) => { _m.Speed = Math.Round(e.NewValue, 2); _speedLabel.Text = SpeedName(_m.Speed); _host.Save(); };
        var speedRow = new Grid { Margin = new Thickness(0, 2, 0, 0), Height = 24 };
        speedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        speedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        speedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        speedRow.Children.Add(new TextBlock { Text = "Speed", Style = (Style)FindResource("Style.TextDim"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(_speed, 1); speedRow.Children.Add(_speed);
        Grid.SetColumn(_speedLabel, 2); speedRow.Children.Add(_speedLabel);
        var animStack = new StackPanel();
        animStack.Children.Add(new CardHeader("play", "Animation"));
        animStack.Children.Add(new Border { Child = _anim, Margin = new Thickness(0, 8, 0, 0) });
        animStack.Children.Add(speedRow);
        cards.Children.Add(Card(animStack, 0));

        var colorStack = new StackPanel();
        colorStack.Children.Add(new CardHeader("tag", "Colour"));
        colorStack.Children.Add(new Border { Child = _swatches, Margin = new Thickness(0, 8, 0, 0) });
        var finishRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        finishRow.Children.Add(new TextBlock { Text = "Finish", Style = (Style)FindResource("Style.TextDim"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 5) });
        finishRow.Children.Add(_finish);
        colorStack.Children.Add(finishRow);
        cards.Children.Add(Card(colorStack, 1));

        _repeat = new CheckBox { Style = (Style)FindResource("Style.Toggle"), IsChecked = _m.Repeat };
        _repeat.Click += (_, _) => { _m.Repeat = _repeat.IsChecked == true; _host.Save(); UpdateShows(); };
        var durStack = new StackPanel();
        durStack.Children.Add(new CardHeader("clock", "Duration"));
        durStack.Children.Add(new Border { Child = _duration, Margin = new Thickness(0, 8, 0, 0) });
        var repeatRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        repeatRow.Children.Add(new TextBlock { Text = "Repeat until taken down", Style = (Style)FindResource("Style.TextDim"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        _repeat.HorizontalAlignment = HorizontalAlignment.Right;
        repeatRow.Children.Add(_repeat);
        durStack.Children.Add(repeatRow);
        cards.Children.Add(Card(durStack, 2));
        Grid.SetRow(cards, 2);
        root.Children.Add(cards);
        Content = root;

        BuildAnimation(); BuildSwatches(); BuildFinish(); BuildDuration();
        _speedLabel.Text = SpeedName(_m.Speed);
        UpdateShows();
    }

    private Border Card(UIElement child, int column)
    {
        var c = new Border { Style = (Style)FindResource("Style.Card"), Child = child, Margin = new Thickness(5, 3, 5, 3), Padding = new Thickness(12, 10, 12, 8) };
        Grid.SetColumn(c, column);
        return c;
    }

    private Button Button(string label, Brush bg, double width, Action click)
    {
        var b = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = width, Height = 38, Background = bg,
            Content = new TextBlock { Text = label, Style = (Style)FindResource("Style.Text"), FontWeight = FontWeights.SemiBold },
        };
        b.Click += (_, _) => click();
        return b;
    }

    private Border Chip(string text, bool selected, Action click)
    {
        var chip = new Border
        {
            CornerRadius = new CornerRadius(13), Padding = new Thickness(9, 3, 9, 3), Margin = new Thickness(0, 0, 5, 5), Cursor = Cursors.Hand,
            Background = (Brush)FindResource("Brush.Control"), BorderThickness = new Thickness(1),
            BorderBrush = selected ? (Brush)FindResource("Brush.Accent") : Brushes.Transparent,
            Child = new TextBlock { Text = text, Style = (Style)FindResource("Style.Text"), FontSize = 12, FontWeight = FontWeights.Medium },
        };
        chip.MouseLeftButtonUp += (_, _) => click();
        return chip;
    }

    private void BuildAnimation()
    {
        _anim.Children.Clear();
        for (var i = 0; i < AnimationNames.Length; i++)
        {
            var index = i;
            _anim.Children.Add(Chip(AnimationNames[i], _m.Animation == i, () => { _m.Animation = index; _host.Save(); BuildAnimation(); }));
        }
    }

    private void BuildSwatches()
    {
        _swatches.Children.Clear();
        foreach (var hex in BannerPalette.Colors)
        {
            var selected = string.Equals(_m.Color, hex, StringComparison.OrdinalIgnoreCase);
            var dot = new Border
            {
                Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Margin = new Thickness(0, 0, 8, 6), Cursor = Cursors.Hand,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                BorderThickness = new Thickness(2), BorderBrush = selected ? Brushes.White : Brushes.Transparent,
            };
            var c = hex;
            dot.MouseLeftButtonUp += (_, _) => { _m.Color = c; _host.Save(); BuildSwatches(); };
            _swatches.Children.Add(dot);
        }
    }

    private void BuildFinish()
    {
        _finish.Children.Clear();
        for (var i = 0; i < FinishNames.Length; i++)
        {
            var index = i;
            _finish.Children.Add(Chip(FinishNames[i], _m.Finish == i, () => { _m.Finish = index; _host.Save(); BuildFinish(); }));
        }
    }

    private void BuildDuration()
    {
        _duration.Children.Clear();
        foreach (var s in new[] { 3, 5, 10 })
        {
            var seconds = s;
            _duration.Children.Add(Chip($"{s}s", _m.Seconds == s, () => { _m.Seconds = seconds; _host.Save(); BuildDuration(); UpdateShows(); }));
        }
        var custom = !new[] { 3, 5, 10 }.Contains(_m.Seconds);
        Border? chip = null;
        chip = Chip(custom ? $"{_m.Seconds}s" : "Custom", custom, () => EditCustom(chip!));
        _duration.Children.Add(chip);
    }

    private void EditCustom(FrameworkElement anchor)
    {
        var box = new TextBox { Text = _m.Seconds.ToString(CultureInfo.InvariantCulture), Width = 80, Background = (Brush)FindResource("Brush.Control"), Foreground = Brushes.White, CaretBrush = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6) };
        var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 4 };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Seconds (1-60)", Style = (Style)FindResource("Style.TextDim"), FontSize = 11 });
        panel.Children.Add(box);
        popup.Child = new Border { Padding = new Thickness(10), CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)), BorderBrush = (Brush)FindResource("Brush.Control"), BorderThickness = new Thickness(1), Child = panel };
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || !int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) return;
            _m.Seconds = Math.Clamp(s, 1, 60);
            _host.Save(); popup.IsOpen = false; BuildDuration(); UpdateShows();
        };
        popup.IsOpen = true;
        box.Focus(); box.SelectAll();
    }

    private static string SpeedName(double s) => s < 0.9 ? "Slow" : s > 1.3 ? "Fast" : "Normal";

    private void UpdateShows() => _shows.Text = _m.Repeat ? "Stays until cleared" : $"Shows for {_m.Seconds}s";

    private BannerSpec? Spec() => BannerSpec.TryCreate(_input.Text, (BannerAnimation)Math.Clamp(_m.Animation, 0, 5), _m.Speed, _m.Color,
        (BannerFinish)Math.Clamp(_m.Finish, 0, 3), _m.Seconds, _m.Repeat);

    private void Flash()
    {
        if (Spec() is not { } spec) return;
        _notch.Width = BannerView.PreferredWidth(spec);
        _preview.Width = _notch.Width;
        _preview.Play(spec);
        _host.FlashBanner(spec);
    }
}
