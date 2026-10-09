namespace DjSetupList.Music;

public enum MusicSourceKind
{
    Local,
    Spotify,
    SoundCloud,
    AppleMusic
}

public sealed class MusicTrack
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public TimeSpan? Duration { get; init; }
    public required MusicSourceKind Source { get; init; }
    public string? ExternalUrl { get; init; }
    public string? LocalPath { get; init; }
    public bool OfflineReady { get; init; }

    public string Display =>
        string.IsNullOrWhiteSpace(Artist) ? Title : $"{Artist} — {Title}";
}

public sealed class MusicPlaylist
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required MusicSourceKind Source { get; init; }
    public string? ExternalUrl { get; init; }
    public int? TrackCount { get; init; }
    public string? Description { get; init; }
}

public sealed class MusicSourceStatus
{
    public required MusicSourceKind Kind { get; init; }
    public required string DisplayName { get; init; }
    public required bool Configured { get; init; }
    public required bool Available { get; init; }
    public string? Message { get; init; }
    public string? AuthUrlHint { get; init; }
}

public sealed class MusicSearchResult
{
    public required MusicSourceKind Source { get; init; }
    public IReadOnlyList<MusicTrack> Tracks { get; init; } = [];
    public IReadOnlyList<MusicPlaylist> Playlists { get; init; } = [];
    public string? Warning { get; init; }
}
