using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using VAK.App.Controls;
using VAK.App.Services;
using VAK.App.Widgets;

namespace VAK.App.Pages;

/// <summary>Claude and GPT from the island. Website mode shows the real site in an embedded browser (sign in once on the site);
/// API mode is a native chat that uses the user's own API key.</summary>
public sealed class AiChatPage : UserControl, IHeaderExtras, ITallPage
{
    private readonly AppHost _host;
    private readonly Grid _body = new();
    private WebView2CompositionControl? _shownView;

    public double PreferredHeight => 560;

    public AiChatPage(AppHost host)
    {
        _host = host;
        Content = _body;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => ReleaseView();
    }

    public UIElement CreateHeaderExtras()
    {
        var ai = _host.Settings.Ai;
        var provider = new SegmentedControl("Claude", "GPT") { SelectedIndex = ai.Provider == "gpt" ? 1 : 0 };
        provider.SelectionChanged += i => { ai.Provider = i == 1 ? "gpt" : "claude"; _host.Save(); Render(); };
        var mode = new SegmentedControl("Website", "API") { SelectedIndex = ai.Mode == "api" ? 1 : 0, Margin = new Thickness(8, 0, 0, 0) };
        mode.SelectionChanged += i => { ai.Mode = i == 1 ? "api" : "web"; _host.Save(); Render(); };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(provider); row.Children.Add(mode);
        return row;
    }

    private void ReleaseView()
    {
        if (_shownView is not null) AiWebViews.Detach(_shownView);
        _shownView = null;
    }

    private TextBlock Message(string text) => new()
    {
        Text = text, Style = (Style)FindResource("Style.TextDim"), FontSize = 13, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 460,
    };

    private void Render()
    {
        ReleaseView();
        _body.Children.Clear();
        var ai = _host.Settings.Ai;
        var provider = AiWebViews.Provider(ai.Provider);

        if (ai.Mode == "api")
        {
            _body.Children.Add(Message($"API chat for {provider.Name} is coming next. Switch to Website to use {provider.Name} now."));
            return;
        }
        if (!AiWebViews.RuntimeAvailable())
        {
            _body.Children.Add(Message("The Microsoft Edge WebView2 runtime is needed to show " + provider.Name + " here. It is built into Windows 11; on older systems install it from Microsoft."));
            return;
        }

        var status = Message($"Loading {provider.Name}…  Sign in on the {provider.Name} page the first time; VAK never sees your password.");
        _body.Children.Add(status);
        var view = _host.AiWeb.Get(provider.Id, problem => Dispatcher.Invoke(() => status.Text = problem));
        AiWebViews.Detach(view);
        view.Margin = new Thickness(6, 0, 6, 6);
        _body.Children.Add(view);
        _shownView = view;
    }
}
