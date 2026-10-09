namespace DjSetupList.Cli;

public sealed class ArgParser
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positionals = [];

    public IReadOnlyList<string> Positionals => _positionals;

    public ArgParser(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var key = arg[2..];
                string? value = null;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    value = args[++i];
                }

                _options[key] = value ?? "true";
            }
            else if (arg.StartsWith("-", StringComparison.Ordinal) && arg.Length == 2)
            {
                var key = arg[1] switch
                {
                    'o' => "output",
                    't' => "template",
                    'e' => "event",
                    'v' => "venue",
                    'f' => "format",
                    'h' => "help",
                    _ => arg[1..]
                };
                string? value = null;
                if (key is not "help" && i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    value = args[++i];
                _options[key] = value ?? "true";
            }
            else
            {
                _positionals.Add(arg);
            }
        }
    }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) =>
        _options.TryGetValue(name, out var value) ? value : null;

    public string GetRequired(string name) =>
        Get(name) ?? throw new ArgumentException($"必須オプション --{name} がありません。");

    public string GetOrDefault(string name, string defaultValue) =>
        Get(name) ?? defaultValue;
}
