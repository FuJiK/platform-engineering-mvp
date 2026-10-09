using System.Text.RegularExpressions;

namespace DjSetupList.Music;

public sealed class LocalMusicSource : IMusicSource
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aiff", ".aif", ".m4a", ".aac", ".ogg", ".wma"
    };

    private readonly LocalMusicOptions _options;

    public LocalMusicSource(LocalMusicOptions options) => _options = options;

    public MusicSourceKind Kind => MusicSourceKind.Local;
    public string DisplayName => "ローカル";

    public MusicSourceStatus GetStatus()
    {
        var existing = _options.LibraryPaths
            .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(Expand(p)))
            .Select(Expand)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (existing.Count == 0)
        {
            return new MusicSourceStatus
            {
                Kind = Kind,
                DisplayName = DisplayName,
                Configured = _options.LibraryPaths.Count > 0,
                Available = false,
                Message = _options.LibraryPaths.Count == 0
                    ? "LibraryPaths が未設定です。appsettings または DJ_LOCAL_LIBRARY でフォルダを指定してください。"
                    : "指定フォルダが見つかりません。"
            };
        }

        return new MusicSourceStatus
        {
            Kind = Kind,
            DisplayName = DisplayName,
            Configured = true,
            Available = true,
            Message = string.Join(", ", existing)
        };
    }

    public Task<MusicSearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var tracks = EnumerateTracks()
            .Where(t =>
                string.IsNullOrWhiteSpace(query) ||
                t.Display.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (t.LocalPath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(100)
            .ToList();

        var playlists = EnumeratePlaylists()
            .Where(p =>
                string.IsNullOrWhiteSpace(query) ||
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();

        return Task.FromResult(new MusicSearchResult
        {
            Source = Kind,
            Tracks = tracks,
            Playlists = playlists
        });
    }

    public Task<IReadOnlyList<MusicTrack>> GetPlaylistTracksAsync(string playlistIdOrUrl, CancellationToken ct = default)
    {
        var path = playlistIdOrUrl.StartsWith("local-playlist:", StringComparison.Ordinal)
            ? playlistIdOrUrl["local-playlist:".Length..]
            : playlistIdOrUrl;
        path = Expand(path);

        if (!File.Exists(path))
            throw new FileNotFoundException($"プレイリストが見つかりません: {path}");

        var tracks = ParseM3u(path).ToList();
        return Task.FromResult<IReadOnlyList<MusicTrack>>(tracks);
    }

    public IEnumerable<MusicTrack> EnumerateTracks()
    {
        foreach (var root in ExistingRoots())
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                if (!AudioExtensions.Contains(ext)) continue;
                yield return FromFile(file);
            }
        }
    }

    public IEnumerable<MusicPlaylist> EnumeratePlaylists()
    {
        foreach (var root in ExistingRoots())
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                if (ext is not (".m3u" or ".m3u8")) continue;
                var count = ParseM3u(file).Count();
                yield return new MusicPlaylist
                {
                    Id = $"local-playlist:{file}",
                    Name = Path.GetFileNameWithoutExtension(file),
                    Source = Kind,
                    ExternalUrl = null,
                    TrackCount = count,
                    Description = file
                };
            }
        }
    }

    private IEnumerable<string> ExistingRoots() =>
        _options.LibraryPaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Expand)
            .Where(Directory.Exists)
            .Distinct(StringComparer.Ordinal);

    private static string Expand(string path)
    {
        if (path.StartsWith('~'))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.TrimStart('~').TrimStart('/', '\\'));
        return Path.GetFullPath(path);
    }

    private static MusicTrack FromFile(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        string? artist = null;
        var title = name;
        var parts = name.Split(" - ", 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            artist = parts[0];
            title = parts[1];
        }

        return new MusicTrack
        {
            Id = $"local:{file}",
            Title = title,
            Artist = artist,
            Source = MusicSourceKind.Local,
            LocalPath = file,
            OfflineReady = true
        };
    }

    private static IEnumerable<MusicTrack> ParseM3u(string playlistPath)
    {
        var dir = Path.GetDirectoryName(playlistPath) ?? ".";
        string? pendingTitle = null;

        foreach (var raw in File.ReadLines(playlistPath))
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = line.IndexOf(',');
                pendingTitle = comma >= 0 ? line[(comma + 1)..].Trim() : null;
                continue;
            }

            if (line.StartsWith('#')) continue;

            var trackPath = line;
            if (!Path.IsPathRooted(trackPath) && !Regex.IsMatch(trackPath, @"^[a-z]+://", RegexOptions.IgnoreCase))
                trackPath = Path.GetFullPath(Path.Combine(dir, trackPath));

            if (trackPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                yield return new MusicTrack
                {
                    Id = $"local-url:{trackPath}",
                    Title = pendingTitle ?? trackPath,
                    Source = MusicSourceKind.Local,
                    ExternalUrl = trackPath,
                    OfflineReady = false
                };
            }
            else if (File.Exists(trackPath))
            {
                var track = FromFile(trackPath);
                if (!string.IsNullOrWhiteSpace(pendingTitle))
                {
                    yield return new MusicTrack
                    {
                        Id = track.Id,
                        Title = pendingTitle!,
                        Artist = track.Artist,
                        Album = track.Album,
                        Duration = track.Duration,
                        Source = track.Source,
                        ExternalUrl = track.ExternalUrl,
                        LocalPath = track.LocalPath,
                        OfflineReady = true
                    };
                }
                else
                {
                    yield return track;
                }
            }

            pendingTitle = null;
        }
    }
}
