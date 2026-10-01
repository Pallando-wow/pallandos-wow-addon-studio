using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AddonStudio.Core.Projects;

namespace AddonStudio.Packaging.Releases;

public sealed class ReleaseArtifactManifestService
{
    public const string FileName =
        "artifact.json";

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
        ReleasePackageResult package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var normalizedVersion =
            version?.Trim();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            normalizedVersion);

        var packageFileName =
            ReleasePathRules
                .RequireSimpleZipFileName(
                    package.FileName);

        if (package.SizeBytes < 0)
        {
            throw new InvalidDataException(
                "Release package size must not be negative.");
        }

        if (!IsSha256(
                package.Sha256))
        {
            throw new InvalidDataException(
                "Release package SHA-256 is invalid.");
        }

        var path =
            GetManifestPath(
                projectDirectory,
                normalizedVersion);

        var manifest =
            new ReleaseArtifactManifest
            {
                Version =
                    normalizedVersion,
                PackageFileName =
                    packageFileName,
                SizeBytes =
                    package.SizeBytes,
                Sha256 =
                    package.Sha256.ToLowerInvariant(),
                Entries =
                    package.Entries
                        .OrderBy(
                            entry => entry,
                            StringComparer.Ordinal)
                        .ToArray()
            };

        var directory =
            Path.GetDirectoryName(
                path)!;

        Directory.CreateDirectory(
            directory);

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
                    manifest,
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

    public async Task<ReleaseArtifactManifest?> ReadAsync(
        string projectDirectory,
        string version,
        CancellationToken cancellationToken = default)
    {
        var path =
            GetManifestPath(
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

        var manifest =
            await JsonSerializer.DeserializeAsync<
                ReleaseArtifactManifest>(
                stream,
                SerializerOptions,
                cancellationToken);

        if (manifest is null)
        {
            throw new InvalidDataException(
                $"Release artifact manifest '{path}' is empty.");
        }

        if (manifest.SchemaVersion !=
            ReleaseArtifactManifest.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported release artifact schema version '{manifest.SchemaVersion}'.");
        }

        var normalizedVersion =
            version.Trim();

        if (string.IsNullOrWhiteSpace(
                manifest.Version) ||
            !string.Equals(
                manifest.Version,
                normalizedVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Release artifact version '{manifest.Version}' does not match release directory '{normalizedVersion}'.");
        }

        ReleasePathRules.RequireSimpleZipFileName(
            manifest.PackageFileName);

        if (manifest.SizeBytes < 0)
        {
            throw new InvalidDataException(
                "Release artifact size must not be negative.");
        }

        if (!IsSha256(
                manifest.Sha256))
        {
            throw new InvalidDataException(
                "Release artifact SHA-256 is invalid.");
        }

        return manifest;
    }

    public async Task<ReleaseArtifactVerificationResult>
        VerifyAsync(
            string projectDirectory,
            string version,
            CancellationToken cancellationToken = default)
    {
        var issues =
            new List<string>();

        var manifest =
            await ReadAsync(
                projectDirectory,
                version,
                cancellationToken);

        if (manifest is null)
        {
            issues.Add(
                "Release artifact manifest is missing.");

            return new ReleaseArtifactVerificationResult(
                false,
                issues);
        }

        var versionDirectory =
            GetVersionDirectory(
                projectDirectory,
                version);

        var packageFileName =
            ReleasePathRules
                .RequireSimpleZipFileName(
                    manifest.PackageFileName);

        var packagePath =
            Path.Combine(
                versionDirectory,
                packageFileName);

        if (!File.Exists(
                packagePath))
        {
            issues.Add(
                $"Release package '{manifest.PackageFileName}' is missing.");

            return new ReleaseArtifactVerificationResult(
                false,
                issues);
        }

        var size =
            new FileInfo(
                packagePath)
                .Length;

        if (size != manifest.SizeBytes)
        {
            issues.Add(
                $"Package size mismatch: expected {manifest.SizeBytes}, actual {size}.");
        }

        var sha256 =
            await ComputeSha256Async(
                packagePath,
                cancellationToken);

        if (!string.Equals(
                sha256,
                manifest.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                "Package SHA-256 does not match the artifact manifest.");
        }

        using var archive =
            ZipFile.OpenRead(
                packagePath);

        var entries =
            archive.Entries
                .Select(entry =>
                    entry.FullName)
                .OrderBy(
                    entry => entry,
                    StringComparer.Ordinal)
                .ToArray();

        var expectedEntries =
            manifest.Entries
                .OrderBy(
                    entry => entry,
                    StringComparer.Ordinal)
                .ToArray();

        if (!entries.SequenceEqual(
                expectedEntries,
                StringComparer.Ordinal))
        {
            issues.Add(
                "Package entries do not match the artifact manifest.");
        }

        return new ReleaseArtifactVerificationResult(
            issues.Count == 0,
            issues);
    }

    public string GetManifestPath(
        string projectDirectory,
        string version) =>
        Path.Combine(
            GetVersionDirectory(
                projectDirectory,
                version),
            FileName);

    private static string GetVersionDirectory(
        string projectDirectory,
        string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            version);

        var projectPath =
            Path.GetFullPath(
                projectDirectory);

        if (!File.Exists(
                Path.Combine(
                    projectPath,
                    ProjectLayout.ManifestFileName)))
        {
            throw new InvalidOperationException(
                $"Directory '{projectPath}' is not a managed Studio project.");
        }

        var versionDirectoryName =
            ReleasePathRules
                .RequireVersionDirectoryName(
                    version);

        return Path.Combine(
            projectPath,
            ProjectLayout.ReleaseDirectoryName,
            "Versions",
            versionDirectoryName);
    }

    private static bool IsSha256(
        string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            char.IsAsciiHexDigit(
                character));

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);

        var hash =
            await SHA256.HashDataAsync(
                stream,
                cancellationToken);

        return Convert.ToHexString(
            hash)
            .ToLowerInvariant();
    }
}
