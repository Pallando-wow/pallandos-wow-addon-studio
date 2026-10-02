using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class ReleaseMetadataValidationTests
{
    [Fact]
    public async Task ArtifactRead_RejectsVersionMismatch()
    {
        using var project =
            new TemporaryProject();

        var versionDirectory =
            project.CreateVersionDirectory(
                "1.2.0");

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(
                versionDirectory,
                ReleaseArtifactManifestService.FileName),
            """
            {
              "schemaVersion": 1,
              "version": "1.3.0",
              "packageFileName": "ForeverBag-1.2.0.zip",
              "sizeBytes": 12,
              "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "entries": []
            }
            """);

        var service =
            new ReleaseArtifactManifestService();

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => service.ReadAsync(
                    project.Path,
                    "1.2.0"));

        Assert.Contains(
            "does not match",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ArtifactRead_RejectsInvalidSha256()
    {
        using var project =
            new TemporaryProject();

        var versionDirectory =
            project.CreateVersionDirectory(
                "1.2.0");

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(
                versionDirectory,
                ReleaseArtifactManifestService.FileName),
            """
            {
              "schemaVersion": 1,
              "version": "1.2.0",
              "packageFileName": "ForeverBag-1.2.0.zip",
              "sizeBytes": 12,
              "sha256": "not-a-sha",
              "entries": []
            }
            """);

        var service =
            new ReleaseArtifactManifestService();

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => service.ReadAsync(
                    project.Path,
                    "1.2.0"));

        Assert.Contains(
            "SHA-256",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicationRead_RejectsVersionMismatch()
    {
        using var project =
            new TemporaryProject();

        var versionDirectory =
            project.CreateVersionDirectory(
                "1.2.0");

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(
                versionDirectory,
                CurseForgePublicationRecordService.FileName),
            ValidPublicationJson(
                version: "1.3.0"));

        var service =
            new CurseForgePublicationRecordService(
                new ReleaseArtifactManifestService());

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => service.ReadAsync(
                    project.Path,
                    "1.2.0"));

        Assert.Contains(
            "does not match",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicationRead_RejectsInvalidRemoteMetadata()
    {
        using var project =
            new TemporaryProject();

        var versionDirectory =
            project.CreateVersionDirectory(
                "1.2.0");

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(
                versionDirectory,
                CurseForgePublicationRecordService.FileName),
            """
            {
              "schemaVersion": 1,
              "version": "1.2.0",
              "projectId": 1712846,
              "fileId": 0,
              "packageFileName": "ForeverBag-1.2.0.zip",
              "artifactSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "gameVersionIds": [12919],
              "releaseType": "release",
              "isMarkedForManualRelease": false,
              "uploadedAtUtc": "2026-10-01T08:00:00+00:00"
            }
            """);

        var service =
            new CurseForgePublicationRecordService(
                new ReleaseArtifactManifestService());

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => service.ReadAsync(
                    project.Path,
                    "1.2.0"));

        Assert.Contains(
            "project or file ids",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ValidPublicationJson(
        string version) =>
        $$"""
        {
          "schemaVersion": 1,
          "version": "{{version}}",
          "projectId": 1712846,
          "fileId": 20402,
          "packageFileName": "ForeverBag-1.2.0.zip",
          "artifactSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "gameVersionIds": [12919],
          "releaseType": "release",
          "isMarkedForManualRelease": false,
          "uploadedAtUtc": "2026-10-01T08:00:00+00:00"
        }
        """;

    private sealed class TemporaryProject :
        IDisposable
    {
        public TemporaryProject()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "AddonStudio.Tests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                Path);

            File.WriteAllText(
                System.IO.Path.Combine(
                    Path,
                    ProjectLayout.ManifestFileName),
                "{}");
        }

        public string Path { get; }

        public string CreateVersionDirectory(
            string version) =>
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    Path,
                    ProjectLayout.ReleaseDirectoryName,
                    "Versions",
                    version))
                .FullName;

        public void Dispose()
        {
            if (Directory.Exists(
                    Path))
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
        }
    }
}
