namespace AddonStudio.Application.Projects;

public sealed record ProjectOperationResult(
    string ProjectDirectory,
    string ProjectName,
    IReadOnlyList<string> RuntimeAddons);
