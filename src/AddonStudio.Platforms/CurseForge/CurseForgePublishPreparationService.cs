using AddonStudio.Core.Projects;
using AddonStudio.Core.Publishing;

namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgePublishPreparationService(
    CurseForgeReleaseOverviewService overviewService,
    CurseForgeReleaseSettingsService releaseSettingsService,
    CurseForgeUploadPlanService uploadPlanService,
    CurseForgeUploadTokenService uploadTokenService)
{
    public async Task<CurseForgePublishPreparation> PrepareAsync(
        string projectDirectory,
        string version,
        int projectId,
        bool allowRepublish = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            version);

        if (projectId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectId));
        }

        var normalizedVersion =
            version.Trim();

        var overview =
            await overviewService.GetOverviewAsync(
                projectDirectory,
                cancellationToken);

        var release =
            overview.FirstOrDefault(
                entry =>
                    string.Equals(
                        entry.Version,
                        normalizedVersion,
                        StringComparison.OrdinalIgnoreCase));

        var hasUploadToken =
            !string.IsNullOrWhiteSpace(
                uploadTokenService.Load());

        if (release is null)
        {
            var missingIssues =
                new List<string>
                {
                    $"Release version '{normalizedVersion}' was not found."
                };

            if (!hasUploadToken)
            {
                missingIssues.Add(
                    "CurseForge upload token is missing.");
            }

            return new CurseForgePublishPreparation(
                normalizedVersion,
                CurseForgePublicationState.NotPublished,
                hasUploadToken,
                RequiresRepublishConfirmation: false,
                UploadPlan: null,
                missingIssues);
        }

        var issues =
            release.Issues.ToList();

        if (!hasUploadToken)
        {
            issues.Add(
                "CurseForge upload token is missing.");
        }

        var requiresRepublishConfirmation =
            release.PublicationState ==
                CurseForgePublicationState.Published &&
            !allowRepublish;

        if (requiresRepublishConfirmation)
        {
            var fileReference =
                release.FileId is > 0
                    ? $" as CurseForge file #{release.FileId}"
                    : string.Empty;

            issues.Add(
                $"Release '{normalizedVersion}' is already published{fileReference}. Explicit republish confirmation is required.");
        }

        CurseForgeUploadPlan? uploadPlan =
            null;

        if (release.HasChangelog &&
            release.HasArtifactManifest &&
            release.HasReleaseSettings)
        {
            try
            {
                var settings =
                    await releaseSettingsService.ReadAsync(
                        projectDirectory,
                        normalizedVersion,
                        cancellationToken);

                if (settings is not null)
                {
                    var changelogPath =
                        Path.Combine(
                            Path.GetFullPath(
                                projectDirectory),
                            ProjectLayout.ReleaseDirectoryName,
                            PublishingContentLayout
                                .GetReleaseChangelogRelativePath(
                                    normalizedVersion));

                    var changelog =
                        await File.ReadAllTextAsync(
                            changelogPath,
                            cancellationToken);

                    if (string.IsNullOrWhiteSpace(
                            changelog))
                    {
                        issues.Add(
                            "Version-specific changelog is empty.");
                    }
                    else
                    {
                        uploadPlan =
                            await uploadPlanService.CreateAsync(
                                projectDirectory,
                                normalizedVersion,
                                projectId,
                                settings,
                                changelog,
                                cancellationToken);
                    }
                }
            }
            catch (Exception exception)
                when (exception is
                    InvalidDataException or
                    IOException or
                    UnauthorizedAccessException)
            {
                issues.Add(
                    "Upload plan: " +
                    exception.Message);
            }
        }

        return new CurseForgePublishPreparation(
            normalizedVersion,
            release.PublicationState,
            hasUploadToken,
            requiresRepublishConfirmation,
            uploadPlan,
            issues);
    }
}
