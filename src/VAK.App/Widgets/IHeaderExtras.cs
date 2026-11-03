using System.Windows;

namespace VAK.App.Widgets;

/// <summary>A page that adds its own controls to the header (for example Weather's units toggle and Change City).</summary>
public interface IHeaderExtras
{
    UIElement CreateHeaderExtras();
}
