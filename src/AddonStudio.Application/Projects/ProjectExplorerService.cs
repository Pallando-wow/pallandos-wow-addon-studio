using AddonStudio.Core.Projects;

namespace AddonStudio.Application.Projects;

public sealed class ProjectExplorerService
{
    private static readonly string[] IgnoredDirectoryNames =
    [
        ".git",
        ".idea",
        ".vs",
        "bin",
        "obj"
    ];

    public IReadOnlyList<ProjectTreeItem> BuildTree(
        ProjectCatalogEntry project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var projectDirectory =
            Path.GetFullPath(project.ProjectDirectory);

        if (!Directory.Exists(projectDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Project directory '{projectDirectory}' does not exist.");
        }

        var items = new List<ProjectTreeItem>();

        AddFileIfPresent(
            items,
            Path.Combine(
                projectDirectory,
                ProjectLayout.ManifestFileName));

        AddDirectoryIfPresent(
            items,
            Path.Combine(
                projectDirectory,
                ProjectLayout.RuntimeDirectoryName));

        AddDirectoryIfPresent(
            items,
            Path.Combine(
                projectDirectory,
                ProjectLayout.DocumentationDirectoryName));

        AddDirectoryIfPresent(
            items,
            Path.Combine(
                projectDirectory,
                ProjectLayout.ReleaseDirectoryName));

        AddDirectoryIfPresent(
            items,
            Path.Combine(
                projectDirectory,
                ProjectLayout.MediaDirectoryName));

        var knownNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ProjectLayout.ManifestFileName,
            ProjectLayout.RuntimeDirectoryName,
            ProjectLayout.DocumentationDirectoryName,
            ProjectLayout.ReleaseDirectoryName,
            ProjectLayout.MediaDirectoryName
        };

        foreach (var directory in Directory
                     .EnumerateDirectories(projectDirectory)
                     .Where(path =>
                         !knownNames.Contains(Path.GetFileName(path)) &&
                         !IsIgnoredDirectory(path))
                     .OrderBy(
                         path => Path.GetFileName(path),
                         StringComparer.OrdinalIgnoreCase))
        {
            items.Add(BuildDirectory(directory));
        }

        foreach (var file in Directory
                     .EnumerateFiles(projectDirectory)
                     .Where(path =>
                         !knownNames.Contains(Path.GetFileName(path)))
                     .OrderBy(
                         path => Path.GetFileName(path),
                         StringComparer.OrdinalIgnoreCase))
        {
            items.Add(BuildFile(file));
        }

        return items;
    }

    private static void AddFileIfPresent(
        ICollection<ProjectTreeItem> items,
        string path)
    {
        if (File.Exists(path))
        {
            items.Add(BuildFile(path));
        }
    }

    private static void AddDirectoryIfPresent(
        ICollection<ProjectTreeItem> items,
        string path)
    {
        if (Directory.Exists(path))
        {
            items.Add(BuildDirectory(path));
        }
    }

    private static ProjectTreeItem BuildDirectory(
        string directory)
    {
        var children = new List<ProjectTreeItem>();

        foreach (var childDirectory in Directory
                     .EnumerateDirectories(directory)
                     .Where(path => !IsIgnoredDirectory(path))
                     .OrderBy(
                         path => Path.GetFileName(path),
                         StringComparer.OrdinalIgnoreCase))
        {
            children.Add(
                BuildDirectory(childDirectory));
        }

        foreach (var file in Directory
                     .EnumerateFiles(directory)
                     .OrderBy(
                         path => Path.GetFileName(path),
                         StringComparer.OrdinalIgnoreCase))
        {
            children.Add(BuildFile(file));
        }

        return new ProjectTreeItem(
            Path.GetFileName(directory),
            directory,
            IsDirectory: true,
            children);
    }

    private static ProjectTreeItem BuildFile(
        string file) =>
        new(
            Path.GetFileName(file),
            file,
            IsDirectory: false,
            []);

    private static bool IsIgnoredDirectory(
        string directory) =>
        IgnoredDirectoryNames.Contains(
            Path.GetFileName(directory),
            StringComparer.OrdinalIgnoreCase);
}
