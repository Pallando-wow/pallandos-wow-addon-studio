namespace AddonStudio.Core.Components;

public sealed record ComponentSource
{
    public required ComponentSourceType Type { get; init; }

    public required string Location { get; init; }

    public string? Reference { get; init; }
}
