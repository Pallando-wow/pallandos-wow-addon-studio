using AddonStudio.Core.Projects;

namespace AddonStudio.Application.Projects;

public sealed class ProjectCurseForgeSettingsService(
    IProjectManifestWriter manifestWriter)
{
    public async Task<ProjectManifest> SaveAsync(
        string projectDirectory,
        ProjectManifest manifest,
        CurseForgeConfiguration? curseForge,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);
        ArgumentNullException.ThrowIfNull(manifest);

        var normalized =
            Normalize(curseForge);

        var updatedManifest =
            manifest with
            {
                CurseForge = normalized
            };

        var manifestPath = Path.Combine(
            projectDirectory,
            ProjectLayout.ManifestFileName);

        await manifestWriter.WriteAsync(
            manifestPath,
            updatedManifest,
            cancellationToken);

        return updatedManifest;
    }

    private static CurseForgeConfiguration? Normalize(
        CurseForgeConfiguration? configuration)
    {
        if (configuration is null)
        {
            return null;
        }

        var additionalCategories =
            configuration.AdditionalCategoryIds
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var normalized =
            configuration with
            {
                ProjectId =
                    NormalizeText(
                        configuration.ProjectId),
                Slug =
                    NormalizeText(
                        configuration.Slug),
                MainCategoryId =
                    NormalizeText(
                        configuration.MainCategoryId),
                AdditionalCategoryIds =
                    additionalCategories,
                License =
                    NormalizeText(
                        configuration.License)
            };

        var isEmpty =
            normalized.ProjectId is null &&
            normalized.Slug is null &&
            normalized.MainCategoryId is null &&
            normalized.AdditionalCategoryIds.Count == 0 &&
            normalized.License is null &&
            normalized.AllowDistribution is null;

        return isEmpty
            ? null
            : normalized;
    }

    private static string? NormalizeText(
        string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
