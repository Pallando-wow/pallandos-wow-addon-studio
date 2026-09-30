namespace AddonStudio.Core.Projects;

public sealed record CurseForgeConfiguration
{
    public string? ProjectId { get; init; }

    public string? Slug { get; init; }

    public string? MainCategoryId { get; init; }

    public IReadOnlyList<string> AdditionalCategoryIds { get; init; } = [];

    public string? License { get; init; }

    public bool? AllowDistribution { get; init; }
}
