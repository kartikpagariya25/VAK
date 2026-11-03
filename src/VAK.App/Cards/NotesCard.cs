using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.App.Theme;
using VAK.Core.Notes;

namespace VAK.App.Cards;

/// <summary>Home card: the latest note's title (or "No notes yet") and a quick-add box.</summary>
public sealed class NotesCard : UserControl
{
    private readonly AppHost _host;
    private readonly TextBlock _latest;
    private readonly TextBox _input;

    public NotesCard(AppHost host)
    {
        _host = host;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new CardHeader("note", "Notes"));

        _latest = new TextBlock { Style = (Style)FindResource("Style.Text"), FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetRow(_latest, 1);
        root.Children.Add(_latest);

        _input = new TextBox
        {
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, CaretBrush = Brushes.White,
            FontFamily = (FontFamily)FindResource("Font.Ui"), FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(8, 0, 8, 0),
        };
        var placeholder = new TextBlock { Text = "Write a note…", Style = (Style)FindResource("Style.TextDim"), FontSize = 12, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        _input.TextChanged += (_, _) => placeholder.Visibility = _input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _input.KeyDown += (_, e) => { if (e.Key == Key.Enter) Add(); };
        var box = new Border { CornerRadius = new CornerRadius(10), Background = (Brush)FindResource("Brush.Control"), Height = 30, Child = new Grid { Children = { placeholder, _input } } };

        var add = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 28, Height = 28, ToolTip = "Add note", Margin = new Thickness(8, 0, 0, 0),
            Content = new TextBlock { Text = Icons.Glyph("add"), Style = (Style)FindResource("Style.Icon"), FontSize = 16, Foreground = (Brush)FindResource("Brush.TextDim") },
        };
        add.Click += (_, _) => Add();
        var bottom = new Grid();
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.Children.Add(box);
        Grid.SetColumn(add, 1); bottom.Children.Add(add);
        Grid.SetRow(bottom, 2);
        root.Children.Add(bottom);
        Content = root;
        Loaded += (_, _) => Render();
    }

    private void Add()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0) return;
        _host.Notes.Create(text);
        _input.Text = "";
        Render();
    }

    private void Render()
    {
        var latest = _host.Notes.All.FirstOrDefault();
        _latest.Text = latest is null ? "No notes yet" : NoteText.Title(latest.Body);
    }
}
