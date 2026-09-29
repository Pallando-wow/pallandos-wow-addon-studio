namespace AddonStudio.Core.Projects;

public sealed record ProjectIdentity
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public ProjectType Type { get; init; }
}
