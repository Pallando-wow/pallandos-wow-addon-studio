namespace AddonStudio.Application.Projects;

public sealed record CreateAddonProjectRequest(
    string Name,
    string DestinationDirectory);
