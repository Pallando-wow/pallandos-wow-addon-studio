namespace AddonStudio.Wow.Deployment;

public sealed record WowTestInstallRequest(
    string ProjectDirectory,
    string WowAddOnsDirectory,
    IReadOnlyList<string> RuntimeAddons,
    bool ResetSavedVariables = false,
    string? SavedVariablesDirectory = null,
    string? BackupRootDirectory = null);

public sealed record WowTestAddonDeployment(
    string AddonName,
    string SourceDirectory,
    string TargetDirectory,
    IReadOnlyList<string> AccountSavedVariables,
    IReadOnlyList<string> CharacterSavedVariables);

public sealed record WowSavedVariablesFile(
    string AddonName,
    string FilePath,
    string RelativeBackupPath,
    bool CharacterSpecific);

public sealed record WowTestInstallPlan(
    string ProjectDirectory,
    string WowAddOnsDirectory,
    IReadOnlyList<WowTestAddonDeployment> Addons,
    bool ResetSavedVariables,
    string? SavedVariablesDirectory,
    string BackupRootDirectory,
    IReadOnlyList<WowSavedVariablesFile> SavedVariablesFiles);

public sealed record WowTestInstallResult(
    IReadOnlyList<string> InstalledAddons,
    IReadOnlyList<string> ResetSavedVariablesFiles,
    string? SavedVariablesBackupDirectory);
