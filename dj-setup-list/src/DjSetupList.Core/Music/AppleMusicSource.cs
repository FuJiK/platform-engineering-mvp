using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace DjSetupList.Music;

public sealed class AppleMusicSource : IMusicSource
{
    private readonly AppleMusicOptions _options;
    private readonly HttpClient _http;
    private string? _developerToken;
    private DateTimeOffset _tokenExpiresAt;

    public AppleMusicSource(AppleMusicOptions options, HttpClient? http = null)
    {
        _options = options;
        _http = http ?? new HttpClient { BaseAddress = new Uri("https://api.music.apple.com/v1/") };
    }

    public MusicSourceKind Kind => MusicSourceKind.AppleMusic;
    public string DisplayName => "Apple Music";

    public MusicSourceStatus GetStatus()
    {
        var configured = !string.IsNullOrWhiteSpace(_options.TeamId) &&
                         !string.IsNullOrWhiteSpace(_options.KeyId) &&
                         (!string.IsNullOrWhiteSpace(_options.PrivateKeyPath) ||
                          !string.IsNullOrWhiteSpace(_options.PrivateKeyPem));

        return new MusicSourceStatus
        {
            Kind = Kind,
            DisplayName = DisplayName,
            Configured = configured,
            Available = configured,
            Message = configured
                ? $"Developer Token でカタログ検索可（storefront={_options.Storefront}）。ライブラリ連携は Music User Token が別途必要です。"
                : "AppleMusic:TeamId / KeyId / PrivateKeyPath(or Pem) を設定してください。",
            AuthUrlHint = "https://developer.apple.com/documentation/applemusicapi/getting_keys_and_creating_tokens"
        };
    }

    public async Task<MusicSearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(query))
            return new MusicSearchResult { Source = Kind, Warning = "検索語を入力してください。" };

        await EnsureDeveloperTokenAsync(ct);
        var path =
            $"catalog/{_options.Storefront}/search?term={Uri.EscapeDataString(query)}&types=songs,playlists&limit=20";
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _developerToken);
        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Apple Music search failed: {res.StatusCode} {json}");

        using var doc = JsonDocument.Parse(json);
        var tracks = new List<MusicTrack>();
        var playlists = new List<MusicPlaylist>();
        if (doc.RootElement.TryGetProperty("results", out var results))
        {
            if (results.TryGetProperty("songs", out var songs) &&
                songs.TryGetProperty("data", out var songData))
            {
                foreach (var item in songData.EnumerateArray())
                    tracks.Add(ParseSong(item));
            }

            if (results.TryGetProperty("playlists", out var pls) &&
                pls.TryGetProperty("data", out var plData))
            {
                foreach (var item in plData.EnumerateArray())
                {
                    var attrs = item.GetProperty("attributes");
                    playlists.Add(new MusicPlaylist
                    {
                        Id = item.GetProperty("id").GetString()!,
                        Name = attrs.GetProperty("name").GetString() ?? "(playlist)",
                        Source = Kind,
                        ExternalUrl = attrs.TryGetProperty("url", out var url) ? url.GetString() : null,
                        Description = attrs.TryGetProperty("description", out var desc) &&
                                      desc.TryGetProperty("standard", out var std)
                            ? std.GetString()
                            : null
                    });
                }
            }
        }

        return new MusicSearchResult { Source = Kind, Tracks = tracks, Playlists = playlists };
    }

    public async Task<IReadOnlyList<MusicTrack>> GetPlaylistTracksAsync(string playlistIdOrUrl, CancellationToken ct = default)
    {
        EnsureConfigured();
        await EnsureDeveloperTokenAsync(ct);
        var id = ExtractPlaylistId(playlistIdOrUrl);
        var path = $"catalog/{_options.Storefront}/playlists/{id}/tracks?limit=100";
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _developerToken);
        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Apple Music playlist failed: {res.StatusCode} {json}");

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return [];
        return data.EnumerateArray().Select(ParseSong).ToList();
    }

    private static string ExtractPlaylistId(string value)
    {
        if (value.Contains("pl.", StringComparison.Ordinal))
        {
            var idx = value.IndexOf("pl.", StringComparison.Ordinal);
            var slice = value[idx..];
            return slice.Split('/', '?', '#')[0];
        }

        return value.Trim();
    }

    private static MusicTrack ParseSong(JsonElement item)
    {
        var attrs = item.GetProperty("attributes");
        return new MusicTrack
        {
            Id = $"apple:{item.GetProperty("id").GetString()}",
            Title = attrs.GetProperty("name").GetString() ?? "(untitled)",
            Artist = attrs.TryGetProperty("artistName", out var artist) ? artist.GetString() : null,
            Album = attrs.TryGetProperty("albumName", out var album) ? album.GetString() : null,
            Duration = attrs.TryGetProperty("durationInMillis", out var ms)
                ? TimeSpan.FromMilliseconds(ms.GetInt64())
                : null,
            Source = MusicSourceKind.AppleMusic,
            ExternalUrl = attrs.TryGetProperty("url", out var url) ? url.GetString() : null,
            OfflineReady = false
        };
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.TeamId) ||
            string.IsNullOrWhiteSpace(_options.KeyId) ||
            (string.IsNullOrWhiteSpace(_options.PrivateKeyPath) && string.IsNullOrWhiteSpace(_options.PrivateKeyPem)))
        {
            throw new InvalidOperationException("Apple Music が未設定です。TeamId / KeyId / PrivateKey を設定してください。");
        }
    }

    private Task EnsureDeveloperTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_developerToken) && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return Task.CompletedTask;

        var pem = _options.PrivateKeyPem;
        if (string.IsNullOrWhiteSpace(pem))
            pem = File.ReadAllText(_options.PrivateKeyPath!);

        pem = pem.Replace("-----BEGIN PRIVATE KEY-----", "", StringComparison.Ordinal)
            .Replace("-----END PRIVATE KEY-----", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Trim();

        var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(pem), out _);
        var credentials = new SigningCredentials(new ECDsaSecurityKey(key), SecurityAlgorithms.EcdsaSha256);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _options.TeamId,
            expires: now.AddHours(12),
            signingCredentials: credentials);
        token.Header["kid"] = _options.KeyId;
        _developerToken = new JwtSecurityTokenHandler().WriteToken(token);
        _tokenExpiresAt = now.AddHours(11);
        return Task.CompletedTask;
    }
}
