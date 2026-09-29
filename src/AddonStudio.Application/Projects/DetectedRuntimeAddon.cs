namespace AddonStudio.Application.Projects;

public sealed record DetectedRuntimeAddon(
    string Name,
    string SourcePath,
    IReadOnlyList<string> TocFiles);
