using DjSetupList.Cli;

namespace DjSetupList.Tests;

public class AppCliTests
{
    [Fact]
    public void Templates_command_exits_zero()
    {
        var code = App.Run(["templates"]);
        Assert.Equal(0, code);
    }

    [Fact]
    public void Doctor_command_exits_zero()
    {
        var code = App.Run(["doctor"]);
        Assert.Equal(0, code);
    }

    [Fact]
    public void New_and_check_and_summary_flow_works()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dj-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var json = Path.Combine(dir, "list.json");
        var md = Path.Combine(dir, "list.md");

        try
        {
            var newCode = App.Run([
                "new",
                "--template", "club",
                "--event", "CLI Flow",
                "--venue", "Lab",
                "--date", "2026-10-15",
                "--extra", "ゴーグル,予備シャツ",
                "--save", json,
                "--output", md
            ]);
            Assert.Equal(0, newCode);
            Assert.True(File.Exists(json));
            Assert.True(File.Exists(md));
            Assert.Contains("ゴーグル", File.ReadAllText(md));

            var checkCode = App.Run(["check", "--list", json, "--item", "USB（メイン）"]);
            Assert.Equal(0, checkCode);

            var exactNameCode = App.Run(["check", "--list", json, "--item", "ヘッドフォン"]);
            Assert.Equal(0, exactNameCode);

            var summaryCode = App.Run(["summary", "--list", json]);
            Assert.Equal(0, summaryCode);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
