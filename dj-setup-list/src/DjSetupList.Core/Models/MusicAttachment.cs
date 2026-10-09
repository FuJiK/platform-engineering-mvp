using DjSetupList.Music;

namespace DjSetupList.Models;

public sealed class MusicAttachment
{
    public required MusicSourceKind Source { get; init; }
    public string? PlaylistId { get; init; }
    public string? PlaylistName { get; init; }
    public string? PlaylistUrl { get; init; }
    public List<MusicTrack> Tracks { get; init; } = [];

    public int TrackCount => Tracks.Count;
    public int OfflineReadyCount => Tracks.Count(t => t.OfflineReady);

    public MusicAttachment Clone() => new()
    {
        Source = Source,
        PlaylistId = PlaylistId,
        PlaylistName = PlaylistName,
        PlaylistUrl = PlaylistUrl,
        Tracks = Tracks.Select(t => new MusicTrack
        {
            Id = t.Id,
            Title = t.Title,
            Artist = t.Artist,
            Album = t.Album,
            Duration = t.Duration,
            Source = t.Source,
            ExternalUrl = t.ExternalUrl,
            LocalPath = t.LocalPath,
            OfflineReady = t.OfflineReady
        }).ToList()
    };
}
