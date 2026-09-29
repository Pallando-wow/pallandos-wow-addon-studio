namespace AddonStudio.Core.Components;

public sealed record ComponentDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required ComponentKind Kind { get; init; }

    public required ComponentSource Source { get; init; }
}
