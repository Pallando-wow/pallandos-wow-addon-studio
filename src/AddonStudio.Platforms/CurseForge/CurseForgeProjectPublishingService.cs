using System.Globalization;
using AddonStudio.Core.Projects;

namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgeProjectPublishingService(
    CurseForgePublishPreflightService preflightService,
    CurseForgeConfirmedPublishService confirmedPublishService)
{
    public Task<CurseForgePublishPreflightResult>
        PrepareAsync(
            string projectDirectory,
            ProjectManifest manifest,
            string version,
            bool allowRepublish = false,
            CancellationToken cancellationToken = default)
    {
        var projectId =
            ResolveProjectId(
                manifest);

        return preflightService.CheckAsync(
            projectDirectory,
            version,
            projectId,
            allowRepublish,
            cancellationToken);
    }

    public Task<CurseForgeConfirmedPublishResult>
        PublishConfirmedAsync(
            string projectDirectory,
            ProjectManifest manifest,
            string version,
            string artifactSha256,
            bool allowRepublish = false,
            CancellationToken cancellationToken = default)
    {
        var projectId =
            ResolveProjectId(
                manifest);

        return confirmedPublishService.ExecuteConfirmedAsync(
            projectDirectory,
            new CurseForgeConfirmedPublishRequest(
                version,
                projectId,
                artifactSha256,
                allowRepublish),
            cancellationToken);
    }

    public static int ResolveProjectId(
        ProjectManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(
            manifest);

        var configuredProjectId =
            manifest.CurseForge?.ProjectId;

        if (string.IsNullOrWhiteSpace(
                configuredProjectId))
        {
            throw new InvalidDataException(
                "Project does not contain a CurseForge project id.");
        }

        if (!int.TryParse(
                configuredProjectId.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var projectId) ||
            projectId <= 0)
        {
            throw new InvalidDataException(
                $"CurseForge project id '{configuredProjectId}' is invalid.");
        }

        return projectId;
    }
}
