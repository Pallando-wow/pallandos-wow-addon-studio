using System.Text.Json;

namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgePublishPreflightService(
    CurseForgePublishPreparationService preparationService,
    CurseForgeUploadApiClient uploadApiClient,
    CurseForgeUploadTokenService uploadTokenService)
{
    public async Task<CurseForgePublishPreflightResult>
        CheckAsync(
            string projectDirectory,
            string version,
            int projectId,
            bool allowRepublish = false,
            CancellationToken cancellationToken = default)
    {
        var preparation =
            await preparationService.PrepareAsync(
                projectDirectory,
                version,
                projectId,
                allowRepublish,
                cancellationToken);

        var issues =
            preparation.Issues.ToList();

        var uploadToken =
            uploadTokenService.Load();

        if (string.IsNullOrWhiteSpace(
                uploadToken))
        {
            return new CurseForgePublishPreflightResult(
                preparation,
                UploadApiChecked: false,
                UploadApiReachable: false,
                SelectedGameVersions: [],
                MissingGameVersionIds: [],
                issues);
        }

        if (preparation.UploadPlan is null)
        {
            return new CurseForgePublishPreflightResult(
                preparation,
                UploadApiChecked: false,
                UploadApiReachable: false,
                SelectedGameVersions: [],
                MissingGameVersionIds: [],
                issues);
        }

        IReadOnlyList<CurseForgeUploadGameVersion>
            availableVersions;

        try
        {
            availableVersions =
                await uploadApiClient.GetGameVersionsAsync(
                    uploadToken,
                    cancellationToken);
        }
        catch (Exception exception)
            when (exception is
                HttpRequestException or
                InvalidDataException or
                JsonException)
        {
            issues.Add(
                "CurseForge Upload API: " +
                exception.Message);

            return new CurseForgePublishPreflightResult(
                preparation,
                UploadApiChecked: true,
                UploadApiReachable: false,
                SelectedGameVersions: [],
                MissingGameVersionIds: [],
                issues);
        }

        var availableById =
            availableVersions
                .GroupBy(
                    candidate =>
                        candidate.Id)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First());

        var selectedVersions =
            preparation.UploadPlan.GameVersionIds
                .Where(
                    availableById.ContainsKey)
                .Select(
                    id =>
                        availableById[id])
                .ToArray();

        var missingVersionIds =
            preparation.UploadPlan.GameVersionIds
                .Where(
                    id =>
                        !availableById.ContainsKey(
                            id))
                .ToArray();

        if (missingVersionIds.Length > 0)
        {
            issues.Add(
                "CurseForge game version ids are no longer available: " +
                string.Join(
                    ", ",
                    missingVersionIds));
        }

        return new CurseForgePublishPreflightResult(
            preparation,
            UploadApiChecked: true,
            UploadApiReachable: true,
            selectedVersions,
            missingVersionIds,
            issues);
    }
}
