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

        var path =
            GetManifestPath(
                projectDirectory,
                version);

        var manifest =
            new ReleaseArtifactManifest
            {
                Version = version.Trim(),
                PackageFileName =
                    package.FileName,
                SizeBytes =
                    package.SizeBytes,
                Sha256 =
                    package.Sha256,
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

        await using var stream =
            new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);

        await JsonSerializer.SerializeAsync(
            stream,
            manifest,
            SerializerOptions,
            cancellationToken);

        await stream.FlushAsync(
            cancellationToken);

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

        var packagePath =
            Path.Combine(
                versionDirectory,
                manifest.PackageFileName);

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

        return Path.Combine(
            projectPath,
            ProjectLayout.ReleaseDirectoryName,
            "Versions",
            version.Trim());
    }

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
