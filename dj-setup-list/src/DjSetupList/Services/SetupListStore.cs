using System.Text.Json;
using System.Text.Json.Serialization;
using DjSetupList.Models;

namespace DjSetupList.Services;

public static class SetupListStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public static void Save(SetupList list, string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(path, JsonSerializer.Serialize(list, Options));
    }

    public static SetupList Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"セットアップリストが見つかりません: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SetupList>(json, Options)
               ?? throw new InvalidOperationException($"JSONの読み込みに失敗しました: {path}");
    }
}
