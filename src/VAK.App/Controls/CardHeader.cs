using System.Windows;
using System.Windows.Controls;
using VAK.App.Theme;

namespace VAK.App.Controls;

/// <summary>The small icon + CAPS label row at the top of a card ("SYSTEM", "FOCUS").</summary>
public sealed class CardHeader : StackPanel
{
    public CardHeader(string iconKey, string label)
    {
        Orientation = Orientation.Horizontal;
        Children.Add(new TextBlock
        {
            Text = Icons.Glyph(iconKey), FontFamily = (System.Windows.Media.FontFamily)Application.Current.FindResource("Font.Icons"),
            FontSize = 14, Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("Brush.TextDim"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
        });
        Children.Add(new TextBlock
        {
            Text = label.ToUpperInvariant(), Style = (Style)Application.Current.FindResource("Style.TextDim"),
            FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
        });
    }
}
