using DjSetupList.Models;
using DjSetupList.Services;
using DjSetupList.Templates;

namespace DjSetupList.Web.Services;

public sealed class ListWorkspace
{
    private readonly string _root;
    private readonly object _gate = new();

    public ListWorkspace(IWebHostEnvironment env, IConfiguration config)
    {
        var configured = config["Workspace:Root"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "setup-lists")
            : configured;
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    public IReadOnlyList<SetupListSummary> ListAll()
    {
        lock (_gate)
        {
            return Directory.EnumerateFiles(_root, "*.json")
                .Select(path =>
                {
                    try
                    {
                        var list = SetupListStore.Load(path);
                        return new SetupListSummary(
                            Path.GetFileName(path),
                            path,
                            list.EventName,
                            list.Venue,
                            list.TemplateId,
                            list.EventDate,
                            list.CheckedItems,
                            list.TotalItems,
                            list.MusicTrackCount,
                            list.CreatedAt);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(x => x is not null)
                .Cast<SetupListSummary>()
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        }
    }

    public SetupList Get(string fileName)
    {
        var path = Resolve(fileName);
        lock (_gate) return SetupListStore.Load(path);
    }

    public (string FileName, SetupList List) Create(CreateListRequest request)
    {
        var template = BuiltinTemplates.Find(request.TemplateId)
            ?? throw new ArgumentException($"テンプレートが見つかりません: {request.TemplateId}");

        DateOnly? date = null;
        if (!string.IsNullOrWhiteSpace(request.Date) && DateOnly.TryParse(request.Date, out var parsed))
            date = parsed;

        var list = template.CreateList(
            eventName: request.EventName ?? "",
            venue: request.Venue ?? "",
            eventDate: date,
            startTime: request.StartTime,
            notes: request.Notes);

        if (!string.IsNullOrWhiteSpace(request.ExtraItems))
        {
            var extras = request.ExtraItems.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (extras.Length > 0)
            {
                var custom = new ChecklistCategory { Id = "custom", Title = "追加項目" };
                var i = 1;
                foreach (var name in extras)
                {
                    custom.Items.Add(new ChecklistItem
                    {
                        Id = $"custom-{i++}",
                        Name = name,
                        Essential = true
                    });
                }

                list.Categories.Add(custom);
            }
        }

        var slug = Slugify(string.IsNullOrWhiteSpace(list.EventName) ? list.TemplateId : list.EventName);
        var datePart = list.EventDate?.ToString("yyyyMMdd") ?? DateTime.UtcNow.ToString("yyyyMMdd");
        var fileName = $"{datePart}-{slug}.json";
        var path = Path.Combine(_root, fileName);
        lock (_gate) SetupListStore.Save(list, path);
        return (fileName, list);
    }

    public SetupList Save(string fileName, SetupList list)
    {
        var path = Resolve(fileName);
        lock (_gate)
        {
            SetupListStore.Save(list, path);
            return list;
        }
    }

    public SetupList ToggleItem(string fileName, string itemId, bool? checkedState = null)
    {
        lock (_gate)
        {
            var list = SetupListStore.Load(Resolve(fileName));
            var item = list.Categories.SelectMany(c => c.Items).FirstOrDefault(i => i.Id == itemId)
                ?? throw new ArgumentException($"項目が見つかりません: {itemId}");
            item.Checked = checkedState ?? !item.Checked;
            SetupListStore.Save(list, Resolve(fileName));
            return list;
        }
    }

    private string Resolve(string fileName)
    {
        var safe = Path.GetFileName(fileName);
        var path = Path.Combine(_root, safe);
        if (!path.StartsWith(_root, StringComparison.Ordinal))
            throw new InvalidOperationException("不正なパスです。");
        if (!File.Exists(path))
            throw new FileNotFoundException($"リストが見つかりません: {safe}");
        return path;
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
}

public sealed record SetupListSummary(
    string FileName,
    string Path,
    string EventName,
    string Venue,
    string TemplateId,
    DateOnly? EventDate,
    int CheckedItems,
    int TotalItems,
    int MusicTrackCount,
    DateTimeOffset CreatedAt);

public sealed class CreateListRequest
{
    public string TemplateId { get; set; } = "club";
    public string? EventName { get; set; }
    public string? Venue { get; set; }
    public string? Date { get; set; }
    public string? StartTime { get; set; }
    public string? Notes { get; set; }
    public string? ExtraItems { get; set; }
}
