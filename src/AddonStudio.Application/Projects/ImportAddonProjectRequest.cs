namespace AddonStudio.Application.Projects;

public sealed record ImportAddonProjectRequest(
    string SourceDirectory,
    string DestinationDirectory,
    string? ProjectName = null);
