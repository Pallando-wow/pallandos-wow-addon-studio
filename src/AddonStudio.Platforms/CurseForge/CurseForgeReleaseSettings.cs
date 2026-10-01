namespace AddonStudio.Platforms.CurseForge;

public sealed record CurseForgeReleaseSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public required string Version { get; init; }

    public IReadOnlyList<int> GameVersionIds { get; init; } = [];

    public CurseForgeFileReleaseType ReleaseType { get; init; } =
        CurseForgeFileReleaseType.Release;

    public bool IsMarkedForManualRelease { get; init; }

    public string? DisplayName { get; init; }
}
