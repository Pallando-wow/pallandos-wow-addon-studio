using AddonStudio.Core.Projects;

namespace AddonStudio.Media;

public sealed class ProjectMediaService
{
    private static readonly string[] ScreenshotExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg"
    ];

    public ProjectMediaSnapshot GetSnapshot(
        string projectDirectory)
    {
        var projectPath =
            RequireProjectDirectory(projectDirectory);

        var logoDirectory = Path.Combine(
            projectPath,
            ProjectLayout.MediaDirectoryName,
            ProjectLayout.LogoDirectoryName);

        var screenshotsDirectory = Path.Combine(
            projectPath,
            ProjectLayout.MediaDirectoryName,
            ProjectLayout.ScreenshotsDirectoryName);

        var logoFilePath =
            Directory.Exists(logoDirectory)
                ? Directory
                    .EnumerateFiles(
                        logoDirectory,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .Where(path =>
                        string.Equals(
                            Path.GetExtension(path),
                            ".png",
                            StringComparison.OrdinalIgnoreCase))
                    .OrderBy(
                        path => Path.GetFileName(path),
                        StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault()
                : null;

        var screenshotFilePaths =
            Directory.Exists(screenshotsDirectory)
                ? Directory
                    .EnumerateFiles(
                        screenshotsDirectory,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .Where(IsSupportedScreenshot)
                    .OrderBy(
                        path => Path.GetFileName(path),
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];

        return new ProjectMediaSnapshot(
            logoFilePath,
            screenshotFilePaths);
    }

    public async Task<string> SetLogoAsync(
        string projectDirectory,
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        var projectPath =
            RequireProjectDirectory(projectDirectory);
        var sourcePath =
            RequireSourceFile(sourceFilePath);

        if (!string.Equals(
                Path.GetExtension(sourcePath),
                ".png",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The project logo must be a PNG file.");
        }

        var logoDirectory = Path.Combine(
            projectPath,
            ProjectLayout.MediaDirectoryName,
            ProjectLayout.LogoDirectoryName);

        Directory.CreateDirectory(logoDirectory);

        var destinationPath = Path.Combine(
            logoDirectory,
            "logo.png");

        if (!string.Equals(
                sourcePath,
                destinationPath,
                StringComparison.OrdinalIgnoreCase))
        {
            await CopyFileAsync(
                sourcePath,
                destinationPath,
                overwrite: true,
                cancellationToken);
        }

        foreach (var existingFile in Directory
                     .EnumerateFiles(
                         logoDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly))
        {
            if (!string.Equals(
                    existingFile,
                    destinationPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(existingFile);
            }
        }

        return destinationPath;
    }

    public async Task<IReadOnlyList<string>> AddScreenshotsAsync(
        string projectDirectory,
        IReadOnlyList<string> sourceFilePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFilePaths);

        if (sourceFilePaths.Count == 0)
        {
            return [];
        }

        var projectPath =
            RequireProjectDirectory(projectDirectory);

        var screenshotsDirectory = Path.Combine(
            projectPath,
            ProjectLayout.MediaDirectoryName,
            ProjectLayout.ScreenshotsDirectoryName);

        Directory.CreateDirectory(screenshotsDirectory);

        var added = new List<string>();

        foreach (var sourceFilePath in sourceFilePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourcePath =
                RequireSourceFile(sourceFilePath);

            if (!IsSupportedScreenshot(sourcePath))
            {
                throw new InvalidDataException(
                    $"Screenshot '{Path.GetFileName(sourcePath)}' must be PNG or JPEG.");
            }

            var destinationPath =
                GetAvailableDestinationPath(
                    screenshotsDirectory,
                    Path.GetFileName(sourcePath));

            if (string.Equals(
                    sourcePath,
                    destinationPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                added.Add(destinationPath);
                continue;
            }

            await CopyFileAsync(
                sourcePath,
                destinationPath,
                overwrite: false,
                cancellationToken);

            added.Add(destinationPath);
        }

        return added;
    }

    private static string RequireProjectDirectory(
        string? projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);

        var fullPath =
            Path.GetFullPath(projectDirectory);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Project directory '{fullPath}' does not exist.");
        }

        return fullPath;
    }

    private static string RequireSourceFile(
        string? sourceFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourceFilePath);

        var fullPath =
            Path.GetFullPath(sourceFilePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Media source file does not exist.",
                fullPath);
        }

        return fullPath;
    }

    private static bool IsSupportedScreenshot(
        string path) =>
        ScreenshotExtensions.Contains(
            Path.GetExtension(path),
            StringComparer.OrdinalIgnoreCase);

    private static string GetAvailableDestinationPath(
        string directory,
        string fileName)
    {
        var destinationPath = Path.Combine(
            directory,
            fileName);

        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        var name =
            Path.GetFileNameWithoutExtension(fileName);
        var extension =
            Path.GetExtension(fileName);

        for (var index = 2; ; index++)
        {
            destinationPath = Path.Combine(
                directory,
                $"{name}-{index}{extension}");

            if (!File.Exists(destinationPath))
            {
                return destinationPath;
            }
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var destinationMode =
            overwrite
                ? FileMode.Create
                : FileMode.CreateNew;

        await using var source =
            new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);

        await using var destination =
            new FileStream(
                destinationPath,
                destinationMode,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);

        await source.CopyToAsync(
            destination,
            cancellationToken);
    }
}
