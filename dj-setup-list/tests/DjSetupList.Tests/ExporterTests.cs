using DjSetupList.Services;
using DjSetupList.Templates;

namespace DjSetupList.Tests;

public class ExporterTests
{
    [Fact]
    public void Markdown_contains_event_and_checkboxes()
    {
        var list = BuiltinTemplates.Find("usb-only")!.CreateList(
            eventName: "Smoke Test Night",
            venue: "Test Venue",
            eventDate: new DateOnly(2026, 10, 15));

        var md = SetupListExporter.Export(list, ExportFormat.Markdown);

        Assert.Contains("Smoke Test Night", md);
        Assert.Contains("Test Venue", md);
        Assert.Contains("[ ]", md);
        Assert.Contains("USB（メイン）", md);
    }

    [Fact]
    public void Json_roundtrips_through_store()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dj-setup-{Guid.NewGuid():N}.json");
        try
        {
            var original = BuiltinTemplates.Find("club")!.CreateList("Roundtrip", "Club X");
            original.Categories[0].Items[0].Checked = true;
            SetupListStore.Save(original, path);

            var loaded = SetupListStore.Load(path);
            Assert.Equal(original.EventName, loaded.EventName);
            Assert.Equal(original.Venue, loaded.Venue);
            Assert.True(loaded.Categories[0].Items[0].Checked);
            Assert.Equal(original.TotalItems, loaded.TotalItems);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [InlineData("md", ExportFormat.Markdown)]
    [InlineData("txt", ExportFormat.Text)]
    [InlineData("json", ExportFormat.Json)]
    public void ParseFormat_accepts_aliases(string raw, ExportFormat expected)
    {
        Assert.Equal(expected, SetupListExporter.ParseFormat(raw));
    }
}
