namespace DjSetupList.Music;

public sealed class MusicSourceOptions
{
    public LocalMusicOptions Local { get; set; } = new();
    public SpotifyOptions Spotify { get; set; } = new();
    public SoundCloudOptions SoundCloud { get; set; } = new();
    public AppleMusicOptions AppleMusic { get; set; } = new();
}

public sealed class LocalMusicOptions
{
    /// <summary>Directories scanned for audio files / M3U playlists.</summary>
    public List<string> LibraryPaths { get; set; } = [];
}

public sealed class SpotifyOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    /// <summary>Optional user refresh token for playlist access.</summary>
    public string? RefreshToken { get; set; }
    public string RedirectUri { get; set; } = "http://127.0.0.1:5088/auth/spotify/callback";
}

public sealed class SoundCloudOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? AccessToken { get; set; }
}

public sealed class AppleMusicOptions
{
    public string? TeamId { get; set; }
    public string? KeyId { get; set; }
    /// <summary>Path to AuthKey_XXX.p8</summary>
    public string? PrivateKeyPath { get; set; }
    /// <summary>Inline PEM contents (alternative to PrivateKeyPath).</summary>
    public string? PrivateKeyPem { get; set; }
    public string Storefront { get; set; } = "jp";
}
