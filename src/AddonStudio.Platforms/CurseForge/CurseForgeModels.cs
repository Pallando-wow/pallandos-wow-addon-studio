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


public sealed record CurseForgeProject(
    int Id,
    int GameId,
    string Name,
    string Slug,
    string Summary,
    int Status,
    int PrimaryCategoryId,
    IReadOnlyList<int> CategoryIds)
{
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
