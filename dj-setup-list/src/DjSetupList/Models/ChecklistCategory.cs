namespace DjSetupList.Models;

public sealed class ChecklistCategory
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public List<ChecklistItem> Items { get; init; } = [];

    public ChecklistCategory Clone() => new()
    {
        Id = Id,
        Title = Title,
        Items = Items.Select(i => i.Clone()).ToList()
    };
}
