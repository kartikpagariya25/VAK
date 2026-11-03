using System.Windows;
using VAK.Core.Widgets;

namespace VAK.App.Widgets;

/// <summary>A widget's metadata plus factories for its Home card and its full page.
/// <paramref name="Blurb"/> is the one-line description shown in the Add-to-dock picker.</summary>
public record WidgetModule(WidgetInfo Info, Func<FrameworkElement> CreateCard, Func<FrameworkElement> CreatePage, string Blurb = "");
