namespace AddonStudio.Core.Projects;

public sealed record CurseForgeConfiguration
{
    public string? ProjectId { get; init; }

    public string? Slug { get; init; }
}
