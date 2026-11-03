using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VAK.App.Controls;
using VAK.App.Theme;
using VAK.Core.Timers;

namespace VAK.App.Cards;

/// <summary>Home card: the Pomodoro time, its state, and a round play/pause button.</summary>
public sealed class FocusCard : UserControl
{
    private readonly AppHost _host;
    private readonly TextBlock _time;
    private readonly TextBlock _state;
    private readonly TextBlock _playIcon;

    public FocusCard(AppHost host)
    {
        _host = host;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(new CardHeader("timers", "Focus"));

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _time = new TextBlock { Style = (Style)FindResource("Style.Big"), FontSize = 34 };
        _state = new TextBlock { Style = (Style)FindResource("Style.TextDim"), FontSize = 13 };
        stack.Children.Add(_time);
        stack.Children.Add(_state);
        Grid.SetRow(stack, 1);
        root.Children.Add(stack);

        _playIcon = new TextBlock { Style = (Style)FindResource("Style.Icon"), FontSize = 20 };
        var play = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = 50, Height = 50, Background = (Brush)FindResource("Brush.Control"),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Content = _playIcon,
            ToolTip = "Start or pause focus",
        };
        play.Click += (_, _) =>
        {
            var p = _host.Timers.Pomodoro;
            if (p.Running) p.Pause(); else p.Start();
            _host.Timers.Touch();
        };
        Grid.SetRow(play, 1);
        root.Children.Add(play);
        Content = root;

        Loaded += (_, _) => { _host.Timers.Changed += Render; Render(); };
        Unloaded += (_, _) => _host.Timers.Changed -= Render;
    }

    private void Render()
    {
        var p = _host.Timers.Pomodoro;
        _time.Text = TimerFormatter.Mmss(p.Remaining);
        _state.Text = !p.Running && p.Remaining == TimeSpan.FromMinutes(_host.Settings.Timers.FocusMinutes) && p.Phase == PomodoroPhase.Focus
            ? "Ready"
            : p.Phase switch { PomodoroPhase.Focus => "Focus", PomodoroPhase.Break => "Break", _ => "Long break" };
        _playIcon.Text = Icons.Glyph(p.Running ? "pause" : "play");
    }
}
