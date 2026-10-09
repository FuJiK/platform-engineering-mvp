using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DjSetupList.Music;

public sealed class SpotifyMusicSource : IMusicSource
{
    private readonly SpotifyOptions _options;
    private readonly HttpClient _http;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt;

    public SpotifyMusicSource(SpotifyOptions options, HttpClient? http = null)
    {
        _options = options;
        _http = http ?? new HttpClient { BaseAddress = new Uri("https://api.spotify.com/v1/") };
    }

    public MusicSourceKind Kind => MusicSourceKind.Spotify;
    public string DisplayName => "Spotify";

    public MusicSourceStatus GetStatus()
    {
        var configured = !string.IsNullOrWhiteSpace(_options.ClientId) &&
                         !string.IsNullOrWhiteSpace(_options.ClientSecret);
        if (!configured)
        {
            return new MusicSourceStatus
            {
                Kind = Kind,
                DisplayName = DisplayName,
                Configured = false,
                Available = false,
                Message = "Spotify:ClientId / ClientSecret を設定してください。",
                AuthUrlHint = "https://developer.spotify.com/dashboard"
            };
        }

        var hasUser = !string.IsNullOrWhiteSpace(_options.RefreshToken);
        return new MusicSourceStatus
        {
            Kind = Kind,
            DisplayName = DisplayName,
            Configured = true,
            Available = true,
            Message = hasUser
                ? "Client Credentials + Refresh Token 利用可（プレイリスト取得可）"
                : "Client Credentials のみ（カタログ検索可。ユーザープレイリストには RefreshToken が必要）",
            AuthUrlHint = BuildAuthorizeUrl()
        };
    }

    public string BuildAuthorizeUrl(string? state = null)
    {
        var scopes = Uri.EscapeDataString("playlist-read-private playlist-read-collaborative user-library-read");
        var redirect = Uri.EscapeDataString(_options.RedirectUri);
        var st = Uri.EscapeDataString(state ?? "dj-setup");
        return $"https://accounts.spotify.com/authorize?client_id={_options.ClientId}&response_type=code&redirect_uri={redirect}&scope={scopes}&state={st}";
    }

    public async Task ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        EnsureConfigured();
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicAuth());
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri
        });

        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Spotify token exchange failed: {res.StatusCode} {json}");

        using var doc = JsonDocument.Parse(json);
        _accessToken = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
        _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 30);
        if (doc.RootElement.TryGetProperty("refresh_token", out var rt))
            _options.RefreshToken = rt.GetString();
    }

    public async Task<MusicSearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new MusicSearchResult { Source = Kind, Warning = "検索語を入力してください。" };

        await EnsureAccessTokenAsync(preferUser: false, ct);
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"search?q={Uri.EscapeDataString(query)}&type=track,playlist&limit=20");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Spotify search failed: {res.StatusCode} {json}");

        using var doc = JsonDocument.Parse(json);
        var tracks = new List<MusicTrack>();
        var playlists = new List<MusicPlaylist>();

        if (doc.RootElement.TryGetProperty("tracks", out var tracksNode) &&
            tracksNode.TryGetProperty("items", out var trackItems))
        {
            foreach (var item in trackItems.EnumerateArray())
                tracks.Add(ParseTrack(item));
        }

        if (doc.RootElement.TryGetProperty("playlists", out var playlistsNode) &&
            playlistsNode.TryGetProperty("items", out var playlistItems))
        {
            foreach (var item in playlistItems.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null) continue;
                playlists.Add(new MusicPlaylist
                {
                    Id = item.GetProperty("id").GetString()!,
                    Name = item.GetProperty("name").GetString() ?? "(untitled)",
                    Source = Kind,
                    ExternalUrl = item.TryGetProperty("external_urls", out var urls) &&
                                  urls.TryGetProperty("spotify", out var sp)
                        ? sp.GetString()
                        : null,
                    TrackCount = item.TryGetProperty("tracks", out var t) && t.TryGetProperty("total", out var total)
                        ? total.GetInt32()
                        : null,
                    Description = item.TryGetProperty("description", out var d) ? d.GetString() : null
                });
            }
        }

        return new MusicSearchResult { Source = Kind, Tracks = tracks, Playlists = playlists };
    }

    public async Task<IReadOnlyList<MusicTrack>> GetPlaylistTracksAsync(string playlistIdOrUrl, CancellationToken ct = default)
    {
        var id = ExtractPlaylistId(playlistIdOrUrl);
        await EnsureAccessTokenAsync(preferUser: true, ct);
        var tracks = new List<MusicTrack>();
        var path = $"playlists/{id}/tracks?limit=100";

        while (!string.IsNullOrEmpty(path))
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, path.StartsWith("http", StringComparison.Ordinal)
                ? path
                : path);
            if (path.StartsWith("http", StringComparison.Ordinal))
                req.RequestUri = new Uri(path);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using var res = await _http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"Spotify playlist failed: {res.StatusCode} {json}");

            using var doc = JsonDocument.Parse(json);
            foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                if (!item.TryGetProperty("track", out var track) || track.ValueKind == JsonValueKind.Null)
                    continue;
                tracks.Add(ParseTrack(track));
            }

            path = doc.RootElement.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }

        return tracks;
    }

    private static string ExtractPlaylistId(string playlistIdOrUrl)
    {
        if (playlistIdOrUrl.Contains("playlist/", StringComparison.OrdinalIgnoreCase))
        {
            var part = playlistIdOrUrl.Split("playlist/", 2, StringSplitOptions.None)[1];
            return part.Split('?', '#')[0].Trim('/');
        }

        return playlistIdOrUrl.Trim();
    }

    private static MusicTrack ParseTrack(JsonElement item)
    {
        var artists = item.TryGetProperty("artists", out var arr)
            ? string.Join(", ", arr.EnumerateArray().Select(a => a.GetProperty("name").GetString()))
            : null;
        return new MusicTrack
        {
            Id = $"spotify:{item.GetProperty("id").GetString()}",
            Title = item.GetProperty("name").GetString() ?? "(untitled)",
            Artist = artists,
            Album = item.TryGetProperty("album", out var album) && album.TryGetProperty("name", out var an)
                ? an.GetString()
                : null,
            Duration = item.TryGetProperty("duration_ms", out var ms)
                ? TimeSpan.FromMilliseconds(ms.GetInt32())
                : null,
            Source = MusicSourceKind.Spotify,
            ExternalUrl = item.TryGetProperty("external_urls", out var urls) &&
                          urls.TryGetProperty("spotify", out var sp)
                ? sp.GetString()
                : null,
            OfflineReady = false
        };
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new InvalidOperationException("Spotify が未設定です。ClientId / ClientSecret を設定してください。");
    }

    private async Task EnsureAccessTokenAsync(bool preferUser = false, CancellationToken ct = default)
    {
        EnsureConfigured();
        if (!string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return;

        if (preferUser && !string.IsNullOrWhiteSpace(_options.RefreshToken))
        {
            await RefreshUserTokenAsync(ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_options.RefreshToken))
        {
            try
            {
                await RefreshUserTokenAsync(ct);
                return;
            }
            catch
            {
                // fall back to client credentials
            }
        }

        await ClientCredentialsAsync(ct);
    }

    private async Task ClientCredentialsAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicAuth());
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });
        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Spotify client_credentials failed: {res.StatusCode} {json}");
        using var doc = JsonDocument.Parse(json);
        _accessToken = doc.RootElement.GetProperty("access_token").GetString();
        _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 30);
    }

    private async Task RefreshUserTokenAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicAuth());
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = _options.RefreshToken!
        });
        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Spotify refresh failed: {res.StatusCode} {json}");
        using var doc = JsonDocument.Parse(json);
        _accessToken = doc.RootElement.GetProperty("access_token").GetString();
        _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 30);
    }

    private string BasicAuth() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
}
