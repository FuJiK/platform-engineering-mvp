using DjSetupList.Music;

namespace DjSetupList.Tests;

public class LocalMusicSourceTests
{
    [Fact]
    public async Task Search_finds_audio_files_and_m3u()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dj-lib-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "Artist - Song.mp3"), "x");
            await File.WriteAllTextAsync(Path.Combine(root, "set.m3u"), """
                #EXTM3U
                #EXTINF:120,Artist - Song
                Artist - Song.mp3
                """);

            var source = new LocalMusicSource(new LocalMusicOptions { LibraryPaths = [root] });
            Assert.True(source.GetStatus().Available);

            var result = await source.SearchAsync("Song");
            Assert.Contains(result.Tracks, t => t.Title.Contains("Song", StringComparison.OrdinalIgnoreCase));

            var all = await source.SearchAsync("");
            Assert.Contains(all.Playlists, p => p.Name == "set");

            var tracks = await source.GetPlaylistTracksAsync(all.Playlists.First(p => p.Name == "set").Id);
            Assert.Single(tracks);
            Assert.True(tracks[0].OfflineReady);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Catalog_reports_unconfigured_streaming_providers()
    {
        var catalog = MusicSourceCatalog.FromOptions(new MusicSourceOptions
        {
            Local = new LocalMusicOptions { LibraryPaths = ["/tmp/does-not-exist-dj"] }
        });

        var statuses = catalog.Statuses();
        Assert.Contains(statuses, s => s.Kind == MusicSourceKind.Spotify && !s.Configured);
        Assert.Contains(statuses, s => s.Kind == MusicSourceKind.SoundCloud && !s.Configured);
        Assert.Contains(statuses, s => s.Kind == MusicSourceKind.AppleMusic && !s.Configured);
    }
}
