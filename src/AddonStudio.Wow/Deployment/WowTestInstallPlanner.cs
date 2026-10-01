using AddonStudio.Core.Projects;
using AddonStudio.Wow.Toc;

namespace AddonStudio.Wow.Deployment;

public sealed class WowTestInstallPlanner(
    TocDocumentReader tocDocumentReader,
    string? defaultBackupRootDirectory = null)
{
    private readonly string backupRootDirectory =
        string.IsNullOrWhiteSpace(
            defaultBackupRootDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "PallandosWowAddonStudio",
                "TestBackups")
            : Path.GetFullPath(
                defaultBackupRootDirectory);

    public async Task<WowTestInstallPlan> CreateAsync(
        WowTestInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ProjectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.WowAddOnsDirectory);
        ArgumentNullException.ThrowIfNull(
            request.RuntimeAddons);

        var projectDirectory =
            Path.GetFullPath(
                request.ProjectDirectory);

        if (!File.Exists(
                Path.Combine(
                    projectDirectory,
                    ProjectLayout.ManifestFileName)))
        {
            throw new InvalidOperationException(
                $"Directory '{projectDirectory}' is not a managed Studio project.");
        }

        var wowAddOnsDirectory =
            Path.GetFullPath(
                request.WowAddOnsDirectory);

        if (!Directory.Exists(
                wowAddOnsDirectory))
        {
            throw new DirectoryNotFoundException(
                $"WoW AddOns directory does not exist: '{wowAddOnsDirectory}'.");
        }

        RequireWowAddOnsDirectory(
            wowAddOnsDirectory);

        var addonNames =
            request.RuntimeAddons
                .Select(
                    RequireAddonDirectoryName)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (addonNames.Length == 0)
        {
            throw new InvalidDataException(
                "At least one runtime addon is required for test installation.");
        }

        var addons =
            new List<WowTestAddonDeployment>(
                addonNames.Length);

        foreach (var addonName in addonNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceDirectory =
                Path.Combine(
                    projectDirectory,
                    ProjectLayout.RuntimeDirectoryName,
                    addonName);

            if (!Directory.Exists(
                    sourceDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Runtime addon directory does not exist: '{sourceDirectory}'.");
            }

            var tocPath =
                Path.Combine(
                    sourceDirectory,
                    addonName + ".toc");

            var toc =
                await tocDocumentReader.ReadAsync(
                    tocPath,
                    cancellationToken);

            addons.Add(
                new WowTestAddonDeployment(
                    addonName,
                    sourceDirectory,
                    Path.Combine(
                        wowAddOnsDirectory,
                        addonName),
                    toc.SavedVariables,
                    toc.SavedVariablesPerCharacter));
        }

        var savedVariablesFiles =
            Array.Empty<WowSavedVariablesFile>();

        string? savedVariablesDirectory =
            null;

        if (request.ResetSavedVariables)
        {
            if (string.IsNullOrWhiteSpace(
                    request.SavedVariablesDirectory))
            {
                throw new InvalidDataException(
                    "SavedVariables directory is required for a clean test installation.");
            }

            savedVariablesDirectory =
                Path.GetFullPath(
                    request.SavedVariablesDirectory);

            if (!Directory.Exists(
                    savedVariablesDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"SavedVariables directory does not exist: '{savedVariablesDirectory}'.");
            }

            RequireAccountSavedVariablesDirectory(
                savedVariablesDirectory);

            savedVariablesFiles =
                DiscoverSavedVariablesFiles(
                    addons,
                    savedVariablesDirectory)
                .ToArray();
        }

        var requestedBackupRoot =
            string.IsNullOrWhiteSpace(
                request.BackupRootDirectory)
                ? backupRootDirectory
                : Path.GetFullPath(
                    request.BackupRootDirectory);

        return new WowTestInstallPlan(
            projectDirectory,
            wowAddOnsDirectory,
            addons,
            request.ResetSavedVariables,
            savedVariablesDirectory,
            requestedBackupRoot,
            savedVariablesFiles);
    }

    private static void RequireWowAddOnsDirectory(
        string wowAddOnsDirectory)
    {
        if (!string.Equals(
                Path.GetFileName(
                    wowAddOnsDirectory),
                "AddOns",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Configured WoW AddOns path must point directly to an Interface/AddOns directory.");
        }

        var interfaceDirectory =
            Directory.GetParent(
                wowAddOnsDirectory);

        if (interfaceDirectory is null ||
            !string.Equals(
                interfaceDirectory.Name,
                "Interface",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Configured WoW AddOns path must point directly to an Interface/AddOns directory.");
        }
    }

    private static void RequireAccountSavedVariablesDirectory(
        string savedVariablesDirectory)
    {
        if (!string.Equals(
                Path.GetFileName(
                    savedVariablesDirectory),
                "SavedVariables",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Configured SavedVariables path must point directly to a WTF/Account/<account>/SavedVariables directory.");
        }

        var accountDirectory =
            Directory.GetParent(
                savedVariablesDirectory);

        var accountRootDirectory =
            accountDirectory?.Parent;

        var wtfDirectory =
            accountRootDirectory?.Parent;

        if (accountDirectory is null ||
            accountRootDirectory is null ||
            wtfDirectory is null ||
            !string.Equals(
                accountRootDirectory.Name,
                "Account",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                wtfDirectory.Name,
                "WTF",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Configured SavedVariables path must point directly to a WTF/Account/<account>/SavedVariables directory.");
        }
    }

    private static IEnumerable<WowSavedVariablesFile>
        DiscoverSavedVariablesFiles(
            IReadOnlyList<WowTestAddonDeployment> addons,
            string savedVariablesDirectory)
    {
        var accountDirectory =
            Directory.GetParent(
                savedVariablesDirectory)
            ?? throw new InvalidDataException(
                "SavedVariables directory has no account parent directory.");

        var characterSavedVariablesDirectories =
            EnumerateCharacterSavedVariablesDirectories(
                accountDirectory.FullName)
            .ToArray();

        foreach (var addon in addons)
        {
            if (addon.AccountSavedVariables.Count > 0)
            {
                foreach (var file in EnumerateAddonSavedVariableFiles(
                             savedVariablesDirectory,
                             addon.AddonName))
                {
                    yield return new WowSavedVariablesFile(
                        addon.AddonName,
                        file,
                        Path.GetRelativePath(
                            accountDirectory.FullName,
                            file),
                        CharacterSpecific: false);
                }
            }

            if (addon.CharacterSavedVariables.Count == 0)
            {
                continue;
            }

            foreach (var directory in characterSavedVariablesDirectories)
            {
                foreach (var file in EnumerateAddonSavedVariableFiles(
                             directory,
                             addon.AddonName))
                {
                    yield return new WowSavedVariablesFile(
                        addon.AddonName,
                        file,
                        Path.GetRelativePath(
                            accountDirectory.FullName,
                            file),
                        CharacterSpecific: true);
                }
            }
        }
    }

    private static IEnumerable<string>
        EnumerateCharacterSavedVariablesDirectories(
            string accountDirectory)
    {
        foreach (var realmDirectory in Directory.EnumerateDirectories(
                     accountDirectory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(
                    Path.GetFileName(
                        realmDirectory),
                    "SavedVariables",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var characterDirectory in Directory.EnumerateDirectories(
                         realmDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                var savedVariablesDirectory =
                    Path.Combine(
                        characterDirectory,
                        "SavedVariables");

                if (Directory.Exists(
                        savedVariablesDirectory))
                {
                    yield return savedVariablesDirectory;
                }
            }
        }
    }

    private static IEnumerable<string>
        EnumerateAddonSavedVariableFiles(
            string directory,
            string addonName)
    {
        foreach (var suffix in new[]
                 {
                     ".lua",
                     ".lua.bak"
                 })
        {
            var path =
                Path.Combine(
                    directory,
                    addonName + suffix);

            if (File.Exists(
                    path))
            {
                yield return path;
            }
        }
    }

    private static string RequireAddonDirectoryName(
        string? addonName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            addonName);

        var value =
            addonName.Trim();

        if (value is "." or ".." ||
            !string.Equals(
                Path.GetFileName(
                    value),
                value,
                StringComparison.Ordinal) ||
            value.Any(
                char.IsControl))
        {
            throw new InvalidDataException(
                $"Runtime addon name '{addonName}' is not a safe directory name.");
        }

        return value;
    }
}
