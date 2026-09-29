namespace AddonStudio.Application.Projects;

public sealed record AddonSourceInspection(
    string SourcePath,
    string SuggestedProjectName,
    IReadOnlyList<DetectedRuntimeAddon> RuntimeAddons);
