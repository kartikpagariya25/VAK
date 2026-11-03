using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VAK.Core.Weather;

namespace VAK.App.Controls;

/// <summary>A popup with a search box that looks places up on Open-Meteo's geocoder and returns the chosen one.</summary>
public static class CityPicker
{
    public static void Show(FrameworkElement anchor, AppHost host, Action<GeoPlace> chosen)
    {
        var res = Application.Current.Resources;
        var results = new StackPanel();
        var status = new TextBlock { Foreground = (Brush)res["Brush.TextDim"], FontSize = 12, Margin = new Thickness(10, 6, 10, 6), Text = "Type a city and press Enter" };
        results.Children.Add(status);
        var search = new TextBox
        {
            Background = (Brush)res["Brush.Control"], Foreground = Brushes.White, CaretBrush = Brushes.White, BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 7, 10, 7), FontSize = 13, FontFamily = (FontFamily)res["Font.Ui"],
        };
        var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 6 };

        async void Run()
        {
            var text = search.Text;
            status.Text = "Searching…";
            results.Children.Clear();
            results.Children.Add(status);
            var places = await host.Weather.SearchAsync(text);
            if (search.Text != text) return; // a newer search is already running
            results.Children.Clear();
            if (places.Count == 0) { status.Text = "No places found, or you are offline."; results.Children.Add(status); return; }
            foreach (var place in places)
            {
                var row = new TextBlock { Text = place.Display, Foreground = Brushes.White, FontSize = 13, FontFamily = (FontFamily)res["Font.Ui"], Padding = new Thickness(10, 6, 10, 6), Cursor = Cursors.Hand, TextTrimming = TextTrimming.CharacterEllipsis };
                row.MouseEnter += (_, _) => row.Background = (Brush)res["Brush.Control"];
                row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
                var p = place;
                row.MouseLeftButtonUp += (_, _) => { popup.IsOpen = false; chosen(p); };
                results.Children.Add(row);
            }
        }

        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        debounce.Tick += (_, _) => { debounce.Stop(); if (search.Text.Trim().Length >= 2) Run(); };
        search.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };
        search.KeyDown += (_, e) => { if (e.Key == Key.Enter) { debounce.Stop(); Run(); } };
        popup.Closed += (_, _) => debounce.Stop();

        var panel = new StackPanel();
        panel.Children.Add(search);
        panel.Children.Add(new ScrollViewer { MaxHeight = 200, Content = results, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Margin = new Thickness(0, 4, 0, 0) });
        popup.Child = new Border
        {
            Width = 320, Padding = new Thickness(6), CornerRadius = new CornerRadius(12), Child = panel,
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)), BorderBrush = (Brush)res["Brush.Control"], BorderThickness = new Thickness(1),
        };
        popup.IsOpen = true;
        search.Focus();
    }
}
