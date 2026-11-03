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
    public event Action<bool>? PinToggled;

    public ShellContent(AppHost host)
    {
        InitializeComponent();
        _host = host;

        Header.CloseClicked += () => CloseRequested?.Invoke();
        Header.PinToggled += pinned => PinToggled?.Invoke(pinned);
        Header.EditToggled += editing => { if (Page.Content is Pages.HomePage home) home.Editing = editing; };
        UndoButton.Click += (_, _) => { if (Page.Content is Pages.HomePage home) home.Undo(); };
        Header.SettingsClicked += () => _host.Nav.GoTo(Navigator.Settings);

        Dock.Selected += id => { CloseAddDock(); _host.Nav.GoTo(id); };
        Dock.AddClicked += ToggleAddDock;
        AddDock.Bind(host, () => { _host.Save(); RebuildDock(); });

        _host.Nav.Navigated += _ => ShowPage();
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
        if (id == Navigator.Settings)
        {
            (title, subtitle) = ("Settings", "");
            page = new TextBlock { Text = "Settings", Foreground = System.Windows.Media.Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }
        else
        {
            var module = _host.Modules[id];
            title = module.Info.Title;
            subtitle = id == Navigator.Home ? ClockFormatter.HeaderDate(DateTimeOffset.Now) : module.Info.Subtitle;
            page = module.CreatePage();
        }
        Header.SetPage(title, subtitle);
        Header.SetHomeMode(id == Navigator.Home);
        UndoButton.Visibility = Visibility.Collapsed;
        if (page is Pages.HomePage homePage)
            homePage.StateChanged += () => UndoButton.Visibility = homePage.Editing && homePage.CanUndo ? Visibility.Visible : Visibility.Collapsed;
        Dock.SetActive(id);

        Page.Content = page;
        Page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
    }
}
