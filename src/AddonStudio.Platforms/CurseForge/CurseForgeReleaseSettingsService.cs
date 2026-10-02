using System.Text.Json;
using System.Text.Json.Serialization;
using AddonStudio.Packaging.Releases;

namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgeReleaseSettingsService(
    ReleaseArtifactManifestService artifactManifestService)
{
    public const string FileName =
        "curseforge-release.json";

    private static readonly JsonSerializerOptions
        SerializerOptions =
            CreateSerializerOptions();

    public async Task<string> WriteAsync(
        string projectDirectory,
        string version,
        IReadOnlyList<int> gameVersionIds,
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

        if (!Enum.IsDefined(
                releaseType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(releaseType));
        }

        var normalizedIds =
            gameVersionIds
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();

        if (normalizedIds.Length == 0)
        {
            throw new InvalidDataException(
                "At least one CurseForge game version must be selected.");
        }

        var normalizedVersion =
            version.Trim();

        var settings =
            new CurseForgeReleaseSettings
            {
                Version =
                    normalizedVersion,
                GameVersionIds =
                    normalizedIds,
                ReleaseType =
                    releaseType,
                IsMarkedForManualRelease =
                    isMarkedForManualRelease,
                DisplayName =
                    string.IsNullOrWhiteSpace(
                        displayName)
                        ? null
                        : displayName.Trim()
            };

        var path =
            GetPath(
                projectDirectory,
                normalizedVersion);

        var temporaryPath =
            path +
            ".tmp-" +
            Guid.NewGuid().ToString("N");

        try
        {
            await using (
                var stream =
                    new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    SerializerOptions,
                    cancellationToken);

                await stream.FlushAsync(
                    cancellationToken);
            }

            File.Move(
                temporaryPath,
                path,
                overwrite: true);
        }
        catch
        {
            if (File.Exists(
                    temporaryPath))
            {
                File.Delete(
                    temporaryPath);
            }

            throw;
        }

        return path;
    }

    public async Task<CurseForgeReleaseSettings?> ReadAsync(
        string projectDirectory,
        string version,
        CancellationToken cancellationToken = default)
    {
        var normalizedVersion =
            version?.Trim();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            normalizedVersion);

        var path =
            GetPath(
                projectDirectory,
                normalizedVersion);

        if (!File.Exists(
                path))
        {
            return null;
        }

        await using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);

        var settings =
            await JsonSerializer.DeserializeAsync<
                CurseForgeReleaseSettings>(
                stream,
                SerializerOptions,
                cancellationToken)
            ?? throw new InvalidDataException(
                $"CurseForge release settings '{path}' are empty.");

        if (settings.SchemaVersion !=
            CurseForgeReleaseSettings
                .CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported CurseForge release settings schema version '{settings.SchemaVersion}'.");
        }

        if (!string.Equals(
                settings.Version,
                normalizedVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"CurseForge release settings version '{settings.Version}' does not match release directory '{normalizedVersion}'.");
        }

        if (settings.GameVersionIds.Count == 0 ||
            settings.GameVersionIds.Any(
                id => id <= 0))
        {
            throw new InvalidDataException(
                "CurseForge release settings do not contain valid game version ids.");
        }

        if (!Enum.IsDefined(
                settings.ReleaseType))
        {
            throw new InvalidDataException(
                "CurseForge release settings contain an invalid release type.");
        }

        return settings;
    }

    public void Delete(
        string projectDirectory,
        string version)
    {
        var path =
            GetPath(
                projectDirectory,
                version);

        if (File.Exists(
                path))
        {
            File.Delete(
                path);
        }
    }

    public string GetPath(
        string projectDirectory,
        string version)
    {
        var artifactPath =
            artifactManifestService.GetManifestPath(
                projectDirectory,
                version);

        return Path.Combine(
            Path.GetDirectoryName(
                artifactPath)!,
            FileName);
    }

    private static JsonSerializerOptions
        CreateSerializerOptions()
    {
        var options =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                UnmappedMemberHandling =
                    JsonUnmappedMemberHandling.Disallow
            };

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));

        return options;
    }
}
