using DjSetupList.Templates;

namespace DjSetupList.Tests;

public class TemplateTests
{
    [Fact]
    public void All_templates_have_unique_ids()
    {
        var ids = BuiltinTemplates.All.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("club")]
    [InlineData("mobile")]
    [InlineData("outdoor")]
    [InlineData("wedding")]
    [InlineData("usb-only")]
    public void Find_returns_known_templates(string id)
    {
        var template = BuiltinTemplates.Find(id);
        Assert.NotNull(template);
        Assert.Equal(id, template!.Id);
        Assert.NotEmpty(template.Categories);
        Assert.All(template.Categories, c => Assert.NotEmpty(c.Items));
    }

    [Fact]
    public void CreateList_copies_items_independently()
    {
        var template = BuiltinTemplates.Find("club")!;
        var a = template.CreateList("A");
        var b = template.CreateList("B");
        a.Categories[0].Items[0].Checked = true;

        Assert.False(b.Categories[0].Items[0].Checked);
        Assert.Equal("A", a.EventName);
        Assert.Equal("B", b.EventName);
    }
}
