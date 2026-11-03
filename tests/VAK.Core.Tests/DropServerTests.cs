using System.Net;
using System.Net.Http;
using VAK.Core.Drop;
using Xunit;

namespace VAK.Core.Tests;

public class DropServerTests : IAsyncLifetime
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vak-drop-" + Guid.NewGuid().ToString("N"));
    private readonly HttpClient _http = new();
    private DropServer _server = null!;
    private string _base = "";
    private string _token = "";

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_dir);
        // loopback stands in for a private LAN address so the test never opens a port on the real network
        _server = new DropServer(Path.Combine(_dir, "received"), ip => IPAddress.IsLoopback(ip));
        Assert.True(await _server.StartAsync(IPAddress.Loopback));
        var uri = new Uri(_server.Url!);
        _base = $"http://127.0.0.1:{uri.Port}";
        _token = System.Web.HttpUtility.ParseQueryString(uri.Query)["t"]!;
    }

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
        _http.Dispose();
        Directory.Delete(_dir, true);
    }

    private static MultipartFormDataContent Upload(string fileName, string content)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(content), "files", fileName);
        return form;
    }

    [Fact]
    public async Task Home_NeedsTheToken()
    {
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync($"{_base}/?t={_token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"{_base}/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"{_base}/?t=wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"{_base}/api/files")).StatusCode);
    }

    [Fact]
    public async Task Home_SendsSecurityHeaders()
    {
        var r = await _http.GetAsync($"{_base}/?t={_token}");
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("no-store", r.Headers.GetValues("Cache-Control").Single());
    }

    [Fact]
    public async Task Upload_SavesTheFile_AndListsIt()
    {
        var r = await _http.PostAsync($"{_base}/upload?t={_token}", Upload("hello.txt", "hi there"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("hi there", File.ReadAllText(Path.Combine(_dir, "received", "hello.txt")));
        Assert.Equal("hello.txt", Assert.Single(_server.Received).Name);
    }

    [Fact]
    public async Task Upload_WithoutToken_IsRefused_AndSavesNothing()
    {
        var r = await _http.PostAsync($"{_base}/upload", Upload("x.txt", "x"));
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(_dir, "received")) && Directory.GetFiles(Path.Combine(_dir, "received")).Length > 0);
    }

    [Fact]
    public async Task Upload_PathTricksInTheName_StayInsideTheFolder()
    {
        await _http.PostAsync($"{_base}/upload?t={_token}", Upload("../../evil.txt", "x"));
        Assert.Equal("evil.txt", Assert.Single(_server.Received).Name);
        Assert.False(File.Exists(Path.Combine(_dir, "evil.txt")));
        Assert.True(File.Exists(Path.Combine(_dir, "received", "evil.txt")));
    }

    [Fact]
    public async Task Upload_SameNameTwice_IsNumbered()
    {
        await _http.PostAsync($"{_base}/upload?t={_token}", Upload("a.txt", "1"));
        await _http.PostAsync($"{_base}/upload?t={_token}", Upload("a.txt", "2"));
        Assert.Equal(new[] { "a (2).txt", "a.txt" }, _server.Received.Select(f => f.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task Upload_NotAForm_IsABadRequest()
    {
        var r = await _http.PostAsync($"{_base}/upload?t={_token}", new StringContent("just text"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task SharedFile_CanBeDownloaded_ByIdOnly()
    {
        var path = Path.Combine(_dir, "share.txt");
        File.WriteAllText(path, "shared content");
        Assert.True(_server.AddShared(path));
        var id = _server.Shared.Single().Id;

        Assert.Equal("shared content", await _http.GetStringAsync($"{_base}/f/{id}?t={_token}"));
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"{_base}/f/{id}")).StatusCode);          // no token
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"{_base}/f/nope?t={_token}")).StatusCode); // unknown id
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"{_base}/f/..%2Fshare.txt?t={_token}")).StatusCode);
    }

    [Fact]
    public async Task FileList_ShowsNamesAndSizes()
    {
        var path = Path.Combine(_dir, "list.txt");
        File.WriteAllText(path, "12345");
        _server.AddShared(path);
        var json = await _http.GetStringAsync($"{_base}/api/files?t={_token}");
        Assert.Contains("list.txt", json);
        Assert.Contains("5 B", json);
    }

    [Fact]
    public async Task RemovedShare_IsNoLongerDownloadable()
    {
        var path = Path.Combine(_dir, "gone.txt");
        File.WriteAllText(path, "x");
        _server.AddShared(path);
        var id = _server.Shared.Single().Id;
        _server.RemoveShared(id);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"{_base}/f/{id}?t={_token}")).StatusCode);
    }

    [Fact]
    public async Task Stop_ClosesThePort()
    {
        await _server.StopAsync();
        Assert.False(_server.Running);
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => _http.GetAsync($"{_base}/?t={_token}"));
    }

    [Fact]
    public async Task Start_OnAPublicAddress_IsRefused()
    {
        await using var s = new DropServer(Path.Combine(_dir, "r2")); // default rule: private LAN addresses only
        Assert.False(await s.StartAsync(IPAddress.Parse("8.8.8.8")));
        Assert.False(await s.StartAsync(IPAddress.Loopback));
        Assert.False(s.Running);
    }
}
