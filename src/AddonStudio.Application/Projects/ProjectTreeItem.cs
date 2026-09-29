namespace AddonStudio.Application.Projects;

public sealed record ProjectTreeItem(
    string Name,
    string FullPath,
    bool IsDirectory,
    IReadOnlyList<ProjectTreeItem> Children);
