using DjSetupList.Models;
using DjSetupList.Music;
using DjSetupList.Services;
using DjSetupList.Templates;

namespace DjSetupList.Cli;

public static class App
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || IsHelp(args))
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        var command = args[0].ToLowerInvariant();
        var parser = new ArgParser(args.Skip(1).ToArray());

        try
        {
            return command switch
            {
                "templates" or "list" => TemplatesCommand(),
                "new" => NewCommand(parser),
                "show" => ShowCommand(parser),
                "check" => CheckCommand(parser, check: true),
                "uncheck" => CheckCommand(parser, check: false),
                "export" => ExportCommand(parser),
                "summary" => SummaryCommand(parser),
                "sources" => SourcesCommand(),
                "music-search" => MusicSearchCommand(parser),
                "music-attach" => MusicAttachCommand(parser),
                "doctor" => DoctorCommand(),
                _ => Unknown(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"エラー: {ex.Message}");
            return 1;
        }
    }

    private static bool IsHelp(string[] args) =>
        args.Any(a => a is "-h" or "--help" or "help");

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"不明なコマンド: {command}");
        PrintHelp();
        return 1;
    }

    private static int TemplatesCommand()
    {
        Console.WriteLine("利用可能なテンプレート:");
        Console.WriteLine();
        foreach (var t in BuiltinTemplates.All)
        {
            var items = t.Categories.Sum(c => c.Items.Count);
            Console.WriteLine($"  {t.Id,-12} {t.Name}");
            Console.WriteLine($"               {t.Description}（{items}項目 / {t.Categories.Count}カテゴリ）");
            Console.WriteLine();
        }

        return 0;
    }

    private static int NewCommand(ArgParser parser)
    {
        var templateId = parser.Get("template") ?? parser.Positionals.ElementAtOrDefault(0)
            ?? throw new ArgumentException("テンプレートを指定してください（例: --template club）");

        var template = BuiltinTemplates.Find(templateId)
            ?? throw new ArgumentException($"テンプレートが見つかりません: {templateId}");

        DateOnly? date = null;
        if (parser.Get("date") is { } dateRaw)
        {
            if (!DateOnly.TryParse(dateRaw, out var parsed))
                throw new ArgumentException($"日付の形式が不正です: {dateRaw}（YYYY-MM-DD）");
            date = parsed;
        }

        var list = template.CreateList(
            eventName: parser.GetOrDefault("event", ""),
            venue: parser.GetOrDefault("venue", ""),
            eventDate: date,
            startTime: parser.Get("time"),
            notes: parser.Get("notes"));

        if (parser.Has("extra"))
            AddExtraItems(list, parser.GetRequired("extra"));

        var format = SetupListExporter.ParseFormat(parser.GetOrDefault("format", "md"));
        var output = parser.Get("output");
        var jsonPath = parser.Get("save") ?? DeriveJsonPath(output, list);

        SetupListStore.Save(list, jsonPath);

        var rendered = SetupListExporter.Export(list, format);
        if (output is null)
        {
            Console.Write(rendered);
        }
        else
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(output));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(output, rendered);
            Console.WriteLine($"書き出し: {output}");
        }

        Console.Error.WriteLine($"保存: {jsonPath}（check / export 用）");
        Console.Error.WriteLine($"項目数: {list.TotalItems}（必須未チェック想定: {list.EssentialMissing}）");
        return 0;
    }

    private static void AddExtraItems(SetupList list, string csv)
    {
        var extras = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (extras.Length == 0) return;

        var custom = list.Categories.FirstOrDefault(c => c.Id == "custom");
        if (custom is null)
        {
            custom = new ChecklistCategory { Id = "custom", Title = "追加項目" };
            list.Categories.Add(custom);
        }

        var index = 1;
        foreach (var name in extras)
        {
            custom.Items.Add(new ChecklistItem
            {
                Id = $"custom-{index++}",
                Name = name,
                Essential = true
            });
        }
    }

    private static string DeriveJsonPath(string? output, SetupList list)
    {
        if (!string.IsNullOrWhiteSpace(output))
            return Path.ChangeExtension(output, ".json");

        var slug = Slugify(string.IsNullOrWhiteSpace(list.EventName) ? list.TemplateId : list.EventName);
        var datePart = list.EventDate?.ToString("yyyyMMdd") ?? DateTime.UtcNow.ToString("yyyyMMdd");
        return Path.Combine("setup-lists", $"{datePart}-{slug}.json");
    }

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(slug.Trim('-')) ? "setup" : slug.Trim('-');
    }

    private static int ShowCommand(ArgParser parser)
    {
        var path = RequireListPath(parser);
        var list = SetupListStore.Load(path);
        var format = SetupListExporter.ParseFormat(parser.GetOrDefault("format", "txt"));
        Console.Write(SetupListExporter.Export(list, format));
        return 0;
    }

    private static int CheckCommand(ArgParser parser, bool check)
    {
        var path = RequireListPath(parser);
        var list = SetupListStore.Load(path);
        var query = parser.Get("item") ?? parser.Positionals.ElementAtOrDefault(1)
            ?? throw new ArgumentException("項目名またはIDを --item で指定してください。");

        var matches = list.Categories
            .SelectMany(c => c.Items.Select(i => (Category: c, Item: i)))
            .Where(x =>
                x.Item.Id.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                x.Item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            throw new ArgumentException($"項目が見つかりません: {query}");

        // Prefer exact id/name match when the query is ambiguous.
        var exact = matches
            .Where(x =>
                x.Item.Id.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                x.Item.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exact.Count == 1)
            matches = exact;

        if (matches.Count > 1 && !parser.Has("all"))
        {
            Console.Error.WriteLine("複数ヒットしました。--all で一括、またはより具体的な名前/IDを指定してください:");
            foreach (var m in matches)
                Console.Error.WriteLine($"  {m.Item.Id}: {m.Category.Title} / {m.Item.Name}");
            return 1;
        }

        foreach (var m in matches)
            m.Item.Checked = check;

        SetupListStore.Save(list, path);
        var verb = check ? "チェック" : "チェック解除";
        Console.WriteLine($"{verb}: {string.Join(", ", matches.Select(m => m.Item.Name))}");
        Console.WriteLine($"進捗: {list.CheckedItems}/{list.TotalItems}（必須未完了: {list.EssentialMissing}）");
        return 0;
    }

    private static int ExportCommand(ArgParser parser)
    {
        var path = RequireListPath(parser);
        var list = SetupListStore.Load(path);
        var format = SetupListExporter.ParseFormat(parser.GetOrDefault("format", "md"));
        var rendered = SetupListExporter.Export(list, format);
        var output = parser.Get("output");
        if (output is null)
        {
            Console.Write(rendered);
        }
        else
        {
            File.WriteAllText(output, rendered);
            Console.WriteLine($"書き出し: {output}");
        }

        return 0;
    }

    private static int SummaryCommand(ArgParser parser)
    {
        var path = RequireListPath(parser);
        var list = SetupListStore.Load(path);
        Console.WriteLine($"イベント: {list.EventName}");
        Console.WriteLine($"会場: {list.Venue}");
        Console.WriteLine($"テンプレート: {list.TemplateName}");
        Console.WriteLine($"進捗: {list.CheckedItems}/{list.TotalItems}");
        Console.WriteLine($"必須未完了: {list.EssentialMissing}");
        Console.WriteLine();
        Console.WriteLine("未チェック（必須）:");
        foreach (var category in list.Categories)
        {
            foreach (var item in category.Items.Where(i => i.Essential && !i.Checked))
                Console.WriteLine($"  - [{category.Title}] {item.Name}");
        }

        return 0;
    }

    private static int SourcesCommand()
    {
        var catalog = MusicSourceCatalog.FromEnvironment();
        Console.WriteLine("音源プロバイダ:");
        Console.WriteLine();
        foreach (var status in catalog.Statuses())
        {
            var mark = status.Available ? "✓" : "·";
            Console.WriteLine($"  {mark} {status.DisplayName,-12} configured={status.Configured} available={status.Available}");
            if (!string.IsNullOrWhiteSpace(status.Message))
                Console.WriteLine($"               {status.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("※ テンプレート自体は静的チェックリストです。音源は Local / Spotify / SoundCloud / Apple Music から別途検索・添付します。");
        return 0;
    }

    private static int MusicSearchCommand(ArgParser parser)
    {
        var sourceName = parser.Get("source") ?? parser.Positionals.ElementAtOrDefault(0)
            ?? throw new ArgumentException("--source local|spotify|soundcloud|applemusic を指定してください。");
        var query = parser.Get("query") ?? parser.Positionals.ElementAtOrDefault(1) ?? "";
        var catalog = MusicSourceCatalog.FromEnvironment();
        var source = catalog.TryGet(sourceName)
            ?? throw new ArgumentException($"未知の音源: {sourceName}");

        var result = source.SearchAsync(query).GetAwaiter().GetResult();
        if (!string.IsNullOrWhiteSpace(result.Warning))
            Console.WriteLine($"警告: {result.Warning}");

        Console.WriteLine($"[{source.DisplayName}] tracks={result.Tracks.Count} playlists={result.Playlists.Count}");
        foreach (var pl in result.Playlists.Take(20))
            Console.WriteLine($"  playlist  {pl.Id}  {pl.Name} ({pl.TrackCount?.ToString() ?? "?"}曲)");
        foreach (var tr in result.Tracks.Take(30))
            Console.WriteLine($"  track     {tr.Id}  {tr.Display}");
        return 0;
    }

    private static int MusicAttachCommand(ArgParser parser)
    {
        var path = RequireListPath(parser);
        var sourceName = parser.GetRequired("source");
        var playlist = parser.Get("playlist") ?? parser.Get("url")
            ?? throw new ArgumentException("--playlist <id|path|url> を指定してください。");
        var catalog = MusicSourceCatalog.FromEnvironment();
        var source = catalog.TryGet(sourceName)
            ?? throw new ArgumentException($"未知の音源: {sourceName}");

        var tracks = source.GetPlaylistTracksAsync(playlist).GetAwaiter().GetResult();
        var list = SetupListStore.Load(path);
        list.Music.Add(new MusicAttachment
        {
            Source = source.Kind,
            PlaylistId = playlist,
            PlaylistName = parser.Get("name") ?? playlist,
            PlaylistUrl = playlist.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? playlist : null,
            Tracks = tracks.ToList()
        });
        SetupListStore.Save(list, path);
        Console.WriteLine($"添付: {source.DisplayName} / {tracks.Count}曲（オフライン可 {tracks.Count(t => t.OfflineReady)}）");
        return 0;
    }

    private static int DoctorCommand()
    {
        Console.WriteLine("=== DJ Setup List — .NET Doctor ===");
        Console.WriteLine();
        Console.WriteLine($".NET: {Environment.Version}");
        Console.WriteLine($"OS: {Environment.OSVersion}");
        Console.WriteLine($"RID hint: {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");
        Console.WriteLine($"Templates: {BuiltinTemplates.All.Count}");
        foreach (var t in BuiltinTemplates.All)
            Console.WriteLine($"  - {t.Id}");
        Console.WriteLine();
        Console.WriteLine("Music sources:");
        foreach (var status in MusicSourceCatalog.FromEnvironment().Statuses())
        {
            var mark = status.Available ? "✓" : "·";
            Console.WriteLine($"  {mark} {status.DisplayName}: {status.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("OK");
        return 0;
    }

    private static string RequireListPath(ArgParser parser)
    {
        var path = parser.Get("list") ?? parser.Positionals.ElementAtOrDefault(0)
            ?? throw new ArgumentException("リストJSONを指定してください（例: --list setup-lists/foo.json）");
        return path;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            dj-setup-list — DJセットアップリスト補助ツール

            使い方:
              dj-setup-list templates
              dj-setup-list new --template <id> [options]
              dj-setup-list show --list <file.json> [--format txt|md|json]
              dj-setup-list check --list <file.json> --item <名前|ID> [--all]
              dj-setup-list uncheck --list <file.json> --item <名前|ID> [--all]
              dj-setup-list export --list <file.json> [--format md|txt|json] [-o out]
              dj-setup-list summary --list <file.json>
              dj-setup-list sources
              dj-setup-list music-search --source local|spotify|soundcloud|applemusic --query "..."
              dj-setup-list music-attach --list <file.json> --source local --playlist /path/to/set.m3u
              dj-setup-list doctor

            new オプション:
              -t, --template   club | mobile | outdoor | wedding | usb-only
              -e, --event      イベント名
              -v, --venue      会場名
                  --date       YYYY-MM-DD
                  --time       開始時刻（例: 22:00）
                  --notes      メモ
                  --extra      カンマ区切りの追加項目
              -f, --format     md | txt | json（既定: md）
              -o, --output     書き出し先ファイル
                  --save       編集用JSONの保存先（省略時は setup-lists/ 配下）

            例:
              dj-setup-list new -t club -e "Friday Night" -v "Club Neon" --date 2026-10-15 -o friday.md
              dj-setup-list check --list setup-lists/20261015-friday-night.json --item USB
              dj-setup-list summary --list setup-lists/20261015-friday-night.json
            """);
    }
}
