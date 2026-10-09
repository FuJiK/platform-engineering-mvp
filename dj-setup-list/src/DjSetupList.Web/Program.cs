using DjSetupList.Models;
using DjSetupList.Music;
using DjSetupList.Services;
using DjSetupList.Templates;
using DjSetupList.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MusicSourceOptions>(builder.Configuration.GetSection("Music"));
builder.Services.AddHttpClient("spotify", c => c.BaseAddress = new Uri("https://api.spotify.com/v1/"));
builder.Services.AddHttpClient("soundcloud", c => c.BaseAddress = new Uri("https://api.soundcloud.com/"));
builder.Services.AddHttpClient("applemusic", c => c.BaseAddress = new Uri("https://api.music.apple.com/v1/"));
builder.Services.AddSingleton<ListWorkspace>();
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MusicSourceOptions>>().Value;
    // Env overrides for local library convenience
    var envLib = Environment.GetEnvironmentVariable("DJ_LOCAL_LIBRARY");
    if (!string.IsNullOrWhiteSpace(envLib))
    {
        options.Local.LibraryPaths = envLib
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    options.Spotify.ClientId ??= Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
    options.Spotify.ClientSecret ??= Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");
    options.Spotify.RefreshToken ??= Environment.GetEnvironmentVariable("SPOTIFY_REFRESH_TOKEN");
    options.SoundCloud.ClientId ??= Environment.GetEnvironmentVariable("SOUNDCLOUD_CLIENT_ID");
    options.SoundCloud.AccessToken ??= Environment.GetEnvironmentVariable("SOUNDCLOUD_ACCESS_TOKEN");
    options.AppleMusic.TeamId ??= Environment.GetEnvironmentVariable("APPLE_MUSIC_TEAM_ID");
    options.AppleMusic.KeyId ??= Environment.GetEnvironmentVariable("APPLE_MUSIC_KEY_ID");
    options.AppleMusic.PrivateKeyPath ??= Environment.GetEnvironmentVariable("APPLE_MUSIC_PRIVATE_KEY_PATH");

    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return MusicSourceCatalog.FromOptions(options, name => factory.CreateClient(name));
});
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MusicSourceOptions>>().Value;
    return new SpotifyMusicSource(options.Spotify, sp.GetRequiredService<IHttpClientFactory>().CreateClient("spotify"));
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { ok = true, service = "dj-setup-list-web" }));

app.MapGet("/api/templates", () =>
    BuiltinTemplates.All.Select(t => new
    {
        t.Id,
        t.Name,
        t.Description,
        itemCount = t.Categories.Sum(c => c.Items.Count),
        categories = t.Categories.Select(c => new { c.Id, c.Title, count = c.Items.Count })
    }));

app.MapGet("/api/lists", (ListWorkspace workspace) => workspace.ListAll());

app.MapPost("/api/lists", (CreateListRequest request, ListWorkspace workspace) =>
{
    var (fileName, list) = workspace.Create(request);
    return Results.Created($"/api/lists/{fileName}", new { fileName, list });
});

app.MapGet("/api/lists/{fileName}", (string fileName, ListWorkspace workspace) =>
{
    try { return Results.Ok(workspace.Get(fileName)); }
    catch (FileNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
});

app.MapPost("/api/lists/{fileName}/toggle", (string fileName, ToggleRequest body, ListWorkspace workspace) =>
{
    try { return Results.Ok(workspace.ToggleItem(fileName, body.ItemId, body.Checked)); }
    catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/api/lists/{fileName}/export", (string fileName, string? format, ListWorkspace workspace) =>
{
    var list = workspace.Get(fileName);
    var fmt = SetupListExporter.ParseFormat(format ?? "md");
    var text = SetupListExporter.Export(list, fmt);
    var contentType = fmt switch
    {
        ExportFormat.Json => "application/json; charset=utf-8",
        ExportFormat.Markdown => "text/markdown; charset=utf-8",
        _ => "text/plain; charset=utf-8"
    };
    return Results.Text(text, contentType);
});

app.MapGet("/api/music/sources", (MusicSourceCatalog catalog) => catalog.Statuses());

app.MapGet("/api/music/search", async (string source, string? q, MusicSourceCatalog catalog) =>
{
    var src = catalog.TryGet(source);
    if (src is null) return Results.BadRequest(new { error = $"未知の音源: {source}" });
    try
    {
        var result = await src.SearchAsync(q ?? "");
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message, status = src.GetStatus() });
    }
});

app.MapPost("/api/lists/{fileName}/music", async (string fileName, AttachMusicRequest body, ListWorkspace workspace, MusicSourceCatalog catalog) =>
{
    var src = catalog.TryGet(body.Source);
    if (src is null) return Results.BadRequest(new { error = $"未知の音源: {body.Source}" });
    try
    {
        IReadOnlyList<MusicTrack> tracks;
        if (!string.IsNullOrWhiteSpace(body.Playlist))
            tracks = await src.GetPlaylistTracksAsync(body.Playlist);
        else if (body.Tracks is { Count: > 0 })
            tracks = body.Tracks;
        else
            return Results.BadRequest(new { error = "playlist または tracks が必要です。" });

        var list = workspace.Get(fileName);
        list.Music.Add(new MusicAttachment
        {
            Source = src.Kind,
            PlaylistId = body.Playlist,
            PlaylistName = body.Name ?? body.Playlist ?? $"{src.DisplayName} selection",
            PlaylistUrl = body.Playlist is not null && body.Playlist.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? body.Playlist
                : null,
            Tracks = tracks.ToList()
        });
        workspace.Save(fileName, list);
        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/auth/spotify/login", (SpotifyMusicSource spotify) =>
{
    var status = spotify.GetStatus();
    if (!status.Configured)
        return Results.BadRequest(new { error = status.Message, hint = status.AuthUrlHint });
    return Results.Redirect(spotify.BuildAuthorizeUrl());
});

app.MapGet("/auth/spotify/callback", async (string? code, string? error, SpotifyMusicSource spotify, Microsoft.Extensions.Options.IOptions<MusicSourceOptions> options) =>
{
    if (!string.IsNullOrWhiteSpace(error))
        return Results.BadRequest($"Spotify auth error: {error}");
    if (string.IsNullOrWhiteSpace(code))
        return Results.BadRequest("code がありません。");

    await spotify.ExchangeCodeAsync(code);
    var refresh = options.Value.Spotify.RefreshToken;
    return Results.Content($"""
        <html><body style="font-family:sans-serif;background:#111;color:#eee;padding:2rem">
        <h1>Spotify 連携完了</h1>
        <p>このセッションでアクセストークンを取得しました。</p>
        <p>永続化する場合は Refresh Token を環境変数 <code>SPOTIFY_REFRESH_TOKEN</code> に保存してください:</p>
        <pre style="white-space:pre-wrap;background:#222;padding:1rem">{refresh}</pre>
        <p><a href="/" style="color:#f0c35a">セットアップリストへ戻る</a></p>
        </body></html>
        """, "text/html");
});

app.MapFallbackToFile("index.html");

app.Run();

public sealed class ToggleRequest
{
    public string ItemId { get; set; } = "";
    public bool? Checked { get; set; }
}

public sealed class AttachMusicRequest
{
    public string Source { get; set; } = "local";
    public string? Playlist { get; set; }
    public string? Name { get; set; }
    public List<DjSetupList.Music.MusicTrack>? Tracks { get; set; }
}

public partial class Program;
