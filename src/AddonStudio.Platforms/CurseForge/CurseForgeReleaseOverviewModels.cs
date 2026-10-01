namespace AddonStudio.Platforms.CurseForge;

public sealed record CurseForgeReleaseOverviewEntry(
    string Version,
    CurseForgePublicationState PublicationState,
    bool HasChangelog,
    bool HasArtifactManifest,
    bool HasReleaseSettings,
    bool IsReadyForUpload,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> PackageFiles,
    IReadOnlyList<int> GameVersionIds,
    CurseForgeFileReleaseType? ReleaseType,
    bool? IsMarkedForManualRelease,
    string? DisplayName,
    int? ProjectId,
    int? FileId,
    DateTimeOffset? UploadedAtUtc);
