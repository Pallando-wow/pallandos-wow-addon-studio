using AddonStudio.Packaging.Releases;

namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgeUploadPlanService(
    ReleaseArtifactManifestService artifactManifestService)
{
    public Task<CurseForgeUploadPlan> CreateAsync(
        string projectDirectory,
        string version,
        int projectId,
        CurseForgeReleaseSettings settings,
        string changelog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        if (!string.Equals(
                settings.Version,
                version?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"CurseForge release settings version '{settings.Version}' does not match release '{version?.Trim()}'.");
        }

        return CreateAsync(
            projectDirectory,
            version,
            projectId,
            settings.GameVersionIds,
            changelog,
            settings.ReleaseType,
            settings.IsMarkedForManualRelease,
            settings.DisplayName,
            cancellationToken);
    }

    public async Task<CurseForgeUploadPlan> CreateAsync(
        string projectDirectory,
        string version,
        int projectId,
        IReadOnlyList<int> gameVersionIds,
        string changelog,
        CurseForgeFileReleaseType releaseType =
            CurseForgeFileReleaseType.Release,
        bool isMarkedForManualRelease = false,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            version);
        ArgumentNullException.ThrowIfNull(
            gameVersionIds);
        ArgumentNullException.ThrowIfNull(
            changelog);

        if (projectId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectId));
        }

        var versionIds =
            gameVersionIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

        if (versionIds.Length == 0)
        {
            throw new InvalidDataException(
                "At least one CurseForge game version must be selected.");
        }

        if (string.IsNullOrWhiteSpace(
                changelog))
        {
            throw new InvalidDataException(
                "CurseForge upload changelog must not be empty.");
        }

        if (!Enum.IsDefined(
                releaseType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(releaseType));
        }

        var verification =
            await artifactManifestService.VerifyAsync(
                projectDirectory,
                version,
                cancellationToken);

        if (!verification.IsValid)
        {
            throw new InvalidDataException(
                "Release artifact verification failed: " +
                string.Join(
                    " ",
                    verification.Issues));
        }

        var artifact =
            await artifactManifestService.ReadAsync(
                projectDirectory,
                version,
                cancellationToken)
            ?? throw new InvalidDataException(
                "Release artifact manifest is missing.");

        var packagePath =
            Path.Combine(
                Path.GetDirectoryName(
                    artifactManifestService.GetManifestPath(
                        projectDirectory,
                        version))!,
                artifact.PackageFileName);

        if (!File.Exists(
                packagePath))
        {
            throw new FileNotFoundException(
                "Verified release package is missing.",
                packagePath);
        }

        var uploadDisplayName =
            string.IsNullOrWhiteSpace(
                displayName)
                ? Path.GetFileNameWithoutExtension(
                    artifact.PackageFileName)
                : displayName.Trim();

        return new CurseForgeUploadPlan(
            projectId,
            packagePath,
            artifact.PackageFileName,
            uploadDisplayName,
            changelog,
            CurseForgeChangelogMarkupType.Markdown,
            versionIds,
            releaseType,
            artifact.SizeBytes,
            isMarkedForManualRelease,
            artifact.Sha256);
    }
}
