using System.Windows;
using System.Windows.Media;
using VAK.Core.Settings;

namespace VAK.App.Theme;

/// <summary>Applies a theme by recolouring the shared brushes in place, so every page that already uses them follows at once.</summary>
public static class ThemeService
{
    private static string? _current;

    /// <summary>Applies the theme. Returns true when the theme actually changed, so callers can rebuild what drew with the old colours.</summary>
    public static bool Apply(string themeName)
    {
        var p = Themes.Get(themeName);
        if (_current == p.Name) return false;
        _current = p.Name;
        Set("Brush.Panel", p.Panel); Set("Brush.Card", p.Card); Set("Brush.Control", p.Control);
        Set("Brush.Accent", p.Accent); Set("Brush.Primary", p.Primary);
        Set("Brush.Text", p.Text); Set("Brush.TextDim", p.TextDim); Set("Brush.TextFaint", p.TextFaint); Set("Brush.Divider", p.Divider);
        return true;
    }

    /// <summary>Puts a new brush under the key. Pages and styles use DynamicResource for these brushes, so they repaint at once;
    /// code that fetched a brush earlier keeps the old colour until its page is rebuilt.</summary>
    private static void Set(string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
            if (dictionary.Contains(key)) { dictionary[key] = brush; return; }
        Application.Current.Resources[key] = brush;
    }

    public static Color Parse(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch (FormatException) { return fallback; }
    }
}
