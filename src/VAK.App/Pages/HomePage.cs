using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VAK.App.Theme;
using VAK.App.Widgets;
using VAK.Core.Widgets;

namespace VAK.App.Pages;

/// <summary>The Home grid: six card slots, with an edit mode to add, remove and undo.</summary>
public sealed class HomePage : UserControl
{
    private readonly AppHost _host;
    private readonly HomeLayout _layout;
    private readonly UniformGrid _grid = new() { Columns = 3, Rows = 2 };
    private bool _editing;

    public event Action? StateChanged;

    public HomePage(AppHost host)
    {
        _host = host;
        var s = host.Settings;
        if (!s.HomeInitialized)
        {
            s.HomeSlots = HomeLayout.Default().Slots;
            s.HomeInitialized = true;
            host.Save();
        }
        _layout = new HomeLayout(s.HomeSlots);
        Content = _grid;
        Rebuild();
    }

    public bool Editing
    {
        get => _editing;
        set { _editing = value; Rebuild(); StateChanged?.Invoke(); }
    }

    public bool CanUndo => _layout.CanUndo;

    public void Undo()
    {
        _layout.Undo();
        Commit();
    }

    private void Commit()
    {
        _host.Settings.HomeSlots = _layout.Slots;
        _host.Save();
        Rebuild();
        StateChanged?.Invoke();
    }

    private void Rebuild()
    {
        _grid.Children.Clear();
        var allEmpty = _layout.Slots.All(id => id is null);
        for (var i = 0; i < HomeLayout.SlotCount; i++)
        {
            var id = _layout.Slots[i];
            var cell = new Grid { Margin = new Thickness(5) };
            if (id is not null && _host.Modules.TryGetValue(id, out var module))
                cell.Children.Add(BuildCard(i, module));
            else if (_editing)
                cell.Children.Add(BuildEmptySlot(i));
            else if (allEmpty && i == 1)
                cell.Children.Add(Hint());
            _grid.Children.Add(cell);
        }
    }

    private UIElement BuildCard(int slot, WidgetModule module)
    {
        var card = new Border { Style = (Style)FindResource("Style.Card"), Child = module.CreateCard(), Cursor = Cursors.Hand };
        var root = new Grid();
        root.Children.Add(card);
        if (_editing)
        {
            var remove = new Button
            {
                Style = (Style)FindResource("Style.RoundButton"), Width = 26, Height = 26, ToolTip = "Remove from Home",
                Background = (Brush)FindResource("Brush.Control"), HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 8, 0),
                Content = new TextBlock { Text = Icons.Glyph("minus"), Style = (Style)FindResource("Style.Icon"), FontSize = 14 },
            };
            remove.Click += (_, _) => { _layout.Assign(slot, null); Commit(); };
            root.Children.Add(remove);
        }
        else
        {
            card.MouseLeftButtonUp += (_, _) => _host.Nav.GoTo(module.Info.Id);
        }
        return root;
    }

    private UIElement BuildEmptySlot(int slot)
    {
        var slotCard = new Border
        {
            Style = (Style)FindResource("Style.Card"), Cursor = Cursors.Hand, Background = Brushes.Transparent,
            BorderBrush = (Brush)FindResource("Brush.Control"), BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = "+ Add widget", Style = (Style)FindResource("Style.TextDim"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        slotCard.MouseLeftButtonUp += (_, _) => ShowPicker(slotCard, slot);
        return slotCard;
    }

    private void ShowPicker(FrameworkElement anchor, int slot)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
        var available = _host.Registry.All.Where(w => w.HasHomeCard && !_layout.Slots.Contains(w.Id)).ToList();
        if (available.Count == 0)
            menu.Items.Add(new MenuItem { Header = "All widgets are on Home", IsEnabled = false });
        foreach (var w in available)
        {
            var item = new MenuItem { Header = w.Title };
            var id = w.Id;
            item.Click += (_, _) => { _layout.Assign(slot, id); Commit(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private UIElement Hint()
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock { Text = "Nothing on Home yet", Style = (Style)FindResource("Style.Text"), FontSize = 15, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = "Choose Edit to add a widget to Home.", Style = (Style)FindResource("Style.TextDim"), HorizontalAlignment = HorizontalAlignment.Center });
        return stack;
    }
}
