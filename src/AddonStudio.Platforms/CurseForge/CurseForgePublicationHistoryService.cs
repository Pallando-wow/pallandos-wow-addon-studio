using AddonStudio.Packaging.Releases;

namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgePublicationHistoryService(
    ReleaseHistoryService releaseHistoryService,
    ReleaseArtifactManifestService artifactManifestService,
    CurseForgePublicationRecordService publicationRecordService)
{
    public async Task<
        IReadOnlyList<CurseForgePublicationHistoryEntry>>
        GetHistoryAsync(
            string projectDirectory,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);

        var releases =
            releaseHistoryService.GetHistory(
                projectDirectory);

        var result =
            new List<CurseForgePublicationHistoryEntry>(
                releases.Count);

        foreach (var release in releases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var artifact =
                release.HasArtifactManifest
                    ? await artifactManifestService.ReadAsync(
                        projectDirectory,
                        release.Version,
                        cancellationToken)
                    : null;

            var publication =
                await publicationRecordService.ReadAsync(
                    projectDirectory,
                    release.Version,
                    cancellationToken);

            var state =
                ResolveState(
                    artifact,
                    publication);

            result.Add(
                new CurseForgePublicationHistoryEntry(
                    release.Version,
                    state,
                    release.HasChangelog,
                    release.HasArtifactManifest,
                    release.PackageFiles,
                    publication?.ProjectId,
                    publication?.FileId,
                    publication?.PackageFileName,
                    publication?.ArtifactSha256,
                    publication?.GameVersionIds ?? [],
                    publication?.ReleaseType,
                    publication?.IsMarkedForManualRelease,
                    publication?.UploadedAtUtc));
        }

        return result;
    }

    private static CurseForgePublicationState ResolveState(
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
