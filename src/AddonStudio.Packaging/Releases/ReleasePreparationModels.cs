using AddonStudio.Core.Projects;

namespace AddonStudio.Packaging.Releases;

public enum ReleasePreparationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ReleasePreparationIssue(
    ReleasePreparationSeverity Severity,
    string Code,
    string Message);

public sealed record ReleasePreparationRequest(
    string ProjectDirectory,
    ProjectManifest Manifest,
    string Version,
    bool CurseForgeProjectVerified,
    bool RequireCurseForgeMetadata = true);

public sealed record ReleasePreparationSnapshot(
    string ProjectDirectory,
    string Version,
    string PrimaryAddon,
    IReadOnlyList<string> RuntimeAddons,
    IReadOnlyList<ReleasePreparationIssue> Issues)
{
    public bool IsReady =>
        Issues.All(issue =>
            issue.Severity !=
            ReleasePreparationSeverity.Error);

    public int ErrorCount =>
        Issues.Count(issue =>
            issue.Severity ==
            ReleasePreparationSeverity.Error);

    public int WarningCount =>
        Issues.Count(issue =>
            issue.Severity ==
            ReleasePreparationSeverity.Warning);
}
