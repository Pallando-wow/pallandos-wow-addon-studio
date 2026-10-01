namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgeConfirmedPublishService(
    CurseForgePublishPreflightService preflightService,
    CurseForgeUploadTokenService uploadTokenService,
    CurseForgeUploadExecutionService executionService)
{
    public async Task<CurseForgeConfirmedPublishResult>
        ExecuteConfirmedAsync(
            string projectDirectory,
            CurseForgeConfirmedPublishRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);
        ArgumentNullException.ThrowIfNull(
            request);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ArtifactSha256);

        if (request.ProjectId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.ProjectId));
        }

        var normalizedVersion =
            request.Version.Trim();

        var preflight =
            await preflightService.CheckAsync(
                projectDirectory,
                normalizedVersion,
                request.ProjectId,
                request.AllowRepublish,
                cancellationToken);

        if (!preflight.IsReadyForExplicitUpload)
        {
            var detail =
                preflight.Issues.Count == 0
                    ? "Publishing preflight is not ready."
                    : string.Join(
                        " ",
                        preflight.Issues);

            throw new InvalidOperationException(
                "CurseForge publishing confirmation cannot be executed: " +
                detail);
        }

        var plan =
            preflight.LocalPreparation.UploadPlan
            ?? throw new InvalidOperationException(
                "CurseForge publishing preflight did not produce an upload plan.");

        if (plan.ProjectId !=
            request.ProjectId)
        {
            throw new InvalidOperationException(
                $"Confirmed CurseForge project id '{request.ProjectId}' does not match upload plan project id '{plan.ProjectId}'.");
        }

        if (!string.Equals(
                plan.ArtifactSha256,
                request.ArtifactSha256.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The release artifact changed after publishing was confirmed. Run the preflight again and confirm the current artifact.");
        }

        var uploadToken =
            uploadTokenService.Load();

        if (string.IsNullOrWhiteSpace(
                uploadToken))
        {
            throw new InvalidOperationException(
                "CurseForge upload token is missing after preflight.");
        }

        var execution =
            await executionService.ExecuteAsync(
                uploadToken,
                projectDirectory,
                normalizedVersion,
                plan,
                request.AllowRepublish,
                cancellationToken);

        return new CurseForgeConfirmedPublishResult(
            preflight,
            execution);
    }
}
