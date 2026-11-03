using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using VAK.Core.Clock;
using VAK.Core.Widgets;

namespace VAK.App.Shell;

/// <summary>The expanded panel: header, current page, dock and the Add-to-dock overlay.</summary>
public partial class ShellContent : UserControl
{
    private readonly AppHost _host;
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    public event Action? CloseRequested;

    /// <summary>The page now showing wants the panel this tall (388 unless the page says otherwise).</summary>
    public event Action<double>? PanelHeightRequested;
    public event Action<bool>? PinToggled;

    public ShellContent(AppHost host)
    {
        InitializeComponent();
        _host = host;

        Header.CloseClicked += () => CloseRequested?.Invoke();
        Header.PowerClicked += () => _host.TurnOffNotch();
        Header.PinToggled += pinned => PinToggled?.Invoke(pinned);
        Header.EditToggled += editing => { if (Page.Content is Pages.HomePage home) home.Editing = editing; };
        UndoButton.Click += (_, _) => { if (Page.Content is Pages.HomePage home) home.Undo(); };
        Header.SettingsClicked += () => _host.OpenSettings?.Invoke(null);

        Dock.Selected += id => { CloseAddDock(); _host.Nav.GoTo(id); };
        Dock.AddClicked += ToggleAddDock;
        AddDock.Bind(host, () => { _host.Save(); RebuildDock(); });

        _host.Nav.Navigated += _ => ShowPage();
        _host.ThemeChanged += () => Dispatcher.Invoke(() => { RebuildDock(); ShowPage(); });
        _host.ModulesChanged += () => Dispatcher.Invoke(() => { RebuildDock(); if (_host.Nav.CurrentId == Navigator.Home) ShowPage(); });
        _statusTimer.Tick += (_, _) => Header.SetBattery(_host.Battery.Read());
        _statusTimer.Start();

        RebuildDock();
        ShowPage();
        Header.SetBattery(_host.Battery.Read());
    }

    private void RebuildDock()
    {
        var ordered = new List<WidgetInfo> { _host.Registry.Find(Navigator.Home)! };
        ordered.AddRange(_host.Settings.Dock.Select(id => _host.Registry.Find(id)).OfType<WidgetInfo>());
        Dock.Build(ordered);
        Dock.SetActive(_host.Nav.CurrentId);
    }

    private void ToggleAddDock()
    {
        var open = AddDock.Visibility != Visibility.Visible;
        if (open) { AddDock.ResetSearch(); AddDock.Refresh(); }
        AddDock.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        Dock.SetAddOpen(open);
    }

    private void CloseAddDock()
    {
        AddDock.Visibility = Visibility.Collapsed;
        Dock.SetAddOpen(false);
    }

    private void ShowPage()
    {
        var id = _host.Nav.CurrentId;
        string title, subtitle;
        FrameworkElement page;
        var module = _host.Modules[id];
        title = module.Info.Title;
        subtitle = id == Navigator.Home ? ClockFormatter.HeaderDate(DateTimeOffset.Now) : module.Info.Subtitle;
        page = module.CreatePage();
        Header.SetPage(title, subtitle);
        Header.SetExtras((page as Widgets.IHeaderExtras)?.CreateHeaderExtras());
        PanelHeightRequested?.Invoke(page is Widgets.ITallPage tall ? tall.PreferredHeight : 388);
        Header.SetHomeMode(id == Navigator.Home);
        UndoButton.Visibility = Visibility.Collapsed;
        if (page is Pages.HomePage homePage)
            homePage.StateChanged += () => UndoButton.Visibility = homePage.Editing && homePage.CanUndo ? Visibility.Visible : Visibility.Collapsed;
        Dock.SetActive(id);

        Page.Content = page;
        if (_host.Settings.Notch.TabAnimations) Page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
        else { Page.BeginAnimation(OpacityProperty, null); Page.Opacity = 1; }
    }
}
