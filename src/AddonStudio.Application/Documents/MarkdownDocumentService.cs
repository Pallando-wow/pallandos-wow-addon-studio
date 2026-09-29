using System.Text;
using AddonStudio.Core.Projects;

namespace AddonStudio.Application.Documents;

public sealed class MarkdownDocumentService
{
    public async Task<MarkdownDocument> OpenAsync(
        string projectDirectory,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var path = ValidatePath(
            projectDirectory,
            filePath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Markdown file does not exist.",
                path);
        }

        var text = await File.ReadAllTextAsync(
            path,
            cancellationToken);

        return new MarkdownDocument(
            path,
            Path.GetFileName(path),
            text);
    }

    public async Task SaveAsync(
        string projectDirectory,
        string filePath,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var path = ValidatePath(
            projectDirectory,
            filePath);

        await File.WriteAllTextAsync(
            path,
            text,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string ValidatePath(
        string projectDirectory,
        string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        var projectPath = Path.GetFullPath(
            projectDirectory);

        if (!Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException(
                $"Project directory '{projectPath}' does not exist.");
        }

        if (!File.Exists(
                Path.Combine(
                    projectPath,
                    ProjectLayout.ManifestFileName)))
        {
            throw new InvalidOperationException(
                $"Directory '{projectPath}' is not a managed Studio project.");
        }

        var path = Path.GetFullPath(filePath);
        var relativePath = Path.GetRelativePath(
            projectPath,
            path);

        if (relativePath.Equals(
                "..",
                StringComparison.Ordinal) ||
            relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) ||
            relativePath.StartsWith(
                $"..{Path.AltDirectorySeparatorChar}",
                StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException(
                "Markdown files must be located inside the current Studio project.");
        }

        if (!string.Equals(
                Path.GetExtension(path),
                ".md",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only Markdown (.md) files can be opened by the Markdown editor.");
        }

        return path;
    }
}
