namespace DjSetupList.Music;

public sealed class MusicSourceCatalog
{
    private readonly IReadOnlyDictionary<MusicSourceKind, IMusicSource> _sources;

    public MusicSourceCatalog(IEnumerable<IMusicSource> sources)
    {
        _sources = sources.ToDictionary(s => s.Kind);
    }

    public static MusicSourceCatalog FromOptions(
        MusicSourceOptions options,
        Func<string, HttpClient?>? httpClientFactory = null)
    {
        HttpClient? Create(string name) => httpClientFactory?.Invoke(name);

        return new MusicSourceCatalog(
        [
            new LocalMusicSource(options.Local),
            new SpotifyMusicSource(options.Spotify, Create("spotify")),
            new SoundCloudMusicSource(options.SoundCloud, Create("soundcloud")),
            new AppleMusicSource(options.AppleMusic, Create("applemusic"))
        ]);
    }

    public static MusicSourceCatalog FromEnvironment()
    {
        var options = new MusicSourceOptions();

        var local = Environment.GetEnvironmentVariable("DJ_LOCAL_LIBRARY");
        if (!string.IsNullOrWhiteSpace(local))
            options.Local.LibraryPaths = local.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        options.Spotify.ClientId = Env("SPOTIFY_CLIENT_ID") ?? Env("DJ_SPOTIFY_CLIENT_ID");
        options.Spotify.ClientSecret = Env("SPOTIFY_CLIENT_SECRET") ?? Env("DJ_SPOTIFY_CLIENT_SECRET");
        options.Spotify.RefreshToken = Env("SPOTIFY_REFRESH_TOKEN") ?? Env("DJ_SPOTIFY_REFRESH_TOKEN");

        options.SoundCloud.ClientId = Env("SOUNDCLOUD_CLIENT_ID") ?? Env("DJ_SOUNDCLOUD_CLIENT_ID");
        options.SoundCloud.ClientSecret = Env("SOUNDCLOUD_CLIENT_SECRET");
        options.SoundCloud.AccessToken = Env("SOUNDCLOUD_ACCESS_TOKEN");

        options.AppleMusic.TeamId = Env("APPLE_MUSIC_TEAM_ID");
        options.AppleMusic.KeyId = Env("APPLE_MUSIC_KEY_ID");
        options.AppleMusic.PrivateKeyPath = Env("APPLE_MUSIC_PRIVATE_KEY_PATH");
        options.AppleMusic.PrivateKeyPem = Env("APPLE_MUSIC_PRIVATE_KEY_PEM");
        options.AppleMusic.Storefront = Env("APPLE_MUSIC_STOREFRONT") ?? "jp";

        return FromOptions(options);
    }

    public IReadOnlyList<IMusicSource> All => _sources.Values.ToList();

    public IMusicSource Get(MusicSourceKind kind) =>
        _sources.TryGetValue(kind, out var source)
            ? source
            : throw new ArgumentException($"未知の音源: {kind}");

    public IMusicSource? TryGet(string name)
    {
        if (Enum.TryParse<MusicSourceKind>(name, ignoreCase: true, out var kind))
            return _sources.GetValueOrDefault(kind);

        return name.ToLowerInvariant() switch
        {
            "local" or "folder" or "file" => _sources.GetValueOrDefault(MusicSourceKind.Local),
            "spotify" => _sources.GetValueOrDefault(MusicSourceKind.Spotify),
            "soundcloud" or "sc" => _sources.GetValueOrDefault(MusicSourceKind.SoundCloud),
            "apple" or "applemusic" or "music" => _sources.GetValueOrDefault(MusicSourceKind.AppleMusic),
            _ => null
        };
    }

    public IReadOnlyList<MusicSourceStatus> Statuses() =>
        All.Select(s => s.GetStatus()).ToList();

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
