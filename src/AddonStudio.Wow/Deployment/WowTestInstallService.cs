namespace AddonStudio.Wow.Deployment;

public sealed class WowTestInstallService(
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock =
        timeProvider ??
        TimeProvider.System;

    public async Task<WowTestInstallResult> ExecuteAsync(
        WowTestInstallPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        ValidatePlan(
            plan);

        var operationId =
            Guid.NewGuid().ToString(
                "N");

        var stagingRoot =
            Path.Combine(
                plan.WowAddOnsDirectory,
                ".addonstudio-staging-" +
                operationId);

        var previousRoot =
            Path.Combine(
                plan.WowAddOnsDirectory,
                ".addonstudio-previous-" +
                operationId);

        var stagedAddons =
            new List<WowTestAddonDeployment>();
        var replacedAddons =
            new List<WowTestAddonDeployment>();

        try
        {
            Directory.CreateDirectory(
                stagingRoot);

            foreach (var addon in plan.Addons)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stagedDirectory =
                    Path.Combine(
                        stagingRoot,
                        addon.AddonName);

                await CopyDirectoryAsync(
                    addon.SourceDirectory,
                    stagedDirectory,
                    cancellationToken);

                stagedAddons.Add(
                    addon);
            }

            Directory.CreateDirectory(
                previousRoot);

            foreach (var addon in plan.Addons)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stagedDirectory =
                    Path.Combine(
                        stagingRoot,
                        addon.AddonName);

                var previousDirectory =
                    Path.Combine(
                        previousRoot,
                        addon.AddonName);

                if (Directory.Exists(
                        addon.TargetDirectory))
                {
                    Directory.Move(
                        addon.TargetDirectory,
                        previousDirectory);
                }

                try
                {
                    Directory.Move(
                        stagedDirectory,
                        addon.TargetDirectory);

                    replacedAddons.Add(
                        addon);
                }
                catch
                {
                    if (Directory.Exists(
                            previousDirectory) &&
                        !Directory.Exists(
                            addon.TargetDirectory))
                    {
                        Directory.Move(
                            previousDirectory,
                            addon.TargetDirectory);
                    }

                    throw;
                }
            }

            var resetFiles =
                Array.Empty<string>();

            string? backupDirectory =
                null;

            if (plan.ResetSavedVariables &&
                plan.SavedVariablesFiles.Count > 0)
            {
                backupDirectory =
                    CreateBackupDirectory(
                        plan.BackupRootDirectory);

                resetFiles =
                    await BackupAndDeleteSavedVariablesAsync(
                        plan.SavedVariablesFiles,
                        backupDirectory,
                        cancellationToken);
            }

            TryDeleteDirectory(
                previousRoot);
            TryDeleteDirectory(
                stagingRoot);

            return new WowTestInstallResult(
                plan.Addons
                    .Select(
                        addon =>
                            addon.AddonName)
                    .ToArray(),
                resetFiles,
                backupDirectory);
        }
        catch
        {
            RollBackInstalledAddons(
                plan.Addons,
                replacedAddons,
                previousRoot);

            TryDeleteDirectory(
                stagingRoot);
            TryDeleteDirectory(
                previousRoot);

            throw;
        }
    }

    private string CreateBackupDirectory(
        string backupRootDirectory)
    {
        Directory.CreateDirectory(
            backupRootDirectory);

        var timestamp =
            clock.GetUtcNow()
                .UtcDateTime
                .ToString(
                    "yyyyMMdd-HHmmss-fff",
                    System.Globalization.CultureInfo.InvariantCulture);

        var directory =
            Path.Combine(
                backupRootDirectory,
                timestamp +
                "-" +
                Guid.NewGuid().ToString(
                    "N"));

        Directory.CreateDirectory(
            directory);

        return directory;
    }

    private static async Task<string[]>
        BackupAndDeleteSavedVariablesAsync(
            IReadOnlyList<WowSavedVariablesFile> files,
            string backupDirectory,
            CancellationToken cancellationToken)
    {
        var backedUp =
            new List<WowSavedVariablesFile>(
                files.Count);

        try
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(
                        file.FilePath))
                {
                    continue;
                }

                var backupPath =
                    Path.Combine(
                        backupDirectory,
                        file.RelativeBackupPath);

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        backupPath)!);

                await CopyFileAsync(
                    file.FilePath,
                    backupPath,
                    cancellationToken);

                backedUp.Add(
                    file);
            }

            foreach (var file in backedUp)
            {
                cancellationToken.ThrowIfCancellationRequested();

                File.Delete(
                    file.FilePath);
            }

            return backedUp
                .Select(
                    file =>
                        file.FilePath)
                .ToArray();
        }
        catch
        {
            foreach (var file in backedUp)
            {
                var backupPath =
                    Path.Combine(
                        backupDirectory,
                        file.RelativeBackupPath);

                if (!File.Exists(
                        backupPath))
                {
                    continue;
                }

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        file.FilePath)!);

                File.Copy(
                    backupPath,
                    file.FilePath,
                    overwrite: true);
            }

            throw;
        }
    }

    private static void RollBackInstalledAddons(
        IReadOnlyList<WowTestAddonDeployment> allAddons,
        IReadOnlyList<WowTestAddonDeployment> replacedAddons,
        string previousRoot)
    {
        foreach (var addon in allAddons.Reverse())
        {
            if (replacedAddons.Contains(
                    addon) &&
                Directory.Exists(
                    addon.TargetDirectory))
            {
                Directory.Delete(
                    addon.TargetDirectory,
                    recursive: true);
            }

            var previousDirectory =
                Path.Combine(
                    previousRoot,
                    addon.AddonName);

            if (Directory.Exists(
                    previousDirectory) &&
                !Directory.Exists(
                    addon.TargetDirectory))
            {
                Directory.Move(
                    previousDirectory,
                    addon.TargetDirectory);
            }
        }
    }

    private static async Task CopyDirectoryAsync(
        string sourceDirectory,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(
            targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath =
                Path.GetRelativePath(
                    sourceDirectory,
                    directory);

            Directory.CreateDirectory(
                Path.Combine(
                    targetDirectory,
                    relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath =
                Path.GetRelativePath(
                    sourceDirectory,
                    file);

            var targetPath =
                Path.Combine(
                    targetDirectory,
                    relativePath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    targetPath)!);

            await CopyFileAsync(
                file,
                targetPath,
                cancellationToken);
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string targetPath,
        CancellationToken cancellationToken)
    {
        await using var source =
            new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);

        await using var target =
            new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);

        await source.CopyToAsync(
            target,
            cancellationToken);

        await target.FlushAsync(
            cancellationToken);
    }

    private static void ValidatePlan(
        WowTestInstallPlan plan)
    {
        if (!Directory.Exists(
                plan.WowAddOnsDirectory))
        {
            throw new DirectoryNotFoundException(
                $"WoW AddOns directory does not exist: '{plan.WowAddOnsDirectory}'.");
        }

        if (plan.Addons.Count == 0)
        {
            throw new InvalidDataException(
                "Test installation plan contains no runtime addons.");
        }

        foreach (var addon in plan.Addons)
        {
            if (!Directory.Exists(
                    addon.SourceDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Runtime addon source does not exist: '{addon.SourceDirectory}'.");
            }

            var expectedTarget =
                Path.Combine(
                    plan.WowAddOnsDirectory,
                    addon.AddonName);

            if (!string.Equals(
                    Path.GetFullPath(
                        addon.TargetDirectory),
                    Path.GetFullPath(
                        expectedTarget),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Test installation target for '{addon.AddonName}' is outside the configured WoW AddOns directory.");
            }
        }
    }

    private static void TryDeleteDirectory(
        string path)
    {
        if (!Directory.Exists(
                path))
        {
            return;
        }

        try
        {
            Directory.Delete(
                path,
                recursive: true);
        }
        catch
        {
        }
    }
}
