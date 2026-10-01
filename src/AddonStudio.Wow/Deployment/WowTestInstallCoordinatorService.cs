using AddonStudio.Application.Settings;
using AddonStudio.Core.Projects;

namespace AddonStudio.Wow.Deployment;

public sealed class WowTestInstallCoordinatorService(
    WowTestInstallWorkflowService workflowService)
{
    public Task<WowTestInstallResult> InstallAsync(
        string projectDirectory,
        ProjectManifest manifest,
        StudioSettings settings,
        bool cleanTest = false,
        string? backupRootDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);
        ArgumentNullException.ThrowIfNull(
            manifest);
        ArgumentNullException.ThrowIfNull(
            settings);

        var runtimeAddons =
            manifest.Runtime.Addons
                .Where(
                    addon =>
                        !string.IsNullOrWhiteSpace(
                            addon))
                .Select(
                    addon =>
                        addon.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (runtimeAddons.Length == 0)
        {
            throw new InvalidDataException(
                "Project does not contain any runtime addons for test installation.");
        }

        if (string.IsNullOrWhiteSpace(
                settings.WowForeverAddOnsPath))
        {
            throw new InvalidDataException(
                "WoW Forever AddOns path is not configured.");
        }

        var savedVariablesPath =
            cleanTest
                ? settings.SavedVariablesPath
                : null;

        if (cleanTest &&
            string.IsNullOrWhiteSpace(
                savedVariablesPath))
        {
            throw new InvalidDataException(
                "WoW SavedVariables path is required for a clean test installation.");
        }

        var request =
            new WowTestInstallRequest(
                projectDirectory,
                settings.WowForeverAddOnsPath,
                runtimeAddons,
                ResetSavedVariables:
                    cleanTest,
                SavedVariablesDirectory:
                    savedVariablesPath,
                BackupRootDirectory:
                    backupRootDirectory);

        return workflowService.ExecuteAsync(
            request,
            cancellationToken);
    }
}
