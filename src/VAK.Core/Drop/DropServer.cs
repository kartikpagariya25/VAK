using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VAK.Core.Shelf;

namespace VAK.Core.Drop;

public record SharedFile(string Id, string Name, string Path, long Size);
public record ReceivedFile(string Name, string Path, long Size, DateTime Time);

/// <summary>A small web server for moving files between this PC and phones on the same private network.
/// It listens only on this PC's private LAN address, and every request must carry the random token from the QR code.</summary>
public sealed class DropServer : IAsyncDisposable
{
    private const long MaxUploadBytes = 1L << 30; // 1 GB

    private readonly object _gate = new();
    private readonly List<SharedFile> _shared = new();
    private readonly List<ReceivedFile> _received = new();
    private WebApplication? _app;
    private string _token = "";

    public string ReceivedDir { get; }
    public bool Running { get; private set; }
    public string? Url { get; private set; }

    public event Action? Changed;

    /// <summary>Raised when the server stopped itself because nobody used it for <see cref="IdleTimeout"/>.</summary>
    public event Action? AutoStopped;

    /// <summary>How long the server may sit unused before it switches itself off.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(20);

    private DateTime _lastActivity = DateTime.UtcNow;
    private Timer? _idleTimer;

    private readonly Func<IPAddress, bool> _isAllowed;

    /// <param name="isAllowed">Which addresses the server may listen on. The default is private LAN addresses only;
    /// tests pass a rule that allows loopback so no port is opened on the real network.</param>
    public DropServer(string receivedDir, Func<IPAddress, bool>? isAllowed = null)
    {
        ReceivedDir = receivedDir;
        _isAllowed = isAllowed ?? PrivateNetwork.IsPrivate;
    }

    public IReadOnlyList<SharedFile> Shared { get { lock (_gate) return _shared.ToList(); } }
    public IReadOnlyList<ReceivedFile> Received { get { lock (_gate) return _received.OrderByDescending(r => r.Time).ToList(); } }

    public bool AddShared(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return false;
            lock (_gate) _shared.Add(new SharedFile(Guid.NewGuid().ToString("N"), info.Name, info.FullName, info.Length));
            Changed?.Invoke();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    public void RemoveShared(string id)
    {
        lock (_gate) _shared.RemoveAll(s => s.Id == id);
        Changed?.Invoke();
    }

    /// <summary>Starts listening on <paramref name="address"/> (a private LAN address). Returns false if no port could be opened.</summary>
    public async Task<bool> StartAsync(IPAddress address)
    {
        if (Running || !_isAllowed(address)) return false;
        _token = DropToken.New();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var port = Random.Shared.Next(49200, 50000);
            try
            {
                var builder = WebApplication.CreateSlimBuilder();
                builder.Logging.ClearProviders();
                builder.WebHost.ConfigureKestrel(o =>
                {
                    o.Listen(address, port);
                    o.Limits.MaxRequestBodySize = MaxUploadBytes;
                    o.AddServerHeader = false;
                });
                builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxUploadBytes);
                var app = builder.Build();
                MapRoutes(app);
                await app.StartAsync().ConfigureAwait(false);
                _app = app;
                Url = $"http://{address}:{port}/?t={_token}";
                Running = true;
                _lastActivity = DateTime.UtcNow;
                var every = TimeSpan.FromMilliseconds(Math.Clamp(IdleTimeout.TotalMilliseconds / 4, 50, 5000));
                _idleTimer = new Timer(_ => CheckIdle(), null, every, every);
                Changed?.Invoke();
                return true;
            }
            catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException or InvalidOperationException) { /* port taken: try another */ }
        }
        return false;
    }

    private void CheckIdle()
    {
        if (!Running || DateTime.UtcNow - _lastActivity < IdleTimeout) return;
        _ = Task.Run(async () => { await StopAsync().ConfigureAwait(false); AutoStopped?.Invoke(); });
    }

    public async Task StopAsync()
    {
        _idleTimer?.Dispose(); _idleTimer = null;
        var app = _app;
        _app = null; Running = false; Url = null;
        if (app is not null)
        {
            try { await app.StopAsync().ConfigureAwait(false); await app.DisposeAsync().ConfigureAwait(false); }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException) { }
        }
        Changed?.Invoke();
    }

    private void MapRoutes(WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            _lastActivity = DateTime.UtcNow;
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            if (!DropToken.Matches(_token, ctx.Request.Query["t"]))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound; // do not reveal that anything is here
                return;
            }
            await next().ConfigureAwait(false);
        });

        app.MapGet("/", () => Results.Content(DropPageHtml.Html, "text/html; charset=utf-8"));

        app.MapGet("/api/files", () => Results.Json(Shared.Select(s => new { id = s.Id, name = s.Name, size = ShelfStore.FormatSize(s.Size) })));

        app.MapGet("/f/{id}", (string id) =>
        {
            var file = Shared.FirstOrDefault(s => s.Id == id);
            return file is null || !File.Exists(file.Path)
                ? Results.NotFound()
                : Results.File(file.Path, "application/octet-stream", file.Name, enableRangeProcessing: true);
        });

        app.MapPost("/upload", async (HttpRequest request) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest("Send files as a form upload.");
            IFormCollection form;
            try { form = await request.ReadFormAsync().ConfigureAwait(false); }
            catch (Exception e) when (e is InvalidDataException or Microsoft.AspNetCore.Http.BadHttpRequestException or IOException) { return Results.BadRequest("The upload could not be read."); }

            var saved = 0;
            Directory.CreateDirectory(ReceivedDir);
            foreach (var file in form.Files)
            {
                var name = UploadNames.Safe(file.FileName);
                string path, unique;
                FileStream stream;
                lock (_gate) // only the name reservation is under the lock, so the UI never waits for a long copy
                {
                    unique = ShelfStore.UniqueName(Directory.GetFiles(ReceivedDir).Select(Path.GetFileName).OfType<string>(), name);
                    path = Path.Combine(ReceivedDir, unique);
                    stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                }
                try
                {
                    await using (stream) await file.CopyToAsync(stream).ConfigureAwait(false);
                    MarkFromInternet(path);
                    lock (_gate) _received.Add(new ReceivedFile(unique, path, file.Length, DateTime.Now));
                    saved++;
                }
                catch (IOException)
                {
                    try { File.Delete(path); } catch (IOException) { } // never leave a half-written file behind
                    return Results.StatusCode(StatusCodes.Status507InsufficientStorage);
                }
            }
            Changed?.Invoke();
            return Results.Json(new { saved });
        });
    }

    /// <summary>Adds the Zone.Identifier stream (ZoneId=3, "Internet") so Windows shows its usual warning before running a received file.</summary>
    private static void MarkFromInternet(string path)
    {
        try { File.WriteAllText(path + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n"); }
        catch (Exception e) when (e is IOException or NotSupportedException or UnauthorizedAccessException) { } // not NTFS
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}

internal static class DropPageHtml
{
    public const string Html = """
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>VAK Drop</title>
<style>
body{margin:0;background:#000;color:#fff;font:16px system-ui,sans-serif;padding:20px;max-width:560px;margin-inline:auto}
h1{font-size:22px;margin:0 0 4px}p{color:#a6a6aa;margin:0 0 18px}
.card{background:#0d0d0f;border-radius:18px;padding:16px;margin-bottom:14px}
.btn{display:inline-block;background:#1d64d8;color:#fff;border:0;border-radius:12px;padding:12px 18px;font-size:16px}
.row{display:flex;justify-content:space-between;gap:10px;padding:10px 0;border-bottom:1px solid #1f1f22}
a{color:#2f7cf6;text-decoration:none}.dim{color:#a6a6aa}#status{margin-top:10px}
</style></head><body>
<h1>VAK Drop</h1><p>Send files to this PC, or grab files it is sharing.</p>
<div class="card"><b>Send to the PC</b><p class="dim">Pick one or more files.</p>
<input id="pick" type="file" multiple hidden><button class="btn" id="btn">Choose files</button><div id="status" class="dim"></div></div>
<div class="card"><b>From the PC</b><div id="list" class="dim">Nothing shared yet.</div></div>
<script>
const t=new URLSearchParams(location.search).get('t')||'';
const q='?t='+encodeURIComponent(t);
const status=document.getElementById('status');
document.getElementById('btn').onclick=()=>document.getElementById('pick').click();
document.getElementById('pick').onchange=async e=>{
  const files=[...e.target.files]; if(!files.length) return;
  const fd=new FormData(); files.forEach(f=>fd.append('files',f,f.name));
  status.textContent='Sending '+files.length+' file(s)...';
  try{const r=await fetch('/upload'+q,{method:'POST',body:fd}); const j=await r.json();
    status.textContent=r.ok?('Sent '+j.saved+' file(s).'):'Could not send.';}
  catch{status.textContent='Could not send.';}
  e.target.value='';
};
async function load(){
  try{const r=await fetch('/api/files'+q); const files=await r.json(); const el=document.getElementById('list');
    el.textContent=''; if(!files.length){el.textContent='Nothing shared yet.';return;}
    files.forEach(f=>{const row=document.createElement('div');row.className='row';
      const a=document.createElement('a');a.href='/f/'+encodeURIComponent(f.id)+q;a.textContent=f.name;
      const s=document.createElement('span');s.className='dim';s.textContent=f.size;row.append(a,s);el.append(row);});
  }catch{}
}
load(); setInterval(load,5000);
</script></body></html>
""";
}
