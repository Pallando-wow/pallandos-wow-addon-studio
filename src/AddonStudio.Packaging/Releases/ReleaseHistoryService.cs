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
                ReleaseVersionComparer.Instance)
            .ToArray();
    }
}


internal sealed class ReleaseVersionComparer :
    IComparer<string>
{
    public static ReleaseVersionComparer Instance { get; } =
        new();

    public int Compare(
        string? left,
        string? right)
    {
        if (ReferenceEquals(
                left,
                right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (Version.TryParse(
                left,
                out var leftVersion) &&
            Version.TryParse(
                right,
                out var rightVersion))
        {
            return leftVersion.CompareTo(
                rightVersion);
        }

        return StringComparer.OrdinalIgnoreCase.Compare(
            left,
            right);
    }
}
