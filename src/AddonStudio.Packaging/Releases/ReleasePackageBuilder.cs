using System.IO.Compression;
using System.Security.Cryptography;
using AddonStudio.Core.Projects;

namespace AddonStudio.Packaging.Releases;

public sealed class ReleasePackageBuilder
{
    private const string PortableInvalidFileNameCharacters =
        "<>:\"/\\|?*";

    private static readonly DateTimeOffset
        DeterministicEntryTimestamp =
            new(
                1980,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);

    public async Task<ReleasePackageResult> BuildAsync(
        ReleasePackageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(
            request.Manifest);

        var projectDirectory =
            RequireManagedProjectDirectory(
                request.ProjectDirectory);

        var version =
            RequireVersion(
                request.Version);

        var runtimeAddons =
            request.Manifest.Runtime.Addons
                .Where(addon =>
                    !string.IsNullOrWhiteSpace(addon))
                .Select(addon =>
                    addon.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (runtimeAddons.Length == 0)
        {
            throw new InvalidOperationException(
                "The project does not define any runtime addon directories.");
        }

        var releaseDirectory =
            Path.Combine(
                projectDirectory,
                ProjectLayout.ReleaseDirectoryName,
                "Versions",
                version);

        Directory.CreateDirectory(
            releaseDirectory);

        var packageFileName =
            BuildPackageFileName(
                request.Manifest,
                version);

        var packagePath =
            Path.Combine(
                releaseDirectory,
                packageFileName);

        var temporaryPackagePath =
            packagePath +
            ".tmp-" +
            Guid.NewGuid().ToString("N");

        var entries =
            new List<string>();

        try
        {
            {
                await using var stream =
                    new FileStream(
                        temporaryPackagePath,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        81920,
                        useAsync: true);

                using (
                    var archive = new ZipArchive(
                        stream,
                        ZipArchiveMode.Create,
                        leaveOpen: true))
                {
                foreach (var addon in runtimeAddons)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var addonDirectory =
                        Path.Combine(
                            projectDirectory,
                            ProjectLayout.RuntimeDirectoryName,
                            addon);

                    if (!Directory.Exists(
                            addonDirectory))
                    {
                        throw new DirectoryNotFoundException(
                            $"Runtime addon directory '{addonDirectory}' does not exist.");
                    }

                    foreach (var file in Directory
                                 .EnumerateFiles(
                                     addonDirectory,
                                     "*",
                                     SearchOption.AllDirectories)
                                 .OrderBy(
                                     file => file,
                                     StringComparer.OrdinalIgnoreCase))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var relativePath =
                            Path.GetRelativePath(
                                addonDirectory,
                                file);

                        if (!ShouldPackage(
                                relativePath))
                        {
                            continue;
                        }

                        var entryName =
                            NormalizeZipPath(
                                Path.Combine(
                                    addon,
                                    relativePath));

                        var entry =
                            archive.CreateEntry(
                                entryName,
                                CompressionLevel.Optimal);

                        entry.LastWriteTime =
                            DeterministicEntryTimestamp;

                        await using var input =
                            new FileStream(
                                file,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.Read,
                                81920,
                                useAsync: true);

                        await using var output =
                            entry.Open();

                        await input.CopyToAsync(
                            output,
                            cancellationToken);

                        entries.Add(
                            entryName);
                    }
                    }
                }

                await stream.FlushAsync(
                    cancellationToken);
            }

            if (entries.Count == 0)
            {
                throw new InvalidOperationException(
                    "The release package would be empty.");
            }

            File.Move(
                temporaryPackagePath,
                packagePath,
                overwrite: true);
        }
        catch
        {
            if (File.Exists(
                    temporaryPackagePath))
            {
                File.Delete(
                    temporaryPackagePath);
            }

            throw;
        }

        var size =
            new FileInfo(
                packagePath)
                .Length;

        var sha256 =
            await ComputeSha256Async(
                packagePath,
                cancellationToken);

        return new ReleasePackageResult(
            packagePath,
            packageFileName,
            size,
            sha256,
            entries);
    }

    internal static bool ShouldPackage(
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(
                relativePath))
        {
            return false;
        }

        var normalized =
            NormalizeZipPath(
                relativePath);

        var segments =
            normalized.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment =>
                segment is
                    ".git" or
                    ".idea" or
                    ".vs"))
        {
            return false;
        }

        var fileName =
            segments[^1];

        if (fileName is
            ".DS_Store" or
            "Thumbs.db")
        {
            return false;
        }

        var extension =
            Path.GetExtension(
                fileName);

        return !extension.Equals(
                   ".md",
                   StringComparison.OrdinalIgnoreCase) &&
               !extension.Equals(
                   ".markdown",
                   StringComparison.OrdinalIgnoreCase) &&
               !extension.Equals(
                   ".zip",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPackageFileName(
        ProjectManifest manifest,
        string version)
    {
        var baseName =
            manifest.Release?.PackageName;

        if (string.IsNullOrWhiteSpace(
                baseName))
        {
            baseName =
                manifest.Runtime.PrimaryAddon;
        }

        if (string.IsNullOrWhiteSpace(
                baseName))
        {
            baseName =
                manifest.Project.Name;
        }

        baseName =
            Path.GetFileNameWithoutExtension(
                baseName.Trim());

        var safeBaseName =
            new string(
                baseName
                    .Select(character =>
                        char.IsControl(character) ||
                        PortableInvalidFileNameCharacters
                            .Contains(character)
                            ? '-'
                            : character)
                    .ToArray())
                .Trim(
                    ' ',
                    '.',
                    '-');

        if (safeBaseName.Length == 0)
        {
            safeBaseName =
                "Addon";
        }

        return $"{safeBaseName}-{version}.zip";
    }

    private static string RequireVersion(
        string? version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            version);

        var value =
            version.Trim();

        if (value is "." or ".." ||
            value.Any(character =>
                char.IsControl(character) ||
                PortableInvalidFileNameCharacters
                    .Contains(character)))
        {
            throw new ArgumentException(
                "Release version cannot be used as a directory name.",
                nameof(version));
        }

        return value;
    }

    private static string NormalizeZipPath(
        string path) =>
        path.Replace(
            '\\',
            '/');

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);

        var hash =
            await SHA256.HashDataAsync(
                stream,
                cancellationToken);

        return Convert.ToHexString(
            hash)
            .ToLowerInvariant();
    }

    private static string RequireManagedProjectDirectory(
        string? projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);

        var fullPath =
            Path.GetFullPath(
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
