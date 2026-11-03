using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VAK.App.Theme;
using VAK.Core.Widgets;

namespace VAK.App.Shell;

public partial class DockView : UserControl
{
    /// <summary>Home plus the user's docked widgets, and nothing else, fit in one dock.</summary>
    public const int Capacity = 14;

    private readonly Dictionary<string, Button> _buttons = new();
    private Button? _addButton;
    private TextBlock? _addIcon;
    private string _activeId = Navigator.Home;

    public event Action<string>? Selected;
    public event Action? AddClicked;

    public DockView() => InitializeComponent();

    /// <summary>Rebuilds the dock from the ordered widgets (Home first).</summary>
    public void Build(IEnumerable<WidgetInfo> widgets)
    {
        Buttons.Children.Clear();
        _buttons.Clear();
        foreach (var w in widgets)
        {
            var icon = new TextBlock { Text = Icons.Glyph(w.IconKey), Style = (Style)FindResource("Style.Icon") };
            var button = new Button
            {
                Style = (Style)FindResource("Style.RoundButton"), Content = icon, ToolTip = w.Title,
                Margin = new Thickness(0, 0, 9, 0),
            };
            var id = w.Id;
            button.Click += (_, _) => Selected?.Invoke(id);
            _buttons[id] = button;
            Buttons.Children.Add(button);
        }
        _addIcon = new TextBlock { Text = Icons.Glyph("add"), Style = (Style)FindResource("Style.Icon") };
        _addButton = new Button { Style = (Style)FindResource("Style.RoundButton"), Content = _addIcon, ToolTip = "Add to dock" };
        _addButton.Click += (_, _) => AddClicked?.Invoke();
        Buttons.Children.Add(new Border
        {
            Width = 1, Height = 22, Background = (Brush)FindResource("Brush.Divider"), Margin = new Thickness(0, 0, 14, 0),
        });
        Buttons.Children.Add(_addButton);
        SetActive(_activeId);
    }

    public void SetActive(string id)
    {
        _activeId = id;
        foreach (var (key, button) in _buttons)
        {
            var active = key == id;
            button.Background = active ? Brushes.White : Brushes.Transparent;
            ((TextBlock)button.Content).Foreground = active ? Brushes.Black : (Brush)FindResource("Brush.Text");
        }
    }

    /// <summary>While the Add-to-dock picker is open the "+" button shows a close glyph and stands out.</summary>
    public void SetAddOpen(bool open)
    {
        if (_addButton is null || _addIcon is null) return;
        _addIcon.Text = Icons.Glyph(open ? "close" : "add");
        _addButton.Background = open ? Brushes.White : Brushes.Transparent;
        _addIcon.Foreground = open ? Brushes.Black : (Brush)FindResource("Brush.Text");
    }
}
