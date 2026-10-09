namespace DjSetupList.Models;

public sealed class ChecklistItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Note { get; init; }
    public bool Essential { get; init; } = true;
    public bool Checked { get; set; }

    public ChecklistItem Clone() => new()
    {
        Id = Id,
        Name = Name,
        Note = Note,
        Essential = Essential,
        Checked = Checked
    };
}
