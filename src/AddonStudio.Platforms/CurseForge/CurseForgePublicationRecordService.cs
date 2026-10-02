using System.Text.Json;
using System.Text.Json.Serialization;
using AddonStudio.Packaging.Releases;

namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgePublicationRecordService(
    ReleaseArtifactManifestService artifactManifestService)
{
    public const string FileName =
        "curseforge-publication.json";

    private static readonly JsonSerializerOptions
        SerializerOptions =
            new()
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition =
                    JsonIgnoreCondition.WhenWritingNull
            };

    public async Task<string> WriteAsync(
        string projectDirectory,
        string version,
        CurseForgeUploadPlan plan,
        CurseForgeUploadResult result,
        DateTimeOffset uploadedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            plan);
        ArgumentNullException.ThrowIfNull(
            result);

        if (result.FileId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(result));
        }

        var artifact =
            await artifactManifestService.ReadAsync(
                projectDirectory,
                version,
                cancellationToken)
            ?? throw new InvalidDataException(
                "Release artifact manifest is missing.");

        if (!string.Equals(
                artifact.PackageFileName,
                plan.FileName,
                StringComparison.Ordinal) ||
            !string.Equals(
                artifact.Sha256,
                plan.ArtifactSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "CurseForge upload plan does not match the release artifact manifest.");
        }

        var path =
            GetPath(
                projectDirectory,
                version);

        var record =
            new CurseForgePublicationRecord
            {
                Version =
                    version.Trim(),
                ProjectId =
                    plan.ProjectId,
                FileId =
                    result.FileId,
                PackageFileName =
                    plan.FileName,
                ArtifactSha256 =
                    plan.ArtifactSha256,
                GameVersionIds =
                    plan.GameVersionIds
                        .Distinct()
                        .ToArray(),
                ReleaseType =
                    plan.ReleaseType
                        .ToWireValue(),
                IsMarkedForManualRelease =
                    plan.IsMarkedForManualRelease,
                UploadedAtUtc =
                    uploadedAtUtc.ToUniversalTime()
            };

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
                    record,
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

    public async Task<CurseForgePublicationRecord?> ReadAsync(
        string projectDirectory,
        string version,
        CancellationToken cancellationToken = default)
    {
        var path =
            GetPath(
                projectDirectory,
                version);

        if (!File.Exists(path))
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

        var record =
            await JsonSerializer.DeserializeAsync<
                CurseForgePublicationRecord>(
                stream,
                SerializerOptions,
                cancellationToken);

        if (record is null)
        {
            throw new InvalidDataException(
                $"CurseForge publication record '{path}' is empty.");
        }

        if (record.SchemaVersion !=
            CurseForgePublicationRecord
                .CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported CurseForge publication schema version '{record.SchemaVersion}'.");
        }

        var normalizedVersion =
            version.Trim();

        if (string.IsNullOrWhiteSpace(
                record.Version) ||
            !string.Equals(
                record.Version,
                normalizedVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"CurseForge publication version '{record.Version}' does not match release directory '{normalizedVersion}'.");
        }

        if (record.ProjectId <= 0 ||
            record.FileId <= 0)
        {
            throw new InvalidDataException(
                "CurseForge publication record contains invalid project or file ids.");
        }

        if (!IsSimpleZipFileName(
                record.PackageFileName))
        {
            throw new InvalidDataException(
                "CurseForge publication package file name is invalid.");
        }

        if (!IsSha256(
                record.ArtifactSha256))
        {
            throw new InvalidDataException(
                "CurseForge publication artifact SHA-256 is invalid.");
        }

        if (record.GameVersionIds.Count == 0 ||
            record.GameVersionIds.Any(
                id => id <= 0))
        {
            throw new InvalidDataException(
                "CurseForge publication record contains invalid game version ids.");
        }

        if (record.ReleaseType is not
            ("release" or "beta" or "alpha"))
        {
            throw new InvalidDataException(
                $"CurseForge publication release type '{record.ReleaseType}' is invalid.");
        }

        return record;
    }

    private static bool IsSimpleZipFileName(
        string? fileName)
    {
        if (string.IsNullOrWhiteSpace(
                fileName))
        {
            return false;
        }

        var value =
            fileName.Trim();

        return string.Equals(
                Path.GetFileName(
                    value),
                value,
                StringComparison.Ordinal) &&
            !value.Contains(
                '/') &&
            !value.Contains(
                '\\') &&
            value.EndsWith(
                ".zip",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSha256(
        string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            char.IsAsciiHexDigit(
                character));

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
}
