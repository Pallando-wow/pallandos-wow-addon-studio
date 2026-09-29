namespace AddonStudio.Application.Projects;

public sealed record ProjectCatalogResult(
    IReadOnlyList<ProjectCatalogEntry> Projects,
    IReadOnlyList<UnmanagedProjectFolder> UnmanagedFolders,
    IReadOnlyList<ProjectCatalogIssue> Issues);
