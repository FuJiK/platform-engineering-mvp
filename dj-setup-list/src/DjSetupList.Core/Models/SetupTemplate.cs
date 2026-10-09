namespace DjSetupList.Models;

public sealed class SetupTemplate
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<ChecklistCategory> Categories { get; init; }

    public SetupList CreateList(
        string eventName = "",
        string venue = "",
        DateOnly? eventDate = null,
        string? startTime = null,
        string? notes = null) =>
        new()
        {
            TemplateId = Id,
            TemplateName = Name,
            EventName = eventName,
            Venue = venue,
            EventDate = eventDate,
            StartTime = startTime,
            Notes = notes,
            Categories = Categories.Select(c => c.Clone()).ToList()
        };
}
