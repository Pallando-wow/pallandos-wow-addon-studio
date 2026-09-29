using AddonStudio.Application.Projects;
using AddonStudio.Core.Projects;

namespace AddonStudio.Wow.Projects;

public sealed class AddonSourceInspector : IAddonSourceInspector
{
    public Task<AddonSourceInspection> InspectAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullPath = Path.GetFullPath(sourcePath);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Source directory '{fullPath}' does not exist.");
        }

        if (File.Exists(Path.Combine(
                fullPath,
                ProjectLayout.ManifestFileName)))
        {
            throw new InvalidOperationException(
                "The selected directory already contains project.json and should be opened as an existing Studio project.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var directAddon = TryDetectDirectAddon(fullPath);

        if (directAddon is not null)
        {
            return Task.FromResult(
                new AddonSourceInspection(
                    fullPath,
                    directAddon.Name,
                    [directAddon]));
        }

        var runtimeRoot = Path.Combine(
            fullPath,
            ProjectLayout.RuntimeDirectoryName);

        var runtimeAddons = Directory.Exists(runtimeRoot)
            ? DetectChildAddons(runtimeRoot, cancellationToken)
            : [];

        if (runtimeAddons.Count > 0)
        {
            return Task.FromResult(
                new AddonSourceInspection(
                    fullPath,
                    SuggestProjectName(fullPath, runtimeAddons),
                    runtimeAddons));
        }

        runtimeAddons = DetectChildAddons(
            fullPath,
            cancellationToken);

        if (runtimeAddons.Count > 0)
        {
            return Task.FromResult(
                new AddonSourceInspection(
                    fullPath,
                    SuggestProjectName(fullPath, runtimeAddons),
                    runtimeAddons));
        }

        var wrappedProject = TryDetectSingleWrapperDirectory(
            fullPath,
            cancellationToken);

        if (wrappedProject is not null)
        {
            return Task.FromResult(wrappedProject);
        }

        throw new InvalidDataException(
            "No WoW addon could be detected. The selected directory contains no .toc file directly, below AddOns, or in an immediate addon child directory.");
    }

    private static DetectedRuntimeAddon? TryDetectDirectAddon(
        string directory)
    {
        var tocFiles = GetTocFiles(directory);

        if (tocFiles.Count == 0)
        {
            return null;
        }

        var addonName = SelectAddonName(
            directory,
            tocFiles);

        return new DetectedRuntimeAddon(
            addonName,
            directory,
            tocFiles);
    }

    private static IReadOnlyList<DetectedRuntimeAddon> DetectChildAddons(
        string parentDirectory,
        CancellationToken cancellationToken)
    {
        var runtimeAddons = new List<DetectedRuntimeAddon>();

        foreach (var directory in Directory
                     .EnumerateDirectories(parentDirectory)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detected = TryDetectDirectAddon(directory);

            if (detected is not null)
            {
                runtimeAddons.Add(detected);
            }
        }

        return runtimeAddons;
    }

    private static AddonSourceInspection? TryDetectSingleWrapperDirectory(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        var childDirectories = Directory
            .EnumerateDirectories(sourcePath)
            .ToArray();

        if (childDirectories.Length != 1)
        {
            return null;
        }

        var wrapperDirectory = childDirectories[0];

        if (File.Exists(Path.Combine(
                wrapperDirectory,
                ProjectLayout.ManifestFileName)))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var directAddon = TryDetectDirectAddon(wrapperDirectory);

        if (directAddon is not null)
        {
            return new AddonSourceInspection(
                sourcePath,
                directAddon.Name,
                [directAddon]);
        }

        var runtimeRoot = Path.Combine(
            wrapperDirectory,
            ProjectLayout.RuntimeDirectoryName);

        if (!Directory.Exists(runtimeRoot))
        {
            return null;
        }

        var runtimeAddons = DetectChildAddons(
            runtimeRoot,
            cancellationToken);

        if (runtimeAddons.Count == 0)
        {
            return null;
        }

        return new AddonSourceInspection(
            sourcePath,
            SuggestProjectName(wrapperDirectory, runtimeAddons),
            runtimeAddons);
    }

    private static string SuggestProjectName(
        string projectDirectory,
        IReadOnlyList<DetectedRuntimeAddon> runtimeAddons)
    {
        if (runtimeAddons.Count == 1)
        {
            return runtimeAddons[0].Name;
        }

        return Path.GetFileName(
            projectDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
    }

    private static IReadOnlyList<string> GetTocFiles(string directory) =>
        Directory
            .EnumerateFiles(
                directory,
                "*.toc",
                SearchOption.TopDirectoryOnly)
            .OrderBy(
                path => path,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string SelectAddonName(
        string directory,
        IReadOnlyList<string> tocFiles)
    {
        var directoryName = Path.GetFileName(
            directory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));

        var matchingToc = tocFiles.FirstOrDefault(
            path => string.Equals(
                Path.GetFileNameWithoutExtension(path),
                directoryName,
                StringComparison.OrdinalIgnoreCase));

        return matchingToc is not null
            ? Path.GetFileNameWithoutExtension(matchingToc)
            : Path.GetFileNameWithoutExtension(tocFiles[0]);
    }
}
