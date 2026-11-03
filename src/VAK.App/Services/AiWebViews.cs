using System.IO;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace VAK.App.Services;

public record AiProvider(string Id, string Name, string Url);

/// <summary>Keeps one embedded browser per AI provider alive for the whole run, so switching pages does not reload the site.
/// Sign-in happens on the real claude.ai / chatgpt.com page; the session cookies live in VAK's own profile folder
/// (%AppData%\VAK\webview) and VAK never sees the password.</summary>
public sealed class AiWebViews
{
    public static readonly IReadOnlyList<AiProvider> Providers = new[]
    {
        new AiProvider("claude", "Claude", "https://claude.ai/new"),
        new AiProvider("gpt", "GPT", "https://chatgpt.com/"),
    };

    private readonly string _profileDir;
    private Task<CoreWebView2Environment>? _environment;
    private readonly Dictionary<string, WebView2CompositionControl> _views = new();
    private readonly Dictionary<string, Task> _starting = new();

    public AiWebViews(string dataDir) => _profileDir = Path.Combine(dataDir, "webview");

    public static AiProvider Provider(string id) => Providers.FirstOrDefault(p => p.Id == id) ?? Providers[0];

    /// <summary>True when the Microsoft Edge WebView2 runtime is installed (it ships with Windows 11).</summary>
    public static bool RuntimeAvailable()
    {
        try { return CoreWebView2Environment.GetAvailableBrowserVersionString() is { Length: > 0 }; }
        catch (Exception e) when (e is WebView2RuntimeNotFoundException or InvalidOperationException) { return false; }
    }

    /// <summary>The browser for a provider. It starts loading in the background the first time it is asked for.</summary>
    public WebView2CompositionControl Get(string providerId, Action<string>? onProblem = null)
    {
        if (_views.TryGetValue(providerId, out var existing)) return existing;
        var view = new WebView2CompositionControl();
        _views[providerId] = view;
        _starting[providerId] = StartAsync(view, Provider(providerId), onProblem);
        return view;
    }

    private async Task StartAsync(WebView2CompositionControl view, AiProvider provider, Action<string>? onProblem)
    {
        try
        {
            _environment ??= CoreWebView2Environment.CreateAsync(null, _profileDir);
            var env = await _environment.ConfigureAwait(true);
            await view.EnsureCoreWebView2Async(env).ConfigureAwait(true);
            var settings = view.CoreWebView2.Settings;
            settings.AreDevToolsEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
            view.CoreWebView2.Navigate(provider.Url);
        }
        catch (Exception e) when (e is WebView2RuntimeNotFoundException or InvalidOperationException or System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException)
        {
            onProblem?.Invoke(e is WebView2RuntimeNotFoundException
                ? "The Microsoft Edge WebView2 runtime is not installed."
                : "The embedded browser could not start: " + e.Message);
            _views.Remove(provider.Id);
        }
    }

    /// <summary>Takes a browser out of whatever page holds it, so another page can show it.</summary>
    public static void Detach(WebView2CompositionControl view)
    {
        if (view.Parent is Panel panel) panel.Children.Remove(view);
    }
}
