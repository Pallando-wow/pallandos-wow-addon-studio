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

        var releaseDirectory =
            Path.GetDirectoryName(file.Path)!;

        Directory.CreateDirectory(releaseDirectory);

        await File.WriteAllTextAsync(
            file.Path,
            content,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        return file with { Exists = true };
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
