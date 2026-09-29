namespace AddonStudio.Core.Components;

public sealed record ComponentUsage
{
    public required string Id { get; init; }

    public IReadOnlyList<ComponentPlacement> Targets { get; init; } = [];
}
