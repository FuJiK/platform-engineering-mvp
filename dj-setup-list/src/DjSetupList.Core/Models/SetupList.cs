namespace DjSetupList.Models;

public sealed class SetupList
{
    public required string TemplateId { get; init; }
    public required string TemplateName { get; init; }
    public string EventName { get; set; } = "";
    public string Venue { get; set; } = "";
    public DateOnly? EventDate { get; set; }
    public string? StartTime { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<ChecklistCategory> Categories { get; init; } = [];
    public List<MusicAttachment> Music { get; init; } = [];

    public int TotalItems => Categories.Sum(c => c.Items.Count);
    public int CheckedItems => Categories.Sum(c => c.Items.Count(i => i.Checked));
    public int EssentialMissing => Categories.Sum(c => c.Items.Count(i => i.Essential && !i.Checked));
    public int MusicTrackCount => Music.Sum(m => m.TrackCount);

    public SetupList Clone() => new()
    {
        TemplateId = TemplateId,
        TemplateName = TemplateName,
        EventName = EventName,
        Venue = Venue,
        EventDate = EventDate,
        StartTime = StartTime,
        Notes = Notes,
        CreatedAt = CreatedAt,
        Categories = Categories.Select(c => c.Clone()).ToList(),
        Music = Music.Select(m => m.Clone()).ToList()
    };
}
