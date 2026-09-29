namespace AddonStudio.Core.Wow;

public sealed class TocDocument
{
    private readonly IReadOnlyDictionary<string, string> metadata;

    public TocDocument(
        string filePath,
        IReadOnlyList<TocLine> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(lines);

        FilePath = Path.GetFullPath(filePath);
        Lines = lines;

        var values = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (line.Kind != TocLineKind.Metadata ||
                string.IsNullOrWhiteSpace(line.Key))
            {
                continue;
            }

            values[line.Key] = line.Value ?? string.Empty;
        }

        metadata = values;
    }

    public string FilePath { get; }

    public IReadOnlyList<TocLine> Lines { get; }

    public IReadOnlyDictionary<string, string> Metadata =>
        metadata;

    public IReadOnlyList<string> Files =>
        Lines
            .Where(line => line.Kind == TocLineKind.File)
            .Select(line => line.Value ?? string.Empty)
            .Where(value => value.Length > 0)
            .ToArray();

    public string? Title => GetMetadata("Title");

    public string? Notes => GetMetadata("Notes");

    public string? Version => GetMetadata("Version");

    public IReadOnlyList<string> Interfaces =>
        GetListMetadata("Interface");

    public IReadOnlyList<string> Dependencies =>
        GetListMetadata("Dependencies");

    public IReadOnlyList<string> OptionalDependencies =>
        GetListMetadata("OptionalDeps");

    public IReadOnlyList<string> SavedVariables =>
        GetListMetadata("SavedVariables");

    public IReadOnlyList<string> SavedVariablesPerCharacter =>
        GetListMetadata("SavedVariablesPerCharacter");

    public string? GetMetadata(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return metadata.TryGetValue(
            key,
            out var value)
            ? value
            : null;
    }

    public IReadOnlyList<string> GetListMetadata(
        string key)
    {
        var value = GetMetadata(key);

        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .ToArray();
    }
}
