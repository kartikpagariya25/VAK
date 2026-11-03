namespace VAK.Core.Widgets;

public record WidgetInfo(string Id, string Title, string Subtitle, string IconKey, string[] Keywords, bool HasHomeCard);

public class WidgetRegistry
{
    private readonly List<WidgetInfo> _widgets = new();

    public IReadOnlyList<WidgetInfo> All => _widgets;

    public void Register(WidgetInfo widget)
    {
        if (_widgets.Any(w => w.Id == widget.Id))
            throw new InvalidOperationException($"Widget '{widget.Id}' is already registered.");
        _widgets.Add(widget);
    }

    public WidgetInfo? Find(string id) => _widgets.FirstOrDefault(w => w.Id == id);

    public IEnumerable<WidgetInfo> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _widgets;
        return _widgets.Where(w =>
            w.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            w.Keywords.Any(k => k.Contains(query, StringComparison.OrdinalIgnoreCase)));
    }
}
