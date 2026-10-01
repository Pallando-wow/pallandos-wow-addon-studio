using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class CurseForgeReleaseSettingsServiceTests
{
    [Fact]
    public async Task WriteAndRead_NormalizeReleaseSettings()
    {
        using var project =
            new TemporaryProject();

        project.CreateVersionDirectory(
            "1.2.0");

        var service =
            new CurseForgeReleaseSettingsService(
                new ReleaseArtifactManifestService());

        var path =
            await service.WriteAsync(
                project.Path,
                "1.2.0",
                [13001, 12919, 13001],
                CurseForgeFileReleaseType.Beta,
                isMarkedForManualRelease: true,
                displayName: "  ForeverBag 1.2.0 Beta  ");

        Assert.True(
            File.Exists(
                path));

        var raw =
            await File.ReadAllTextAsync(
                path);

        Assert.Contains(
            ""releaseType": "beta"",
            raw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "upload-token",
            raw,
            StringComparison.OrdinalIgnoreCase);

        var settings =
            await service.ReadAsync(
                project.Path,
                "1.2.0");

        Assert.NotNull(
            settings);
        Assert.Equal(
            "1.2.0",
            settings.Version);
        Assert.Equal(
            [12919, 13001],
            settings.GameVersionIds);
        Assert.Equal(
            CurseForgeFileReleaseType.Beta,
            settings.ReleaseType);
        Assert.True(
            settings.IsMarkedForManualRelease);
        Assert.Equal(
            "ForeverBag 1.2.0 Beta",
            settings.DisplayName);
    }

    [Fact]
    public async Task Write_RequiresAtLeastOneValidGameVersionId()
    {
        using var project =
            new TemporaryProject();

        project.CreateVersionDirectory(
            "1.2.0");

        var service =
            new CurseForgeReleaseSettingsService(
                new ReleaseArtifactManifestService());

        await Assert.ThrowsAsync<
            InvalidDataException>(
                () => service.WriteAsync(
                    project.Path,
                    "1.2.0",
                    [0, -1]));
    }

    [Fact]
    public async Task Read_RejectsVersionMismatch()
    {
        using var project =
            new TemporaryProject();

        var versionDirectory =
            project.CreateVersionDirectory(
                "1.2.0");

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(
                versionDirectory,
                CurseForgeReleaseSettingsService.FileName),
            """
            {
              "schemaVersion": 1,
              "version": "1.3.0",
              "gameVersionIds": [12919],
              "releaseType": "release",
              "isMarkedForManualRelease": false
            }
            """);

        var service =
            new CurseForgeReleaseSettingsService(
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
    public async Task Delete_RemovesOnlyReleaseSettingsFile()
    {
        using var project =
            new TemporaryProject();

        var versionDirectory =
            project.CreateVersionDirectory(
                "1.2.0");

        var artifactPath =
            System.IO.Path.Combine(
                versionDirectory,
                ReleaseArtifactManifestService.FileName);

        await File.WriteAllTextAsync(
            artifactPath,
            "{}");

        var service =
            new CurseForgeReleaseSettingsService(
                new ReleaseArtifactManifestService());

        var settingsPath =
            await service.WriteAsync(
                project.Path,
                "1.2.0",
                [12919]);

        service.Delete(
            project.Path,
            "1.2.0");

        Assert.False(
            File.Exists(
                settingsPath));
        Assert.True(
            File.Exists(
                artifactPath));
    }

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
