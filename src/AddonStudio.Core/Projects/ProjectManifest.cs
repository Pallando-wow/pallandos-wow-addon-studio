using AddonStudio.Core.Components;

namespace AddonStudio.Core.Projects;

public sealed record ProjectManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required ProjectIdentity Project { get; init; }

    public required RuntimeLayout Runtime { get; init; }

    public IReadOnlyList<ComponentUsage> Components { get; init; } = [];

    public ReleaseConfiguration? Release { get; init; }

    public CurseForgeConfiguration? CurseForge { get; init; }
}
