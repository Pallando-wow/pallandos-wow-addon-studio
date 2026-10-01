using AddonStudio.Packaging.Releases;

namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgeReleaseOverviewService(
    ReleaseHistoryService releaseHistoryService,
    ReleaseArtifactManifestService artifactManifestService,
    CurseForgeReleaseSettingsService releaseSettingsService,
    CurseForgePublicationRecordService publicationRecordService)
{
    public async Task<
        IReadOnlyList<CurseForgeReleaseOverviewEntry>>
        GetOverviewAsync(
            string projectDirectory,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);

        var releases =
            releaseHistoryService.GetHistory(
                projectDirectory);

        var result =
            new List<CurseForgeReleaseOverviewEntry>(
                releases.Count);

        foreach (var release in releases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var issues =
                new List<string>();

            CurseForgeReleaseSettings? settings =
                null;

            if (!release.HasChangelog)
            {
                issues.Add(
                    "Version-specific changelog is missing.");
            }

            if (!release.HasArtifactManifest)
            {
                issues.Add(
                    "Release artifact manifest is missing.");
            }
            else
            {
                try
                {
                    var verification =
                        await artifactManifestService.VerifyAsync(
                            projectDirectory,
                            release.Version,
                            cancellationToken);

                    foreach (var issue in verification.Issues)
                    {
                        issues.Add(
                            "Artifact: " +
                            issue);
                    }
                }
                catch (Exception exception)
                    when (exception is
                        InvalidDataException or
                        IOException or
                        UnauthorizedAccessException)
                {
                    issues.Add(
                        "Artifact: " +
                        exception.Message);
                }
            }

            try
            {
                settings =
                    await releaseSettingsService.ReadAsync(
                        projectDirectory,
                        release.Version,
                        cancellationToken);
            }
            catch (Exception exception)
                when (exception is
                    InvalidDataException or
                    IOException or
                    UnauthorizedAccessException)
            {
                issues.Add(
                    "Release settings: " +
                    exception.Message);
            }

            if (settings is null)
            {
                issues.Add(
                    "CurseForge release settings are missing.");
            }

            CurseForgePublicationRecord? publication =
                null;

            try
            {
                publication =
                    await publicationRecordService.ReadAsync(
                        projectDirectory,
                        release.Version,
                        cancellationToken);
            }
            catch (Exception exception)
                when (exception is
                    InvalidDataException or
                    IOException or
                    UnauthorizedAccessException)
            {
                issues.Add(
                    "Publication record: " +
                    exception.Message);
            }

            ReleaseArtifactManifest? artifact =
                null;

            if (release.HasArtifactManifest)
            {
                try
                {
                    artifact =
                        await artifactManifestService.ReadAsync(
                            projectDirectory,
                            release.Version,
                            cancellationToken);
                }
                catch (Exception)
                    when (issues.Any(issue =>
                        issue.StartsWith(
                            "Artifact:",
                            StringComparison.Ordinal)))
                {
                }
            }

            var publicationState =
                ResolvePublicationState(
                    artifact,
                    publication);

            result.Add(
                new CurseForgeReleaseOverviewEntry(
                    release.Version,
                    publicationState,
                    release.HasChangelog,
                    release.HasArtifactManifest,
                    settings is not null,
                    issues.Count == 0,
                    issues,
                    release.PackageFiles,
                    settings?.GameVersionIds ?? [],
                    settings?.ReleaseType,
                    settings?.IsMarkedForManualRelease,
                    settings?.DisplayName,
                    publication?.ProjectId,
                    publication?.FileId,
                    publication?.UploadedAtUtc));
        }

        return result;
    }

    private static CurseForgePublicationState
        ResolvePublicationState(
            ReleaseArtifactManifest? artifact,
            CurseForgePublicationRecord? publication)
    {
        if (publication is null)
        {
            return CurseForgePublicationState.NotPublished;
        }

        if (artifact is null ||
            !string.Equals(
                artifact.Sha256,
                publication.ArtifactSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return CurseForgePublicationState.LocalArtifactChanged;
        }

        return CurseForgePublicationState.Published;
    }
}
