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
