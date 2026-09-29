namespace AddonStudio.Core.Projects;

public sealed record RuntimeLayout
{
    public string? PrimaryAddon { get; init; }

    public IReadOnlyList<string> Addons { get; init; } = [];
}
