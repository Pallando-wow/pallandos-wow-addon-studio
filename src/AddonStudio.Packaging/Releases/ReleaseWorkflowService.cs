namespace AddonStudio.Packaging.Releases;

public sealed record ReleaseWorkflowResult(
    ReleasePreparationSnapshot Preparation,
    ReleasePackageResult? Package,
    string? ArtifactManifestPath)
{
    public bool PackageCreated =>
        Package is not null;

    public bool ArtifactManifestCreated =>
        !string.IsNullOrWhiteSpace(
            ArtifactManifestPath);
}

public sealed class ReleaseWorkflowService(
    ReleasePreparationService preparationService,
    ReleasePackageBuilder packageBuilder,
    ReleaseArtifactManifestService artifactManifestService)
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
                null,
                null);
        }

        var package =
            await packageBuilder.BuildAsync(
                new ReleasePackageRequest(
                    request.ProjectDirectory,
                    request.Manifest,
                    request.Version),
                cancellationToken);

        var artifactManifestPath =
            await artifactManifestService.WriteAsync(
                request.ProjectDirectory,
                request.Version,
                package,
                cancellationToken);

        return new ReleaseWorkflowResult(
            preparation,
            package,
            artifactManifestPath);
    }
}
