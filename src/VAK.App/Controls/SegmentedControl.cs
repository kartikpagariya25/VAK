using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VAK.App.Controls;

/// <summary>A pill of mutually exclusive options ("Digital | Analog", "Full | Short | Compact"). The selected one gets an accent outline.</summary>
public sealed class SegmentedControl : Border
{
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal };
    private readonly List<Border> _segments = new();
    private int _selected = -1;

    public event Action<int>? SelectionChanged;

    public SegmentedControl(params string[] options)
    {
        Background = (Brush)Application.Current.FindResource("Brush.Control");
        CornerRadius = new CornerRadius(17);
        Padding = new Thickness(3);
        Child = _panel;
        for (var i = 0; i < options.Length; i++)
        {
            var index = i;
            var seg = new Border
            {
                CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 4, 14, 4), Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent, Background = Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = options[i], Style = (Style)Application.Current.FindResource("Style.Text"), FontSize = 12,
                    FontWeight = FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Center,
                },
            };
            seg.MouseLeftButtonUp += (_, _) => { SelectedIndex = index; SelectionChanged?.Invoke(index); };
            _segments.Add(seg);
            _panel.Children.Add(seg);
        }
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            _selected = value;
            for (var i = 0; i < _segments.Count; i++)
            {
                var on = i == value;
                _segments[i].BorderBrush = on ? (Brush)Application.Current.FindResource("Brush.Accent") : Brushes.Transparent;
                _segments[i].Background = on ? (Brush)Application.Current.FindResource("Brush.Card") : Brushes.Transparent;
            }
        }
    }
}
