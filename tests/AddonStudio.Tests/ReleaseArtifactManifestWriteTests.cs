using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;

namespace AddonStudio.Tests;

public sealed class ReleaseArtifactManifestWriteTests
{
    [Fact]
    public async Task Write_RejectsInvalidPackageMetadataWithoutReplacingExistingManifest()
    {
        using var project =
            new TemporaryProject();

        var service =
            new ReleaseArtifactManifestService();

        var validPackage =
            new ReleasePackageResult(
                System.IO.Path.Combine(
                    project.VersionDirectory,
                    "ForeverBag-1.2.0.zip"),
                "ForeverBag-1.2.0.zip",
                42,
                new string(
                    'a',
                    64),
                ["ForeverBag/ForeverBag.toc"]);

        var path =
            await service.WriteAsync(
                project.Path,
                "1.2.0",
                validPackage);

        var original =
            await File.ReadAllTextAsync(
                path);

        var invalidPackage =
            validPackage with
            {
                Sha256 =
                    "not-a-sha"
            };

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => service.WriteAsync(
                    project.Path,
                    "1.2.0",
                    invalidPackage));

        Assert.Contains(
            "SHA-256",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        var current =
            await File.ReadAllTextAsync(
                path);

        Assert.Equal(
            original,
            current);

        Assert.Empty(
            Directory.EnumerateFiles(
                project.VersionDirectory,
                "artifact.json.tmp-*",
                SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task Write_NormalizesSha256ToLowercase()
    {
        using var project =
            new TemporaryProject();

        var service =
            new ReleaseArtifactManifestService();

        var package =
            new ReleasePackageResult(
                System.IO.Path.Combine(
                    project.VersionDirectory,
                    "ForeverBag-1.2.0.zip"),
                "ForeverBag-1.2.0.zip",
                42,
                new string(
                    'A',
                    64),
                []);

        await service.WriteAsync(
            project.Path,
            "1.2.0",
            package);

        var manifest =
            await service.ReadAsync(
                project.Path,
                "1.2.0");

        Assert.NotNull(
            manifest);
        Assert.Equal(
            new string(
                'a',
                64),
            manifest.Sha256);
    }

    [Fact]
    public async Task Write_RejectsNonZipPackageFileName()
    {
        using var project =
            new TemporaryProject();

        var service =
            new ReleaseArtifactManifestService();

        var package =
            new ReleasePackageResult(
                System.IO.Path.Combine(
                    project.VersionDirectory,
                    "ForeverBag-1.2.0.txt"),
                "ForeverBag-1.2.0.txt",
                42,
                new string(
                    'a',
                    64),
                []);

        await Assert.ThrowsAsync<
            InvalidDataException>(
                () => service.WriteAsync(
                    project.Path,
                    "1.2.0",
                    package));

        Assert.False(
            File.Exists(
                System.IO.Path.Combine(
                    project.VersionDirectory,
                    ReleaseArtifactManifestService.FileName)));
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

            VersionDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        Path,
                        ProjectLayout.ReleaseDirectoryName,
                        "Versions",
                        "1.2.0"))
                    .FullName;
        }

        public string Path { get; }

        public string VersionDirectory { get; }

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
