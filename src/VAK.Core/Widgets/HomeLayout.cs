namespace VAK.Core.Widgets;

/// <summary>The six Home grid slots (3 columns x 2 rows) and their edit history.</summary>
public class HomeLayout
{
    public const int SlotCount = 6;
    private readonly Stack<string?[]> _history = new();

    public HomeLayout(string?[] slots)
    {
        Slots = new string?[SlotCount];
        for (var i = 0; i < Math.Min(slots.Length, SlotCount); i++) Slots[i] = slots[i];
    }

    public string?[] Slots { get; private set; }
    public bool CanUndo => _history.Count > 0;

    public static HomeLayout Default() => new(new string?[] { "system", "timers", "clock", null, null, null });

    /// <summary>Puts a widget in a slot (null empties it). A widget lives in at most one slot, so assigning it elsewhere moves it.</summary>
    public void Assign(int slot, string? widgetId)
    {
        if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
        _history.Push((string?[])Slots.Clone());
        if (widgetId is not null)
            for (var i = 0; i < SlotCount; i++)
                if (Slots[i] == widgetId) Slots[i] = null;
        Slots[slot] = widgetId;
    }

    public void Undo()
    {
        if (_history.Count > 0) Slots = _history.Pop();
    }
}
