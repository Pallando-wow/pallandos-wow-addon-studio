namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgeUploadExecutionService(
    CurseForgeUploadApiClient uploadApiClient,
    CurseForgePublicationRecordService publicationRecordService,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock =
        timeProvider ??
        TimeProvider.System;

    public async Task<CurseForgeUploadExecutionResult>
        ExecuteAsync(
            string apiToken,
            string projectDirectory,
            string version,
            CurseForgeUploadPlan plan,
            bool allowRepublish = false,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            apiToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            version);
        ArgumentNullException.ThrowIfNull(
            plan);

        if (!allowRepublish)
        {
            var existingPublication =
                await publicationRecordService.ReadAsync(
                    projectDirectory,
                    version,
                    cancellationToken);

            if (existingPublication is not null &&
                existingPublication.ProjectId ==
                    plan.ProjectId &&
                string.Equals(
                    existingPublication.ArtifactSha256,
                    plan.ArtifactSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Release '{version.Trim()}' with artifact SHA-256 '{plan.ArtifactSha256}' is already published to CurseForge as file #{existingPublication.FileId}.");
            }
        }

        var upload =
            await uploadApiClient.UploadFileAsync(
                apiToken,
                plan,
                cancellationToken);

        var publicationRecordPath =
            await publicationRecordService.WriteAsync(
                projectDirectory,
                version,
                plan,
                upload,
                clock.GetUtcNow(),
                cancellationToken);

        return new CurseForgeUploadExecutionResult(
            upload,
            publicationRecordPath);
    }
}
