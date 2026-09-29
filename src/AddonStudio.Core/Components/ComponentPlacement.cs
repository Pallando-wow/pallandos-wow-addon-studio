namespace AddonStudio.Core.Components;

public sealed record ComponentPlacement
{
    public required string RuntimeAddon { get; init; }

    public required string Path { get; init; }
}
