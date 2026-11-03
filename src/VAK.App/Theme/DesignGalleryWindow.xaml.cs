using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VAK.App.Theme;

/// <summary>Debug view of every design token, style and icon. Replaced as the entry window in the shell task.</summary>
public partial class DesignGalleryWindow : Window
{
    public DesignGalleryWindow()
    {
        InitializeComponent();
        foreach (var key in new[] { "Panel", "Card", "Control", "Accent", "Primary", "Text", "TextDim" })
        {
            Swatches.Children.Add(new Border
            {
                Width = 90, Height = 40, Margin = new Thickness(0, 0, 8, 0), CornerRadius = new CornerRadius(10),
                BorderBrush = (Brush)FindResource("Brush.Control"), BorderThickness = new Thickness(1),
                Background = (Brush)FindResource("Brush." + key),
                Child = new TextBlock { Text = key, FontSize = 10, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center },
            });
        }
        foreach (var key in Icons.Keys)
        {
            var t = new TextBlock { Text = Icons.Glyph(key), Margin = new Thickness(0, 0, 14, 8) };
            t.Style = (Style)FindResource("Style.Icon");
            t.FontSize = 22;
            IconGrid.Children.Add(t);
        }
        SampleIcon.Text = Icons.Glyph("play");
    }
}
