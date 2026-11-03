using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VAK.App.Theme;

/// <summary>Turns an element height into a corner radius of half that height, so wide buttons get true pill ends
/// (a fixed huge radius makes WPF draw elliptical corners on a wide border).</summary>
public sealed class PillRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new CornerRadius(value is double h && h > 0 ? h / 2 : 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
