using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class CurseForgeUploadPlanTests
{
    [Fact]
    public async Task Plan_UsesVerifiedArtifactAndMarkdownChangelog()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.1.0");

        var manifest =
            CreateManifest();

        var artifactService =
            new ReleaseArtifactManifestService();

        var workflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                artifactService);

        var release =
            await workflow.PrepareAsync(
                new ReleasePreparationRequest(
                    project.Path,
                    manifest,
                    "1.1.0",
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        Assert.True(
            release.PackageCreated);

        var service =
            new CurseForgeUploadPlanService(
                artifactService);

        var plan =
            await service.CreateAsync(
                project.Path,
                "1.1.0",
                1712846,
                [20001, 20001, 20002],
                "## Changes\n- Fixed something",
                CurseForgeFileReleaseType.Release);

        Assert.Equal(
            1712846,
            plan.ProjectId);
        Assert.Equal(
            CurseForgeChangelogMarkupType.Markdown,
            plan.ChangelogType);
        Assert.Equal(
            CurseForgeFileReleaseType.Release,
            plan.ReleaseType);
        Assert.Equal(
            [20001, 20002],
            plan.GameVersionIds);
        Assert.Equal(
            release.Package!.FileName,
            plan.FileName);
        Assert.Equal(
            release.Package.Sha256,
            plan.ArtifactSha256);
        Assert.Equal(
            release.Package.SizeBytes,
            plan.FileLength);
        Assert.False(
            plan.IsMarkedForManualRelease);
        Assert.Equal(
            "ForeverBag-1.1.0",
            plan.DisplayName);
    }

    [Fact]
    public async Task Plan_CanBeCreatedFromReleaseSettings()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.1.0");

        var artifactService =
            new ReleaseArtifactManifestService();

        var workflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                artifactService);

        await workflow.PrepareAsync(
            new ReleasePreparationRequest(
                project.Path,
                CreateManifest(),
                "1.1.0",
                CurseForgeProjectVerified: true),
            buildPackage: true);

        var settings =
            new CurseForgeReleaseSettings
            {
                Version =
                    "1.1.0",
                GameVersionIds =
                    [13001, 12919],
                ReleaseType =
                    CurseForgeFileReleaseType.Beta,
                IsMarkedForManualRelease =
                    true,
                DisplayName =
                    "ForeverBag 1.1.0 Beta"
            };

        var plan =
            await new CurseForgeUploadPlanService(
                    artifactService)
                .CreateAsync(
                    project.Path,
                    "1.1.0",
                    1712846,
                    settings,
                    "Changes");

        Assert.Equal(
            [13001, 12919],
            plan.GameVersionIds);
        Assert.Equal(
            CurseForgeFileReleaseType.Beta,
            plan.ReleaseType);
        Assert.True(
            plan.IsMarkedForManualRelease);
        Assert.Equal(
            "ForeverBag 1.1.0 Beta",
            plan.DisplayName);
    }

    [Fact]
    public async Task Plan_RejectsReleaseSettingsForDifferentVersion()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.1.0");

        var service =
            new CurseForgeUploadPlanService(
                new ReleaseArtifactManifestService());

        var settings =
            new CurseForgeReleaseSettings
            {
                Version =
                    "1.2.0",
                GameVersionIds =
                    [12919]
            };

        await Assert.ThrowsAsync<
            InvalidDataException>(
                () => service.CreateAsync(
                    project.Path,
                    "1.1.0",
                    1712846,
                    settings,
                    "Changes"));
    }

    [Fact]
    public async Task Plan_RejectsMissingGameVersionSelection()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.1.0");

        var artifactService =
            new ReleaseArtifactManifestService();

        var workflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                artifactService);

        await workflow.PrepareAsync(
            new ReleasePreparationRequest(
                project.Path,
                CreateManifest(),
                "1.1.0",
                CurseForgeProjectVerified: true),
            buildPackage: true);

        var service =
            new CurseForgeUploadPlanService(
                artifactService);

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => service.CreateAsync(
                    project.Path,
                    "1.1.0",
                    1712846,
                    [],
                    "Changes"));

        Assert.Contains(
            "game version",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Plan_RejectsTamperedArtifact()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.1.0");

        var artifactService =
            new ReleaseArtifactManifestService();

        var workflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                artifactService);

        var release =
            await workflow.PrepareAsync(
                new ReleasePreparationRequest(
                    project.Path,
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        await File.AppendAllTextAsync(
            release.Package!.PackagePath,
            "tampered");

        var service =
            new CurseForgeUploadPlanService(
                artifactService);

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => service.CreateAsync(
                    project.Path,
                    "1.1.0",
                    1712846,
                    [20001],
                    "Changes"));

        Assert.Contains(
            "verification failed",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UploadPlan_UsesUploadApiWireSemantics()
    {
        Assert.Equal(
            "release",
            CurseForgeFileReleaseType.Release
                .ToWireValue());
        Assert.Equal(
            "beta",
            CurseForgeFileReleaseType.Beta
                .ToWireValue());
        Assert.Equal(
            "alpha",
            CurseForgeFileReleaseType.Alpha
                .ToWireValue());
        Assert.Equal(
            "markdown",
            CurseForgeChangelogMarkupType.Markdown
                .ToWireValue());
    }

    private static ProjectManifest CreateManifest() =>
        new()
        {
            Project =
                new ProjectIdentity
                {
                    Id = "forever-bag",
                    Name = "ForeverBag",
                    Type = ProjectType.Addon
                },
            Runtime =
                new RuntimeLayout
                {
                    PrimaryAddon =
                        "ForeverBag",
                    Addons =
                        ["ForeverBag"]
                },
            CurseForge =
                new CurseForgeConfiguration
                {
                    ProjectId =
                        "1712846",
                    Slug =
                        "forever-bag",
                    MainCategoryId =
                        "1009",
                    License =
                        "GPL-3.0",
                    AllowDistribution =
                        true
                }
        };

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
                    "project.json"),
                "{}");
        }

        public string Path { get; }

        public void CreateRelease(
            string version)
        {
            var addonDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        Path,
                        "AddOns",
                        "ForeverBag"))
                    .FullName;

            File.WriteAllText(
                System.IO.Path.Combine(
                    addonDirectory,
                    "ForeverBag.toc"),
                $"## Interface: 16001{Environment.NewLine}" +
                $"## Version: {version}{Environment.NewLine}" +
                "ForeverBag.lua");

            File.WriteAllText(
                System.IO.Path.Combine(
                    addonDirectory,
                    "ForeverBag.lua"),
                "-- addon");

            var releaseDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        Path,
                        "Release"))
                    .FullName;

            File.WriteAllText(
                System.IO.Path.Combine(
                    releaseDirectory,
                    "SUMMARY.md"),
                "Summary");

            File.WriteAllText(
                System.IO.Path.Combine(
                    releaseDirectory,
                    "DESCRIPTION.md"),
                "Description");

            var versionDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        releaseDirectory,
                        "Versions",
                        version))
                    .FullName;

            File.WriteAllText(
                System.IO.Path.Combine(
                    versionDirectory,
                    "CHANGELOG.md"),
                "Changes");

            var logoDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        Path,
                        "Media",
                        "Logo"))
                    .FullName;

            File.WriteAllBytes(
                System.IO.Path.Combine(
                    logoDirectory,
                    "logo.png"),
                [1, 2, 3]);
        }

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
