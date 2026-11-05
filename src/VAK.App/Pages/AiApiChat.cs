using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using VAK.Core.Chat;

namespace VAK.App.Pages;

/// <summary>Native chat with Claude or GPT using the user's own API key: saved conversations, streaming replies,
/// attachments (drop, paste or pick) and markdown with copyable code blocks.</summary>
public sealed class AiApiChat : UserControl
{
    private readonly AppHost _host;
    private readonly string _provider;
    private readonly string _name;
    private Conversation _conv;
    private readonly StackPanel _list = new();
    private readonly StackPanel _messages = new() { Margin = new Thickness(0, 0, 6, 0) };
    private readonly ScrollViewer _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
    private readonly Grid _center = new();
    private readonly TextBox _input = new();
    private readonly TextBox _model = new();
    private readonly WrapPanel _chips = new() { Margin = new Thickness(0, 0, 0, 4) };
    private readonly TextBlock _status = new();
    private readonly Button _send = new();
    private readonly TextBlock _sendLabel = new();
    private readonly List<ChatAttachment> _pending = new();
    private readonly TextBlock _keyState = new();
    private CancellationTokenSource? _cts;

    public AiApiChat(AppHost host, string provider)
    {
        _host = host;
        _provider = provider;
        _name = provider == "gpt" ? "GPT" : "Claude";
        _conv = _host.Chats.All().FirstOrDefault(c => c.Provider == provider) ?? NewConversation();
        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] files) AddFiles(files); };
        Unloaded += (_, _) => _cts?.Cancel();
        Loaded += (_, _) => { if (Content is null) BuildLayout(); RefreshAll(); }; // Loaded can fire again if the page is re-attached; build the layout once
    }

    private Brush B(string key) => (Brush)FindResource(key);
    private Style S(string key) => (Style)FindResource(key);

    private Conversation NewConversation() => new() { Provider = _provider, Model = SavedModel() };

    private string SavedModel()
    {
        var models = _host.Settings.Ai.Models ??= new();
        return models.TryGetValue(_provider, out var m) && m.Length > 0 ? m : ChatDefaults.DefaultModel(_provider);
    }

    private TextBlock T(string text, string style = "Style.Text", double size = 13, FontWeight? weight = null)
    {
        var t = new TextBlock { Text = text, Style = S(style), FontSize = size, TextWrapping = TextWrapping.Wrap };
        if (weight is { } w) t.FontWeight = w;
        return t;
    }

    private Button Pill(string text, double width, Brush background, Action click, double height = 30)
    {
        var b = new Button { Style = S("Style.RoundButton"), Width = width, Height = height, Background = background, Content = T(text, size: 12, weight: FontWeights.Medium) };
        ((TextBlock)b.Content).TextWrapping = TextWrapping.NoWrap;
        b.Click += (_, _) => click();
        return b;
    }

    // ---------- layout ----------
    private void BuildLayout()
    {
        var root = new Grid { Margin = new Thickness(6, 0, 6, 6) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // conversations
        var left = new Grid { Margin = new Thickness(0, 0, 8, 0) };
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var fresh = Pill("+ New chat", 182, B("Brush.Primary"), StartNew, 32);
        fresh.Margin = new Thickness(0, 0, 0, 8);
        left.Children.Add(fresh);
        var listScroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        Grid.SetRow(listScroll, 1); left.Children.Add(listScroll);
        root.Children.Add(new Border { Style = S("Style.Card"), Child = left, Padding = new Thickness(8) });

        // chat column
        var right = new Grid { Margin = new Thickness(0) };
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        right.Children.Add(BuildTopBar());
        _scroll.Content = _messages;
        _center.Children.Add(_scroll);
        Grid.SetRow(_center, 1); right.Children.Add(_center);
        var bottom = BuildInput();
        Grid.SetRow(bottom, 2); right.Children.Add(bottom);

        var rightCard = new Border { Style = S("Style.Card"), Child = right, Padding = new Thickness(10, 8, 10, 8) };
        Grid.SetColumn(rightCard, 1); root.Children.Add(rightCard);
        Content = root;
    }

    private UIElement BuildTopBar()
    {
        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        _keyState.Style = S("Style.TextDim"); _keyState.FontSize = 12; _keyState.VerticalAlignment = VerticalAlignment.Center;
        var key = Pill("API key", 74, B("Brush.Control"), OpenKeyMenu);
        key.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(key, Dock.Right); bar.Children.Add(key);
        DockPanel.SetDock(_keyState, Dock.Right); bar.Children.Add(_keyState);

        _model.Background = Brushes.Transparent; _model.Foreground = Brushes.White; _model.CaretBrush = Brushes.White; _model.BorderThickness = new Thickness(0);
        _model.Padding = new Thickness(10, 5, 10, 5); _model.FontSize = 12; _model.Width = 200; _model.ToolTip = "Model (type any model id)";
        _model.FontFamily = (FontFamily)FindResource("Font.Ui");
        _model.LostFocus += (_, _) => CommitModel();
        _model.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitModel(); _input.Focus(); } };
        var pick = Pill("▾", 28, B("Brush.Control"), OpenModelMenu);
        pick.Margin = new Thickness(4, 0, 0, 0);
        var modelRow = new StackPanel { Orientation = Orientation.Horizontal };
        modelRow.Children.Add(new Border { CornerRadius = new CornerRadius(15), Background = B("Brush.Control"), Child = _model });
        modelRow.Children.Add(pick);
        bar.Children.Add(modelRow);
        return bar;
    }

    private UIElement BuildInput()
    {
        var box = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        _status.Style = S("Style.TextDim"); _status.FontSize = 11; _status.TextWrapping = TextWrapping.Wrap; _status.Visibility = Visibility.Collapsed;
        box.Children.Add(_status);
        box.Children.Add(_chips);

        _input.Background = Brushes.Transparent; _input.Foreground = Brushes.White; _input.CaretBrush = Brushes.White; _input.BorderThickness = new Thickness(0);
        _input.AcceptsReturn = true; _input.TextWrapping = TextWrapping.Wrap; _input.MaxHeight = 84; _input.MinHeight = 24; _input.FontSize = 13;
        _input.FontFamily = (FontFamily)FindResource("Font.Ui"); _input.VerticalContentAlignment = VerticalAlignment.Center;
        _input.ToolTip = "Enter to send, Shift+Enter for a new line. Drop or paste images and files here.";
        _input.PreviewKeyDown += InputKey;

        var attach = Pill("+", 32, B("Brush.Control"), PickFiles, 32);
        attach.ToolTip = "Attach images or text files";
        _sendLabel.Style = S("Style.Text"); _sendLabel.FontWeight = FontWeights.SemiBold; _sendLabel.FontSize = 12;
        _send.Style = S("Style.RoundButton"); _send.Width = 64; _send.Height = 32; _send.Background = B("Brush.Primary"); _send.Content = _sendLabel;
        _send.Click += (_, _) => { if (_cts is null) _ = SendAsync(); else _cts.Cancel(); };
        SetBusy(false);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(attach);
        var field = new Border { CornerRadius = new CornerRadius(16), Background = B("Brush.Control"), Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(6, 0, 6, 0), Child = _input };
        Grid.SetColumn(field, 1); row.Children.Add(field);
        Grid.SetColumn(_send, 2); row.Children.Add(_send);
        box.Children.Add(row);
        return box;
    }

    // ---------- state ----------
    private void RefreshAll()
    {
        _model.Text = _conv.Model.Length > 0 ? _conv.Model : SavedModel();
        RefreshList();
        RefreshKeyState();
        RenderConversation();
    }

    private bool HasKey => _host.ApiKeys.Has(_provider);

    private void RefreshKeyState()
    {
        _keyState.Text = HasKey ? "Key saved on this PC" : "No key yet";
        _input.IsEnabled = HasKey;
        _send.IsEnabled = HasKey;
    }

    private void RefreshList()
    {
        _list.Children.Clear();
        foreach (var c in _host.Chats.All().Where(c => c.Provider == _provider))
        {
            var conv = c;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = T(conv.Title, size: 12); title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextWrapping = TextWrapping.NoWrap;
            var open = new Button { Style = S("Style.RoundButton"), Height = 28, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 0, 6, 0), Content = title,
                Background = conv.Id == _conv.Id ? B("Brush.Control") : Brushes.Transparent };
            open.Click += (_, _) => Open(conv);
            row.Children.Add(open);
            var del = Pill("✕", 26, Brushes.Transparent, () => Delete(conv), 28);
            del.ToolTip = "Delete this chat";
            Grid.SetColumn(del, 1); row.Children.Add(del);
            _list.Children.Add(row);
        }
    }

    private void Open(Conversation conv)
    {
        if (_cts is not null) return;
        _conv = conv; _pending.Clear(); RenderChips(); SetStatus(null);
        RefreshAll();
    }

    private void StartNew()
    {
        if (_cts is not null) return;
        _conv = NewConversation(); _pending.Clear(); RenderChips(); SetStatus(null);
        RefreshAll();
        _input.Focus();
    }

    private void Delete(Conversation conv)
    {
        if (_cts is not null) return;
        _host.Chats.Delete(conv.Id);
        if (conv.Id == _conv.Id) _conv = _host.Chats.All().FirstOrDefault(c => c.Provider == _provider) ?? NewConversation();
        RefreshAll();
    }

    private void CommitModel()
    {
        var text = _model.Text.Trim();
        if (text.Length == 0) text = ChatDefaults.DefaultModel(_provider);
        _model.Text = text;
        _conv.Model = text;
        (_host.Settings.Ai.Models ??= new())[_provider] = text;
        _host.Save();
    }

    private void OpenModelMenu()
    {
        var menu = new ContextMenu();
        foreach (var m in ChatDefaults.Models(_provider))
        {
            var item = new MenuItem { Header = m };
            var id = m;
            item.Click += (_, _) => { _model.Text = id; CommitModel(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void OpenKeyMenu()
    {
        var menu = new ContextMenu();
        var set = new MenuItem { Header = HasKey ? "Replace key…" : "Add key…" };
        set.Click += (_, _) => ShowKeyEntry();
        menu.Items.Add(set);
        if (HasKey)
        {
            var remove = new MenuItem { Header = "Remove key from this PC" };
            remove.Click += (_, _) => { _host.ApiKeys.Remove(_provider); RefreshKeyState(); RenderConversation(); };
            menu.Items.Add(remove);
        }
        menu.IsOpen = true;
    }

    // ---------- key entry ----------
    private void ShowKeyEntry()
    {
        var where = _provider == "gpt" ? "platform.openai.com/api-keys" : "console.anthropic.com/settings/keys";
        var password = new PasswordBox { Background = Brushes.Transparent, Foreground = Brushes.White, CaretBrush = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(12, 8, 12, 8), Width = 320, FontSize = 13 };
        var save = Pill("Save key", 90, B("Brush.Primary"), () => { }, 34);
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Width = 360 };
        panel.Children.Add(T($"Use {_name} with your own API key", size: 15, weight: FontWeights.SemiBold));
        var help = T($"Create a key at {where}. VAK encrypts it with Windows so only your user account can read it, and sends it only to {(_provider == "gpt" ? "api.openai.com" : "api.anthropic.com")}. API use is billed by the provider, separately from a claude.ai or ChatGPT subscription.", "Style.TextDim", 12);
        help.Margin = new Thickness(0, 6, 0, 10);
        panel.Children.Add(help);
        panel.Children.Add(new Border { CornerRadius = new CornerRadius(17), Background = B("Brush.Control"), Child = password, Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left });
        panel.Children.Add(save);
        void Commit()
        {
            if (password.Password.Trim().Length == 0) return;
            _host.ApiKeys.Set(_provider, password.Password);
            RefreshKeyState(); RenderConversation(); _input.Focus();
        }
        save.Click += (_, _) => Commit();
        password.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        _center.Children.Clear();
        _center.Children.Add(panel);
        password.Focus();
    }

    // ---------- attachments ----------
    private void PickFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "Attach images or text files", Filter = "Images and text|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.txt;*.md;*.json;*.csv;*.xml;*.yaml;*.yml;*.log;*.cs;*.js;*.ts;*.py;*.html;*.css;*.sql|All files|*.*" };
        if (dialog.ShowDialog() == true) AddFiles(dialog.FileNames);
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        var problems = new List<string>();
        foreach (var path in paths)
        {
            var result = AttachmentReader.Read(path);
            if (result.Attachment is { } a) _pending.Add(a); else problems.Add(result.Problem!);
        }
        RenderChips();
        SetStatus(problems.Count == 0 ? null : string.Join("  ", problems));
    }

    private void RenderChips()
    {
        _chips.Children.Clear();
        foreach (var a in _pending.ToList())
        {
            var attachment = a;
            var chip = new Border { CornerRadius = new CornerRadius(12), Background = B("Brush.Control"), Padding = new Thickness(10, 3, 4, 3), Margin = new Thickness(0, 0, 6, 4) };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(T(attachment.Name, size: 11));
            var remove = new Button { Style = S("Style.RoundButton"), Width = 20, Height = 20, Background = Brushes.Transparent, Content = T("✕", size: 10), Margin = new Thickness(4, 0, 0, 0) };
            remove.Click += (_, _) => { _pending.Remove(attachment); RenderChips(); };
            row.Children.Add(remove);
            chip.Child = row;
            _chips.Children.Add(chip);
        }
    }

    private void InputKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            if (_cts is null) _ = SendAsync();
        }
        else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && TryPasteImage()) e.Handled = true;
    }

    private bool TryPasteImage()
    {
        try
        {
            if (!Clipboard.ContainsImage() || Clipboard.GetImage() is not { } image) return false;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            if (stream.Length > AttachmentReader.MaxImageBytes) { SetStatus("The pasted image is larger than 5 MB."); return true; }
            _pending.Add(new ChatAttachment { Kind = "image", Name = $"pasted-{DateTime.Now:HHmmss}.png", MediaType = "image/png", Data = Convert.ToBase64String(stream.ToArray()) });
            RenderChips();
            return true;
        }
        catch (System.Runtime.InteropServices.ExternalException) { return false; }
    }

    // ---------- sending ----------
    private void SetBusy(bool busy) => _sendLabel.Text = busy ? "Stop" : "Send";

    private void SetStatus(string? text)
    {
        _status.Text = text ?? "";
        _status.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task SendAsync()
    {
        var text = _input.Text.Trim();
        if ((text.Length == 0 && _pending.Count == 0) || _host.ApiKeys.Get(_provider) is not { } key) return;
        CommitModel();

        var user = new ChatMessage { Role = "user", Text = text };
        user.Attachments.AddRange(_pending);
        _conv.Messages.Add(user);
        if (_conv.Messages.Count == 1) _conv.Title = Conversation.TitleFrom(text.Length > 0 ? text : user.Attachments[0].Name);
        _conv.Updated = DateTime.Now;
        _host.Chats.Save(_conv);
        _input.Clear(); _pending.Clear(); RenderChips(); SetStatus(null);
        RefreshList();
        RenderConversation();

        var reply = new ChatMessage { Role = "assistant" };
        var bubble = new StackPanel();
        _messages.Children.Add(bubble);
        var cts = _cts = new CancellationTokenSource();
        SetBusy(true);
        var last = DateTime.MinValue;
        try
        {
            await foreach (var piece in _host.ChatApi.StreamAsync(_provider, key, _conv.Model, _conv.Messages, cts.Token))
            {
                reply.Text += piece;
                if ((DateTime.Now - last).TotalMilliseconds > 70) { RenderAssistant(bubble, reply.Text, false); last = DateTime.Now; ScrollToEnd(); }
            }
        }
        catch (OperationCanceledException) { }
        catch (ChatException e) { SetStatus(e.Message); }
        finally
        {
            _cts = null; cts.Dispose(); SetBusy(false);
        }
        if (reply.Text.Length > 0) { _conv.Messages.Add(reply); }
        else if (_conv.Messages.Count > 0 && _conv.Messages[^1] == user) { /* keep the question so it can be retried */ }
        _conv.Updated = DateTime.Now;
        _host.Chats.Save(_conv);
        RenderAssistant(bubble, reply.Text, true);
        ScrollToEnd();
    }

    private void ScrollToEnd() => Dispatcher.BeginInvoke(DispatcherPriority.Background, () => _scroll.ScrollToEnd());

    // ---------- rendering ----------
    private void RenderConversation()
    {
        _center.Children.Clear();
        _center.Children.Add(_scroll);
        _messages.Children.Clear();
        if (!HasKey) { ShowKeyEntry(); return; }
        if (_conv.Messages.Count == 0)
        {
            var hint = T($"Ask {_name} anything. Drop images or text files here to include them.", "Style.TextDim", 13);
            hint.HorizontalAlignment = HorizontalAlignment.Center; hint.Margin = new Thickness(0, 90, 0, 0); hint.TextAlignment = TextAlignment.Center;
            _messages.Children.Add(hint);
            return;
        }
        foreach (var m in _conv.Messages)
        {
            if (m.Role == "user") _messages.Children.Add(UserBubble(m));
            else { var host = new StackPanel(); RenderAssistant(host, m.Text, true); _messages.Children.Add(host); }
        }
        ScrollToEnd();
    }

    private UIElement UserBubble(ChatMessage m)
    {
        var stack = new StackPanel();
        foreach (var a in m.Attachments) stack.Children.Add(T("Attached: " + a.Name, "Style.TextDim", 11));
        if (m.Text.Length > 0) stack.Children.Add(T(m.Text));
        return new Border { Background = B("Brush.Control"), CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(60, 4, 0, 8), HorizontalAlignment = HorizontalAlignment.Right, Child = stack };
    }

    private void RenderAssistant(StackPanel host, string text, bool final)
    {
        host.Children.Clear();
        host.Margin = new Thickness(0, 4, 0, 10);
        if (text.Length == 0) { if (!final) host.Children.Add(T("…", "Style.TextDim")); return; }
        foreach (var block in MarkdownParser.Parse(text)) host.Children.Add(RenderBlock(block));
        if (final)
        {
            var copy = Pill("Copy reply", 84, Brushes.Transparent, () => CopyText(text), 24);
            copy.HorizontalAlignment = HorizontalAlignment.Left;
            host.Children.Add(copy);
        }
    }

    private UIElement RenderBlock(MdBlock block)
    {
        switch (block.Kind)
        {
            case MdKind.Heading:
                return Inlines(block.Text, block.Level <= 1 ? 17 : block.Level == 2 ? 15 : 14, FontWeights.SemiBold, new Thickness(0, 6, 0, 4));
            case MdKind.Bullet:
            {
                var row = new Grid { Margin = new Thickness(8, 1, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.Children.Add(T("•"));
                var body = Inlines(block.Text, 13, FontWeights.Normal, new Thickness(0));
                Grid.SetColumn(body, 1); row.Children.Add(body);
                return row;
            }
            case MdKind.Numbered:
                return Inlines(block.Text, 13, FontWeights.Normal, new Thickness(8, 1, 0, 2));
            case MdKind.Code:
                return CodeBlock(block);
            default:
                return Inlines(block.Text, 13, FontWeights.Normal, new Thickness(0, 2, 0, 4));
        }
    }

    private TextBlock Inlines(string text, double size, FontWeight weight, Thickness margin)
    {
        var tb = new TextBlock { Style = S("Style.Text"), FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap, Margin = margin, LineHeight = size * 1.45 };
        foreach (var run in MarkdownParser.Inline(text))
        {
            var r = new System.Windows.Documents.Run(run.Text);
            if (run.Bold) r.FontWeight = FontWeights.Bold;
            if (run.Italic) r.FontStyle = FontStyles.Italic;
            if (run.Code) { r.FontFamily = new FontFamily("Consolas"); r.Background = B("Brush.Control"); }
            tb.Inlines.Add(r);
        }
        return tb;
    }

    private UIElement CodeBlock(MdBlock block)
    {
        var head = new Grid { Margin = new Thickness(10, 4, 4, 0) };
        head.Children.Add(T(block.Language.Length > 0 ? block.Language : "code", "Style.TextDim", 11));
        var copy = Pill("Copy", 54, Brushes.Transparent, () => CopyText(block.Text), 22);
        copy.HorizontalAlignment = HorizontalAlignment.Right;
        head.Children.Add(copy);
        var code = new TextBlock { Text = block.Text, FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = Brushes.White, Margin = new Thickness(10, 2, 10, 8) };
        var scroll = new ScrollViewer { Content = code, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var stack = new StackPanel();
        stack.Children.Add(head); stack.Children.Add(scroll);
        return new Border { Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0B, 0x0D)), BorderBrush = B("Brush.Divider"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 4, 0, 6), Child = stack };
    }

    private void CopyText(string text)
    {
        try { Clipboard.SetText(text); SetStatus("Copied."); }
        catch (System.Runtime.InteropServices.ExternalException) { SetStatus("The clipboard is busy; try again."); }
    }
}
