namespace AddonStudio.Platforms.CurseForge;

public sealed record CurseForgePublicationRecord
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public required string Version { get; init; }

    public int ProjectId { get; init; }

    public int FileId { get; init; }

    public required string PackageFileName { get; init; }

    public required string ArtifactSha256 { get; init; }

    public IReadOnlyList<int> GameVersionIds { get; init; } = [];

    public required string ReleaseType { get; init; }

    public bool IsMarkedForManualRelease { get; init; }

    public DateTimeOffset UploadedAtUtc { get; init; }
}

public sealed record CurseForgeUploadExecutionResult(
    CurseForgeUploadResult Upload,
    string PublicationRecordPath);


public enum CurseForgePublicationState
{
    NotPublished,
    Published,
    LocalArtifactChanged
}

public sealed record CurseForgePublicationHistoryEntry(
    string Version,
    CurseForgePublicationState State,
    bool HasChangelog,
    bool HasArtifactManifest,
    IReadOnlyList<string> PackageFiles,
    int? ProjectId,
    int? FileId,
    string? PublishedPackageFileName,
    string? PublishedArtifactSha256,
    IReadOnlyList<int> GameVersionIds,
    string? ReleaseType,
    bool? IsMarkedForManualRelease,
    DateTimeOffset? UploadedAtUtc);
