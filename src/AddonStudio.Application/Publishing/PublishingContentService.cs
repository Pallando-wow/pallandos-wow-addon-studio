using System.Text;
using AddonStudio.Core.Projects;
using AddonStudio.Core.Publishing;

namespace AddonStudio.Application.Publishing;

public sealed class PublishingContentService
{
    public PublishingContentFile Resolve(
        string projectDirectory,
        PublishingContentKind kind)
    {
        var projectPath =
            RequireProjectDirectory(projectDirectory);
        var fileName =
            PublishingContentLayout.GetFileName(kind);

        var path = Path.Combine(
            projectPath,
            ProjectLayout.ReleaseDirectoryName,
            fileName);

        return new PublishingContentFile(
            kind,
            path,
            File.Exists(path));
    }

    public PublishingContentFile ResolveReleaseChangelog(
        string projectDirectory,
        string version)
    {
        var projectPath =
            RequireProjectDirectory(
                projectDirectory);

        var relativePath =
            PublishingContentLayout
                .GetReleaseChangelogRelativePath(
                    version);

        var path = Path.Combine(
            projectPath,
            ProjectLayout.ReleaseDirectoryName,
            relativePath);

        return new PublishingContentFile(
            PublishingContentKind.Changelog,
            path,
            File.Exists(path));
    }

    public IReadOnlyList<PublishingContentFile> ResolveAll(
        string projectDirectory) =>
        Enum.GetValues<PublishingContentKind>()
            .Select(kind => Resolve(
                projectDirectory,
                kind))
            .ToArray();

    public async Task<string> ReadAsync(
        string projectDirectory,
        PublishingContentKind kind,
        CancellationToken cancellationToken = default)
    {
        var file = Resolve(
            projectDirectory,
            kind);

        if (!file.Exists)
        {
            return string.Empty;
        }

        return await File.ReadAllTextAsync(
            file.Path,
            cancellationToken);
    }

    public async Task<string> ReadReleaseChangelogAsync(
        string projectDirectory,
        string version,
        CancellationToken cancellationToken = default)
    {
        var releaseFile =
            ResolveReleaseChangelog(
                projectDirectory,
                version);

        if (releaseFile.Exists)
        {
            return await File.ReadAllTextAsync(
                releaseFile.Path,
                cancellationToken);
        }

        var legacyFile =
            Resolve(
                projectDirectory,
                PublishingContentKind.Changelog);

        if (!legacyFile.Exists)
        {
            return string.Empty;
        }

        return await File.ReadAllTextAsync(
            legacyFile.Path,
            cancellationToken);
    }

    public bool IsReleaseChangelogUsingLegacyFallback(
        string projectDirectory,
        string version)
    {
        var releaseFile =
            ResolveReleaseChangelog(
                projectDirectory,
                version);

        if (releaseFile.Exists)
        {
            return false;
        }

        return Resolve(
            projectDirectory,
            PublishingContentKind.Changelog)
            .Exists;
    }

    public async Task<PublishingContentFile> WriteAsync(
        string projectDirectory,
        PublishingContentKind kind,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var file = Resolve(
            projectDirectory,
            kind);

        return await WriteFileAsync(
            file,
            content,
            cancellationToken);
    }

    public async Task<PublishingContentFile> WriteReleaseChangelogAsync(
        string projectDirectory,
        string version,
        string content,
        bool removeLegacyFile = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var file =
            ResolveReleaseChangelog(
                projectDirectory,
                version);

        var writtenFile =
            await WriteFileAsync(
                file,
                content,
                cancellationToken);

        if (removeLegacyFile)
        {
            var legacyFile =
                Resolve(
                    projectDirectory,
                    PublishingContentKind.Changelog);

            if (legacyFile.Exists &&
                !string.Equals(
                    legacyFile.Path,
                    writtenFile.Path,
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(
                    legacyFile.Path);
            }
        }

        return writtenFile;
    }

    private static async Task<PublishingContentFile> WriteFileAsync(
        PublishingContentFile file,
        string content,
        CancellationToken cancellationToken)
    {
        var directory =
            Path.GetDirectoryName(
                file.Path)!;

        Directory.CreateDirectory(
            directory);

        await File.WriteAllTextAsync(
            file.Path,
            content,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        return file with
        {
            Exists = true
        };
    }

    private static string RequireProjectDirectory(
        string? projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);

        var fullPath = Path.GetFullPath(
            projectDirectory);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Project directory '{fullPath}' does not exist.");
        }

        if (!File.Exists(
                Path.Combine(
                    fullPath,
                    ProjectLayout.ManifestFileName)))
        {
            throw new InvalidOperationException(
                $"Directory '{fullPath}' is not a managed Studio project.");
        }

        return fullPath;
    }
}
