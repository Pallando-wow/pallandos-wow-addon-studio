using AddonStudio.Core.Projects;
using AddonStudio.Core.Publishing;

namespace AddonStudio.Packaging.Releases;

public sealed record ReleaseHistoryEntry(
    string Version,
    string DirectoryPath,
    bool HasChangelog,
    IReadOnlyList<string> PackageFiles);

public sealed class ReleaseHistoryService
{
    public IReadOnlyList<ReleaseHistoryEntry> GetHistory(
        string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);

        var projectPath =
            Path.GetFullPath(
                projectDirectory);

        var versionsDirectory =
            Path.Combine(
                projectPath,
                ProjectLayout.ReleaseDirectoryName,
                PublishingContentLayout.VersionsDirectoryName);

        if (!Directory.Exists(
                versionsDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateDirectories(
                versionsDirectory,
                "*",
                SearchOption.TopDirectoryOnly)
            .Select(directory =>
                new ReleaseHistoryEntry(
                    Path.GetFileName(
                        directory),
                    directory,
                    File.Exists(
                        Path.Combine(
                            directory,
                            PublishingContentLayout.GetFileName(
                                PublishingContentKind.Changelog))),
                    Directory
                        .EnumerateFiles(
                            directory,
                            "*.zip",
                            SearchOption.TopDirectoryOnly)
                        .OrderBy(
                            path => path,
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray()))
            .OrderByDescending(
                entry => entry.Version,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
