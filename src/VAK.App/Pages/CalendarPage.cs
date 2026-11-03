using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Services;
using VAK.App.Theme;
using VAK.Core.Message;
using VAK.Core.Planner;

namespace VAK.App.Pages;

public sealed class CalendarPage : UserControl
{
    private static readonly string[] ColorChoices = { "#2F7CF6", "#30D158", "#F5A623", "#EF6C4D", "#F43F6E", "#A855F7", "#4FB8FF" };
    private static readonly (string Label, int Minutes)[] Reminders = { ("None", -1), ("At start", 0), ("5 min", 5), ("15 min", 15), ("30 min", 30), ("1 hour", 60) };

    private readonly AppHost _host;
    private readonly Grid _root = new();
    private readonly Grid _right = new();
    private DateTime _month;      // first day of the month on show
    private DateTime _selected;   // the selected day
    private CalendarEvent? _editing;
    private bool _isNew;

    private CalendarService Service => _host.Calendar;
    private DayOfWeek FirstDay => CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    public CalendarPage(AppHost host)
    {
        _host = host;
        _selected = DateTime.Today;
        _month = new DateTime(_selected.Year, _selected.Month, 1);
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_right, 1);
        _root.Children.Add(_right);
        Content = _root;
        Loaded += (_, _) => { Service.Changed += Render; Render(); };
        Unloaded += (_, _) => Service.Changed -= Render;
    }

    private TextBlock T(string text, string style = "Style.Text", double size = 13, FontWeight? weight = null)
    {
        var t = new TextBlock { Text = text, Style = (Style)FindResource(style), FontSize = size };
        if (weight is { } w) t.FontWeight = w;
        return t;
    }

    private string Time(DateTime t) => _host.Settings.Use24Hour ? t.ToString("HH:mm", CultureInfo.InvariantCulture) : t.ToString("h:mm tt", CultureInfo.InvariantCulture);

    private void Render()
    {
        if (_root.Children.Count > 1) _root.Children.RemoveRange(0, _root.Children.Count - 1);
        _root.Children.Insert(0, MonthCard());
        _right.Children.Clear();
        _right.Children.Add(_editing is null ? Agenda() : Editor());
    }

    // ---- month ------------------------------------------------------------------------------

    private UIElement MonthCard()
    {
        var head = new Grid { Height = 30 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(T(_month.ToString("MMMM yyyy", CultureInfo.InvariantCulture), "Style.Text", 16, FontWeights.SemiBold));
        var nav = new StackPanel { Orientation = Orientation.Horizontal };
        nav.Children.Add(NavButton("‹", "Previous month", () => { _month = _month.AddMonths(-1); Render(); }));
        nav.Children.Add(NavButton("Today", "Go to today", () => { _selected = DateTime.Today; _month = new DateTime(_selected.Year, _selected.Month, 1); Render(); }));
        nav.Children.Add(NavButton("›", "Next month", () => { _month = _month.AddMonths(1); Render(); }));
        Grid.SetColumn(nav, 1); head.Children.Add(nav);

        var grid = new UniformGrid { Columns = 7, Rows = 7 };
        var names = Enumerable.Range(0, 7).Select(i => (DayOfWeek)(((int)FirstDay + i) % 7));
        foreach (var d in names)
        {
            var label = T(CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(d)[..2], "Style.TextDim", 10);
            label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(label);
        }
        var dots = Service.Store.DaysWithEvents(_month.Year, _month.Month);
        foreach (var day in MonthGrid.Build(_month.Year, _month.Month, FirstDay))
            grid.Children.Add(Cell(day, dots));

        var stack = new Grid();
        stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        stack.Children.Add(head);
        Grid.SetRow(grid, 1);
        stack.Children.Add(grid);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = stack, Margin = new Thickness(5), Padding = new Thickness(12, 8, 12, 8) };
    }

    private Button NavButton(string text, string tip, Action click)
    {
        var b = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = double.NaN, Height = 26, ToolTip = tip, Margin = new Thickness(2, 0, 0, 0),
            Content = new TextBlock { Text = text, Style = (Style)FindResource("Style.Text"), FontSize = 13, FontWeight = FontWeights.Medium, Margin = new Thickness(text.Length > 1 ? 10 : 8, 0, text.Length > 1 ? 10 : 8, 2) },
        };
        System.Windows.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    private UIElement Cell(DateTime day, Dictionary<int, List<string>> dots)
    {
        var inMonth = day.Month == _month.Month;
        var isToday = day.Date == DateTime.Today;
        var isSelected = day.Date == _selected.Date;

        var number = T(day.Day.ToString(CultureInfo.InvariantCulture), "Style.Text", 12, isToday || isSelected ? FontWeights.SemiBold : FontWeights.Normal);
        number.HorizontalAlignment = HorizontalAlignment.Center;
        number.Foreground = isSelected ? Brushes.Black : isToday ? (Brush)FindResource("Brush.Accent") : inMonth ? Brushes.White : (Brush)FindResource("Brush.TextFaint");

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(number);
        var dotRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Height = 5, Margin = new Thickness(0, 1, 0, 0) };
        if (inMonth && dots.TryGetValue(day.Day, out var colors))
            foreach (var c in colors)
                dotRow.Children.Add(new System.Windows.Shapes.Ellipse { Width = 4, Height = 4, Margin = new Thickness(1, 0, 1, 0), Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSelected ? "#000000" : c)) });
        stack.Children.Add(dotRow);

        var cell = new Border
        {
            Child = stack, CornerRadius = new CornerRadius(9), Margin = new Thickness(1), Cursor = Cursors.Hand,
            Background = isSelected ? Brushes.White : Brushes.Transparent,
            BorderThickness = new Thickness(1), BorderBrush = isToday && !isSelected ? (Brush)FindResource("Brush.Accent") : Brushes.Transparent,
        };
        var date = day;
        cell.MouseLeftButtonUp += (_, _) => { _selected = date; _editing = null; if (date.Month != _month.Month) _month = new DateTime(date.Year, date.Month, 1); Render(); };
        if (!isSelected) { cell.MouseEnter += (_, _) => cell.Background = (Brush)FindResource("Brush.Control"); cell.MouseLeave += (_, _) => cell.Background = Brushes.Transparent; }
        return cell;
    }

    // ---- agenda -----------------------------------------------------------------------------

    private UIElement Agenda()
    {
        var head = new Grid { Height = 34 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(T(_selected.ToString("dddd, MMM d", CultureInfo.InvariantCulture), "Style.Text", 16, FontWeights.SemiBold));
        head.Children.Add(title);
        var add = new Button
        {
            Style = (Style)FindResource("Style.RoundButton"), Width = double.NaN, Height = 30, Background = (Brush)FindResource("Brush.Primary"),
            Content = new TextBlock { Text = "+ Add event", Style = (Style)FindResource("Style.Text"), FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(14, 0, 14, 0) },
        };
        System.Windows.Automation.AutomationProperties.SetName(add, "Add event");
        add.Click += (_, _) => StartNew();
        Grid.SetColumn(add, 1); head.Children.Add(add);

        var list = new StackPanel();
        var events = Service.Store.EventsOn(_selected);
        if (events.Count == 0) list.Children.Add(T("Nothing planned for this day.", "Style.TextDim", 12));
        foreach (var o in events) list.Children.Add(AgendaRow(o));

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(head);
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetRow(scroll, 1); grid.Children.Add(scroll);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = grid, Margin = new Thickness(5) };
    }

    private UIElement AgendaRow(Occurrence o)
    {
        var e = o.Event;
        var bar = new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(e.Color)), Margin = new Thickness(0, 2, 10, 2) };
        var when = e.AllDay
            ? (o.End.Date > o.Start.Date ? $"All day · until {o.End:MMM d}" : "All day")
            : o.End.Date > o.Start.Date ? $"{Time(o.Start)} – {o.End:MMM d} {Time(o.End)}" : $"{Time(o.Start)} – {Time(o.End)}";
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(T(e.Title, "Style.Text", 13, FontWeights.Medium));
        var sub = T(when + (e.Repeat != Repeat.None ? "  ·  repeats " + e.Repeat.ToString().ToLowerInvariant() : ""), "Style.TextDim", 11);
        text.Children.Add(sub);
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3), Cursor = Cursors.Hand, Background = Brushes.Transparent };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(bar);
        Grid.SetColumn(text, 1); row.Children.Add(text);
        var id = e.Id;
        row.MouseLeftButtonUp += (_, _) => { _editing = Service.Store.All.FirstOrDefault(x => x.Id == id); _isNew = false; Render(); };
        return new Border { Child = row, BorderBrush = (Brush)FindResource("Brush.Divider"), BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    // ---- editor -----------------------------------------------------------------------------

    private void StartNew()
    {
        var start = _selected.Date.AddHours(DateTime.Now.Hour + 1 > 23 ? 23 : DateTime.Now.Hour + 1);
        if (_selected.Date != DateTime.Today) start = _selected.Date.AddHours(9);
        _editing = new CalendarEvent { Start = start, End = start.AddHours(1), ReminderMinutes = 15 };
        _isNew = true;
        Render();
    }

    private TextBox Field(string text, double width = double.NaN)
    {
        var tb = new TextBox
        {
            Text = text, Background = (Brush)FindResource("Brush.Control"), Foreground = Brushes.White, CaretBrush = Brushes.White, BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent, Padding = new Thickness(6, 5, 6, 5), FontSize = 12, FontFamily = (FontFamily)FindResource("Font.Ui"), Width = width,
        };
        return tb;
    }

    private UIElement Labeled(string label, UIElement field)
    {
        var s = new StackPanel { Margin = new Thickness(0, 0, 6, 6) };
        s.Children.Add(T(label, "Style.TextDim", 10));
        s.Children.Add(field);
        return s;
    }

    private Border Chip(string text, bool selected, Action click)
    {
        var chip = new Border
        {
            CornerRadius = new CornerRadius(12), Padding = new Thickness(9, 3, 9, 3), Margin = new Thickness(0, 0, 4, 4), Cursor = Cursors.Hand,
            Background = (Brush)FindResource("Brush.Control"), BorderThickness = new Thickness(1), BorderBrush = selected ? (Brush)FindResource("Brush.Accent") : Brushes.Transparent,
            Child = T(text, "Style.Text", 11, FontWeights.Medium),
        };
        chip.MouseLeftButtonUp += (_, _) => click();
        return chip;
    }

    private UIElement Editor()
    {
        var e = _editing!;
        var title = Field(e.Title);
        title.ToolTip = "Title";
        var date = Field(e.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var start = Field(e.AllDay ? "" : e.Start.ToString("HH:mm", CultureInfo.InvariantCulture));
        var endDate = Field(e.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var end = Field(e.AllDay ? "" : e.End.ToString("HH:mm", CultureInfo.InvariantCulture));
        var note = Field(e.Note);
        var until = Field(e.RepeatUntil?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
        var allDay = new CheckBox { Style = (Style)FindResource("Style.Toggle"), IsChecked = e.AllDay, HorizontalAlignment = HorizontalAlignment.Left };
        var error = T("", "Style.TextDim", 11);
        error.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x6C, 0x4D));
        System.Windows.Automation.AutomationProperties.SetName(title, "Event title");

        void Mark(TextBox box, bool bad) => box.BorderBrush = bad ? new SolidColorBrush(Color.FromRgb(0xEF, 0x6C, 0x4D)) : Brushes.Transparent;

        void Save()
        {
            error.Text = "";
            foreach (var b in new[] { title, date, start, endDate, end, until }) Mark(b, false);
            var name = title.Text.Trim();
            if (name.Length == 0) { Mark(title, true); error.Text = "Give the event a title."; return; }
            if (!DateTime.TryParse(date.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) { Mark(date, true); error.Text = "Date should look like 2026-11-03."; return; }
            if (!DateTime.TryParse(endDate.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2)) { Mark(endDate, true); error.Text = "End date should look like 2026-11-03."; return; }
            DateTime s, en;
            if (allDay.IsChecked == true) { s = d.Date; en = d2.Date; }
            else
            {
                if (TimeInput.TryParse(start.Text) is not { } st) { Mark(start, true); error.Text = "Start time, for example 9:30 or 2pm."; return; }
                if (TimeInput.TryParse(end.Text) is not { } et) { Mark(end, true); error.Text = "End time, for example 10:30 or 3pm."; return; }
                s = d.Date + st; en = d2.Date + et;
            }
            if (en < s) { Mark(endDate, true); Mark(end, true); error.Text = "The event cannot end before it starts."; return; }
            DateTime? until2 = null;
            if (until.Text.Trim().Length > 0)
            {
                if (!DateTime.TryParse(until.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var u)) { Mark(until, true); error.Text = "Repeat-until date should look like 2026-12-31."; return; }
                until2 = u.Date;
            }
            e.Title = name; e.Start = s; e.End = en; e.AllDay = allDay.IsChecked == true; e.Note = note.Text.Trim(); e.RepeatUntil = e.Repeat == Repeat.None ? null : until2;
            if (_isNew) Service.Add(e);
            else Service.Update(e.Id, x => { x.Title = e.Title; x.Start = e.Start; x.End = e.End; x.AllDay = e.AllDay; x.Note = e.Note; x.Color = e.Color; x.Repeat = e.Repeat; x.RepeatUntil = e.RepeatUntil; x.ReminderMinutes = e.ReminderMinutes; });
            _selected = s.Date; _month = new DateTime(s.Year, s.Month, 1);
            _editing = null;
            Render();
        }

        var left = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        left.Children.Add(Labeled("Title", title));
        var two = new UniformGrid { Columns = 2 };
        two.Children.Add(Labeled("Date", date)); two.Children.Add(Labeled("Starts", start));
        two.Children.Add(Labeled("Ends on", endDate)); two.Children.Add(Labeled("Ends at", end));
        left.Children.Add(two);
        var allRow = new StackPanel { Orientation = Orientation.Horizontal };
        allRow.Children.Add(allDay); allRow.Children.Add(T("All day", "Style.TextDim", 12));
        ((TextBlock)allRow.Children[1]).Margin = new Thickness(8, 0, 0, 0); ((TextBlock)allRow.Children[1]).VerticalAlignment = VerticalAlignment.Center;
        left.Children.Add(allRow);
        left.Children.Add(Labeled("Note", note));

        var swatches = new WrapPanel();
        void Colors()
        {
            swatches.Children.Clear();
            foreach (var hex in ColorChoices)
            {
                var c = hex;
                var dot = new Border
                {
                    Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 6, 4), Cursor = Cursors.Hand,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), BorderThickness = new Thickness(2),
                    BorderBrush = string.Equals(e.Color, hex, StringComparison.OrdinalIgnoreCase) ? Brushes.White : Brushes.Transparent,
                };
                dot.MouseLeftButtonUp += (_, _) => { e.Color = c; Colors(); };
                swatches.Children.Add(dot);
            }
        }
        Colors();
        var repeatChips = new WrapPanel();
        var untilBox = Labeled("Repeat until (optional)", until);
        void Repeats()
        {
            repeatChips.Children.Clear();
            foreach (var r in Enum.GetValues<Repeat>())
            {
                var rep = r;
                repeatChips.Children.Add(Chip(r.ToString(), e.Repeat == r, () => { e.Repeat = rep; Repeats(); }));
            }
            untilBox.Visibility = e.Repeat == Repeat.None ? Visibility.Collapsed : Visibility.Visible;
        }
        Repeats();
        var reminderChips = new WrapPanel();
        void RemindersUi()
        {
            reminderChips.Children.Clear();
            foreach (var (label, minutes) in Reminders)
            {
                var m = minutes;
                reminderChips.Children.Add(Chip(label, e.ReminderMinutes == minutes, () => { e.ReminderMinutes = m; RemindersUi(); }));
            }
        }
        RemindersUi();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        Button B(string label, Brush bg, Action click, string name)
        {
            var b = new Button
            {
                Style = (Style)FindResource("Style.RoundButton"), Width = double.NaN, Height = 30, Background = bg, Margin = new Thickness(0, 0, 6, 0),
                Content = new TextBlock { Text = label, Style = (Style)FindResource("Style.Text"), FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(14, 0, 14, 0) },
            };
            System.Windows.Automation.AutomationProperties.SetName(b, name);
            b.Click += (_, _) => click();
            return b;
        }
        buttons.Children.Add(B("Save", (Brush)FindResource("Brush.Primary"), Save, "Save event"));
        buttons.Children.Add(B("Cancel", (Brush)FindResource("Brush.Control"), () => { _editing = null; Render(); }, "Cancel"));
        if (!_isNew) buttons.Children.Add(B("Delete", (Brush)FindResource("Brush.Control"), () => { Service.Delete(e.Id); _editing = null; Render(); }, "Delete event"));

        var right = new StackPanel();
        right.Children.Add(Labeled("Colour", swatches));
        right.Children.Add(Labeled("Repeat", repeatChips));
        right.Children.Add(untilBox);
        right.Children.Add(Labeled("Remind me", reminderChips));
        right.Children.Add(error);
        right.Children.Add(buttons);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(left);
        Grid.SetColumn(right, 1); grid.Children.Add(right);
        return new Border { Style = (Style)FindResource("Style.Card"), Child = new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden }, Margin = new Thickness(5), Padding = new Thickness(12, 8, 12, 4) };
    }
}
