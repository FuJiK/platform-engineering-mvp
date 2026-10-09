using System.Net.Http.Headers;
using System.Text.Json;

namespace DjSetupList.Music;

public sealed class SoundCloudMusicSource : IMusicSource
{
    private readonly SoundCloudOptions _options;
    private readonly HttpClient _http;

    public SoundCloudMusicSource(SoundCloudOptions options, HttpClient? http = null)
    {
        _options = options;
        _http = http ?? new HttpClient { BaseAddress = new Uri("https://api.soundcloud.com/") };
    }

    public MusicSourceKind Kind => MusicSourceKind.SoundCloud;
    public string DisplayName => "SoundCloud";

    public MusicSourceStatus GetStatus()
    {
        var configured = !string.IsNullOrWhiteSpace(_options.ClientId) ||
                         !string.IsNullOrWhiteSpace(_options.AccessToken);
        return new MusicSourceStatus
        {
            Kind = Kind,
            DisplayName = DisplayName,
            Configured = configured,
            Available = configured,
            Message = configured
                ? "ClientId または AccessToken で検索・URL解決が可能です。"
                : "SoundCloud:ClientId または AccessToken を設定してください。",
            AuthUrlHint = "https://soundcloud.com/you/apps"
        };
    }

    public async Task<MusicSearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(query))
            return new MusicSearchResult { Source = Kind, Warning = "検索語を入力してください。" };

        // Prefer resolve when query looks like a URL
        if (query.Contains("soundcloud.com", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = await ResolveAsync(query, ct);
            return resolved;
        }

        var url = $"tracks?q={Uri.EscapeDataString(query)}&limit=25";
        using var req = CreateRequest(HttpMethod.Get, url);
        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"SoundCloud search failed: {res.StatusCode} {json}");

        using var doc = JsonDocument.Parse(json);
        var tracks = doc.RootElement.EnumerateArray().Select(ParseTrack).ToList();
        return new MusicSearchResult { Source = Kind, Tracks = tracks };
    }

    public async Task<IReadOnlyList<MusicTrack>> GetPlaylistTracksAsync(string playlistIdOrUrl, CancellationToken ct = default)
    {
        EnsureConfigured();
        JsonElement root;
        if (playlistIdOrUrl.Contains("soundcloud.com", StringComparison.OrdinalIgnoreCase))
        {
            root = await ResolveRawAsync(playlistIdOrUrl, ct);
        }
        else
        {
            using var req = CreateRequest(HttpMethod.Get, $"playlists/{playlistIdOrUrl}");
            using var res = await _http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"SoundCloud playlist failed: {res.StatusCode} {json}");
            root = JsonDocument.Parse(json).RootElement.Clone();
        }

        if (!root.TryGetProperty("tracks", out var tracksNode))
            return [];

        return tracksNode.EnumerateArray().Select(ParseTrack).ToList();
    }

    private async Task<MusicSearchResult> ResolveAsync(string url, CancellationToken ct)
    {
        var root = await ResolveRawAsync(url, ct);
        var kind = root.TryGetProperty("kind", out var k) ? k.GetString() : null;
        if (kind == "playlist")
        {
            var playlist = new MusicPlaylist
            {
                Id = root.GetProperty("id").GetRawText(),
                Name = root.GetProperty("title").GetString() ?? "(playlist)",
                Source = Kind,
                ExternalUrl = root.TryGetProperty("permalink_url", out var pu) ? pu.GetString() : url,
                TrackCount = root.TryGetProperty("track_count", out var tc) ? tc.GetInt32() : null
            };
            var tracks = root.TryGetProperty("tracks", out var tr)
                ? tr.EnumerateArray().Select(ParseTrack).ToList()
                : [];
            return new MusicSearchResult { Source = Kind, Playlists = [playlist], Tracks = tracks };
        }

        return new MusicSearchResult { Source = Kind, Tracks = [ParseTrack(root)] };
    }

    private async Task<JsonElement> ResolveRawAsync(string url, CancellationToken ct)
    {
        using var req = CreateRequest(HttpMethod.Get, $"resolve?url={Uri.EscapeDataString(url)}");
        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"SoundCloud resolve failed: {res.StatusCode} {json}");
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static MusicTrack ParseTrack(JsonElement item)
    {
        var user = item.TryGetProperty("user", out var u) && u.TryGetProperty("username", out var un)
            ? un.GetString()
            : null;
        return new MusicTrack
        {
            Id = $"soundcloud:{item.GetProperty("id").GetRawText()}",
            Title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "(untitled)" : "(untitled)",
            Artist = user,
            Duration = item.TryGetProperty("duration", out var d)
                ? TimeSpan.FromMilliseconds(d.GetInt64())
                : null,
            Source = MusicSourceKind.SoundCloud,
            ExternalUrl = item.TryGetProperty("permalink_url", out var p) ? p.GetString() : null,
            OfflineReady = false
        };
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relative)
    {
        var req = new HttpRequestMessage(method, relative);
        if (!string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("OAuth", _options.AccessToken);
        }
        else if (!string.IsNullOrWhiteSpace(_options.ClientId))
        {
            var joiner = relative.Contains('?') ? "&" : "?";
            req.RequestUri = new Uri(relative + joiner + "client_id=" + Uri.EscapeDataString(_options.ClientId!), UriKind.Relative);
        }

        return req;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) && string.IsNullOrWhiteSpace(_options.AccessToken))
            throw new InvalidOperationException("SoundCloud が未設定です。ClientId または AccessToken を設定してください。");
    }
}
