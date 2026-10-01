namespace AddonStudio.Platforms.CurseForge;

public sealed record CurseForgeGame(
    int Id,
    string Name,
    string Slug);

public sealed record CurseForgeCategory(
    int Id,
    int GameId,
    string Name,
    string Slug,
    bool IsClass,
    int? ClassId,
    int? ParentCategoryId,
    int DisplayIndex);

public sealed record CurseForgeDataSnapshot(
    CurseForgeGame Game,
    IReadOnlyList<CurseForgeCategory> Categories);


public sealed record CurseForgeProjectCategory(
    int Id,
    string Name,
    string Slug,
    int? ClassId,
    int? ParentCategoryId);

public sealed record CurseForgeProject(
    int Id,
    int GameId,
    string Name,
    string Slug,
    string Summary,
    int Status,
    int PrimaryCategoryId,
    IReadOnlyList<CurseForgeProjectCategory> Categories)
{
    public IReadOnlyList<int> CategoryIds =>
        Categories
            .Select(category => category.Id)
            .ToArray();

    public CurseForgeProjectCategory? PrimaryCategory =>
        Categories.FirstOrDefault(
            category =>
                category.Id ==
                PrimaryCategoryId);

    public string StatusName =>
        Status switch
        {
            1 => "New",
            2 => "Changes Required",
            3 => "Under Soft Review",
            4 => "Approved",
            5 => "Rejected",
            6 => "Changes Made",
            7 => "Inactive",
            8 => "Abandoned",
            9 => "Deleted",
            10 => "Under Review",
            _ => $"Status {Status}"
        };
}


public sealed record CurseForgeGameVersionType(
    int Id,
    int GameId,
    string Name,
    string Slug);

public sealed record CurseForgeGameVersion(
    int Id,
    string Name,
    string Slug,
    int TypeId,
    string TypeName,
    string TypeSlug);

public sealed record CurseForgeGameVersionCatalog(
    IReadOnlyList<CurseForgeGameVersionType> Types,
    IReadOnlyList<CurseForgeGameVersion> Versions);

public enum CurseForgeFileReleaseType
{
    Release,
    Beta,
    Alpha
}

public enum CurseForgeChangelogMarkupType
{
    Text,
    Html,
    Markdown
}

public sealed record CurseForgeUploadPlan(
    int ProjectId,
    string PackagePath,
    string FileName,
    string DisplayName,
    string Changelog,
    CurseForgeChangelogMarkupType ChangelogType,
    IReadOnlyList<int> GameVersionIds,
    CurseForgeFileReleaseType ReleaseType,
    long FileLength,
    bool IsMarkedForManualRelease,
    string ArtifactSha256);


public sealed record CurseForgeUploadGameVersion(
    int Id,
    int GameVersionTypeId,
    string Name,
    string Slug);

public sealed record CurseForgeUploadResult(
    int FileId);

public static class CurseForgeUploadWireValues
{
    public static string ToWireValue(
        this CurseForgeFileReleaseType value) =>
        value switch
        {
            CurseForgeFileReleaseType.Release =>
                "release",
            CurseForgeFileReleaseType.Beta =>
                "beta",
            CurseForgeFileReleaseType.Alpha =>
                "alpha",
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(value))
        };

    public static string ToWireValue(
        this CurseForgeChangelogMarkupType value) =>
        value switch
        {
            CurseForgeChangelogMarkupType.Text =>
                "text",
            CurseForgeChangelogMarkupType.Html =>
                "html",
            CurseForgeChangelogMarkupType.Markdown =>
                "markdown",
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(value))
        };
}
