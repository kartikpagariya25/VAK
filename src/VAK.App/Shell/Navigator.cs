namespace VAK.App.Shell;

/// <summary>Tracks which page is showing. "home" and "settings" are built in; every other id is a registered widget.</summary>
public sealed class Navigator
{
    public const string Home = "home";

    public string CurrentId { get; private set; } = Home;
    public event Action<string>? Navigated;

    public void GoTo(string widgetId)
    {
        if (widgetId == CurrentId) return;
        CurrentId = widgetId;
        Navigated?.Invoke(widgetId);
    }
}
