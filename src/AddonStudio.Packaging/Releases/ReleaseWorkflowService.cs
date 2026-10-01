namespace AddonStudio.Packaging.Releases;

public sealed record ReleaseWorkflowResult(
    ReleasePreparationSnapshot Preparation,
    ReleasePackageResult? Package)
{
    public bool PackageCreated =>
        Package is not null;
}

public sealed class ReleaseWorkflowService(
    ReleasePreparationService preparationService,
    ReleasePackageBuilder packageBuilder)
{
    public async Task<ReleaseWorkflowResult> PrepareAsync(
        ReleasePreparationRequest request,
        bool buildPackage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var preparation =
            preparationService.Inspect(
                request);

        if (!buildPackage ||
            !preparation.IsReady)
        {
            return new ReleaseWorkflowResult(
                preparation,
                null);
        }

        var package =
            await packageBuilder.BuildAsync(
                new ReleasePackageRequest(
                    request.ProjectDirectory,
                    request.Manifest,
                    request.Version),
                cancellationToken);

        return new ReleaseWorkflowResult(
            preparation,
            package);
    }
}
