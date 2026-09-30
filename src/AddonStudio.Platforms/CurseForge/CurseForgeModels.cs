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
