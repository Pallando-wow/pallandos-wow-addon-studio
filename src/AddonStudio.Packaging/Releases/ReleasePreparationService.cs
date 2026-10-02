using AddonStudio.Core.Projects;
using AddonStudio.Core.Publishing;

namespace AddonStudio.Packaging.Releases;

public sealed class ReleasePreparationService
{
    public ReleasePreparationSnapshot Inspect(
        ReleasePreparationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(
            request.Manifest);

        var projectDirectory =
            RequireManagedProjectDirectory(
                request.ProjectDirectory);

        var version =
            request.Version?.Trim() ??
            string.Empty;

        var primaryAddon =
            request.Manifest.Runtime.PrimaryAddon?
                .Trim() ??
            string.Empty;

        var runtimeAddons =
            request.Manifest.Runtime.Addons
                .Where(addon =>
                    !string.IsNullOrWhiteSpace(addon))
                .Select(addon =>
                    addon.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var issues =
            new List<ReleasePreparationIssue>();

        ValidateVersion(
            version,
            issues);

        ValidateRuntime(
            projectDirectory,
            primaryAddon,
            runtimeAddons,
            issues);

        ValidatePublishingContent(
            projectDirectory,
            version,
            issues);

        if (request.RequireCurseForgeMetadata)
        {
            ValidateCurseForge(
                request.Manifest.CurseForge,
                request.CurseForgeProjectVerified,
                issues);
        }

        return new ReleasePreparationSnapshot(
            projectDirectory,
            version,
            primaryAddon,
            runtimeAddons,
            issues);
    }

    private static void ValidateVersion(
        string version,
        ICollection<ReleasePreparationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            issues.Add(
                Error(
                    "RELEASE_VERSION_MISSING",
                    "The primary addon's .toc version is required."));
            return;
        }

        try
        {
            _ =
                ReleasePathRules
                    .RequireVersionDirectoryName(
                        version);
        }
        catch (ArgumentException)
        {
            issues.Add(
                Error(
                    "RELEASE_VERSION_INVALID",
                    $"Version '{version}' cannot be used as a release directory name."));
        }
    }

    private static void ValidateRuntime(
        string projectDirectory,
        string primaryAddon,
        IReadOnlyList<string> runtimeAddons,
        ICollection<ReleasePreparationIssue> issues)
    {
        if (runtimeAddons.Count == 0)
        {
            issues.Add(
                Error(
                    "RUNTIME_ADDONS_MISSING",
                    "The project does not define any runtime addon directories."));
            return;
        }

        if (string.IsNullOrWhiteSpace(primaryAddon))
        {
            issues.Add(
                Error(
                    "PRIMARY_ADDON_MISSING",
                    "The project does not define a primary addon."));
        }
        else if (!runtimeAddons.Contains(
                     primaryAddon,
                     StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(
                Error(
                    "PRIMARY_ADDON_NOT_IN_RUNTIME",
                    $"Primary addon '{primaryAddon}' is not listed in runtime.addons."));
        }

        foreach (var addon in runtimeAddons)
        {
            var addonDirectory =
                Path.Combine(
                    projectDirectory,
                    ProjectLayout.RuntimeDirectoryName,
                    addon);

            if (!Directory.Exists(
                    addonDirectory))
            {
                issues.Add(
                    Error(
                        "RUNTIME_ADDON_DIRECTORY_MISSING",
                        $"Runtime addon directory '{addon}' does not exist."));
                continue;
            }

            if (!Directory
                    .EnumerateFiles(
                        addonDirectory,
                        "*.toc",
                        SearchOption.TopDirectoryOnly)
                    .Any())
            {
                issues.Add(
                    Error(
                        "RUNTIME_TOC_MISSING",
                        $"Runtime addon '{addon}' does not contain a .toc file."));
            }
        }
    }

    private static void ValidatePublishingContent(
        string projectDirectory,
        string version,
        ICollection<ReleasePreparationIssue> issues)
    {
        var releaseDirectory =
            Path.Combine(
                projectDirectory,
                ProjectLayout.ReleaseDirectoryName);

        RequireFile(
            Path.Combine(
                releaseDirectory,
                PublishingContentLayout.GetFileName(
                    PublishingContentKind.Summary)),
            "SUMMARY_MISSING",
            "Publishing summary is missing.",
            issues);

        RequireFile(
            Path.Combine(
                releaseDirectory,
                PublishingContentLayout.GetFileName(
                    PublishingContentKind.Description)),
            "DESCRIPTION_MISSING",
            "Publishing description is missing.",
            issues);

        if (!string.IsNullOrWhiteSpace(version))
        {
            try
            {
                RequireFile(
                    Path.Combine(
                        releaseDirectory,
                        PublishingContentLayout
                            .GetReleaseChangelogRelativePath(
                                version)),
                    "CHANGELOG_MISSING",
                    $"Release changelog for version '{version}' is missing.",
                    issues);
            }
            catch (ArgumentException)
            {
                // The invalid version already has a dedicated issue.
            }
        }

        RequireFile(
            Path.Combine(
                projectDirectory,
                ProjectLayout.MediaDirectoryName,
                ProjectLayout.LogoDirectoryName,
                "logo.png"),
            "LOGO_MISSING",
            "Project logo is missing.",
            issues);

        var screenshotsDirectory =
            Path.Combine(
                projectDirectory,
                ProjectLayout.MediaDirectoryName,
                ProjectLayout.ScreenshotsDirectoryName);

        if (!Directory.Exists(screenshotsDirectory) ||
            !Directory
                .EnumerateFiles(
                    screenshotsDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Any(IsSupportedScreenshot))
        {
            issues.Add(
                new ReleasePreparationIssue(
                    ReleasePreparationSeverity.Info,
                    "SCREENSHOTS_OPTIONAL",
                    "No project screenshots are configured."));
        }
    }

    private static void ValidateCurseForge(
        CurseForgeConfiguration? configuration,
        bool projectVerified,
        ICollection<ReleasePreparationIssue> issues)
    {
        if (configuration is null ||
            (string.IsNullOrWhiteSpace(
                 configuration.ProjectId) &&
             string.IsNullOrWhiteSpace(
                 configuration.Slug)))
        {
            issues.Add(
                Error(
                    "CURSEFORGE_BINDING_MISSING",
                    "CurseForge project binding is missing."));
        }
        else if (!projectVerified)
        {
            issues.Add(
                Error(
                    "CURSEFORGE_BINDING_NOT_VERIFIED",
                    "CurseForge project binding has not been verified against the API."));
        }

        if (configuration is null ||
            string.IsNullOrWhiteSpace(
                configuration.MainCategoryId))
        {
            issues.Add(
                Error(
                    "CURSEFORGE_MAIN_CATEGORY_MISSING",
                    "CurseForge main category is missing."));
        }

        if (configuration is null ||
            string.IsNullOrWhiteSpace(
                configuration.License))
        {
            issues.Add(
                Error(
                    "CURSEFORGE_LICENSE_MISSING",
                    "CurseForge project license is missing."));
        }

        if (configuration?.AllowDistribution is null)
        {
            issues.Add(
                Error(
                    "CURSEFORGE_DISTRIBUTION_MISSING",
                    "CurseForge distribution setting is missing."));
        }
    }

    private static void RequireFile(
        string path,
        string code,
        string message,
        ICollection<ReleasePreparationIssue> issues)
    {
        if (!File.Exists(path) ||
            new FileInfo(path).Length == 0)
        {
            issues.Add(
                Error(
                    code,
                    message));
        }
    }

    private static bool IsSupportedScreenshot(
        string path) =>
        Path.GetExtension(path)
            .ToLowerInvariant() is
            ".png" or ".jpg" or ".jpeg";

    private static ReleasePreparationIssue Error(
        string code,
        string message) =>
        new(
            ReleasePreparationSeverity.Error,
            code,
            message);

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
