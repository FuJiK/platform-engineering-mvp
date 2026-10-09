namespace DjSetupList.Music;

public interface IMusicSource
{
    MusicSourceKind Kind { get; }
    string DisplayName { get; }
    MusicSourceStatus GetStatus();
    Task<MusicSearchResult> SearchAsync(string query, CancellationToken ct = default);
    Task<IReadOnlyList<MusicTrack>> GetPlaylistTracksAsync(string playlistIdOrUrl, CancellationToken ct = default);
}
