using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class CurseForgeReleaseOverviewServiceTests
{
    [Fact]
    public async Task Overview_ReportsReadyUnpublishedRelease()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0");

        var artifactService =
            new ReleaseArtifactManifestService();

        var releaseWorkflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                artifactService);

        await releaseWorkflow.PrepareAsync(
            new ReleasePreparationRequest(
                project.Path,
                CreateManifest(),
                "1.2.0",
                CurseForgeProjectVerified: true),
            buildPackage: true);

        var settingsService =
            new CurseForgeReleaseSettingsService(
                artifactService);

        await settingsService.WriteAsync(
            project.Path,
            "1.2.0",
            [12919, 13001],
            CurseForgeFileReleaseType.Beta,
            displayName: "ForeverBag 1.2.0 Beta");

        var overview =
            await CreateService(
                    artifactService,
                    settingsService)
                .GetOverviewAsync(
                    project.Path);

        var entry =
            Assert.Single(
                overview);

        Assert.Equal(
            "1.2.0",
            entry.Version);
        Assert.Equal(
            CurseForgePublicationState.NotPublished,
            entry.PublicationState);
        Assert.True(
            entry.IsReadyForUpload);
        Assert.Empty(
            entry.Issues);
        Assert.True(
            entry.HasChangelog);
        Assert.True(
            entry.HasArtifactManifest);
        Assert.True(
            entry.HasReleaseSettings);
        Assert.Equal(
            [12919, 13001],
            entry.GameVersionIds);
        Assert.Equal(
            CurseForgeFileReleaseType.Beta,
            entry.ReleaseType);
        Assert.Equal(
            "ForeverBag 1.2.0 Beta",
            entry.DisplayName);
        Assert.Null(
            entry.FileId);
    }

    [Fact]
    public async Task Overview_ReportsMissingReleaseSettings()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0");

        var artifactService =
            new ReleaseArtifactManifestService();

        var releaseWorkflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                artifactService);

        await releaseWorkflow.PrepareAsync(
            new ReleasePreparationRequest(
                project.Path,
                CreateManifest(),
                "1.2.0",
                CurseForgeProjectVerified: true),
            buildPackage: true);

        var settingsService =
            new CurseForgeReleaseSettingsService(
                artifactService);

        var overview =
            await CreateService(
                    artifactService,
                    settingsService)
                .GetOverviewAsync(
                    project.Path);

        var entry =
            Assert.Single(
                overview);

        Assert.False(
            entry.IsReadyForUpload);
        Assert.False(
            entry.HasReleaseSettings);
        Assert.Contains(
            entry.Issues,
            issue =>
                issue.Contains(
                    "release settings",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Overview_DetectsArtifactChangedAfterPublication()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0");

        var artifactService =
            new ReleaseArtifactManifestService();

        var releaseWorkflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                artifactService);

        await releaseWorkflow.PrepareAsync(
            new ReleasePreparationRequest(
                project.Path,
                CreateManifest(),
                "1.2.0",
                CurseForgeProjectVerified: true),
            buildPackage: true);

        var settingsService =
            new CurseForgeReleaseSettingsService(
                artifactService);

        await settingsService.WriteAsync(
            project.Path,
            "1.2.0",
            [12919]);

        var plan =
            await new CurseForgeUploadPlanService(
                    artifactService)
                .CreateAsync(
                    project.Path,
                    "1.2.0",
                    1712846,
                    [12919],
                    "Changes");

        var publicationService =
            new CurseForgePublicationRecordService(
                artifactService);

        await publicationService.WriteAsync(
            project.Path,
            "1.2.0",
            plan,
            new CurseForgeUploadResult(
                20402),
            DateTimeOffset.UtcNow);

        await File.AppendAllTextAsync(
            System.IO.Path.Combine(
                project.Path,
                "AddOns",
                "ForeverBag",
                "ForeverBag.lua"),
            Environment.NewLine +
            "-- changed");

        await releaseWorkflow.PrepareAsync(
            new ReleasePreparationRequest(
                project.Path,
                CreateManifest(),
                "1.2.0",
                CurseForgeProjectVerified: true),
            buildPackage: true);

        var overview =
            await new CurseForgeReleaseOverviewService(
                    new ReleaseHistoryService(),
                    artifactService,
                    settingsService,
                    publicationService)
                .GetOverviewAsync(
                    project.Path);

        var entry =
            Assert.Single(
                overview);

        Assert.Equal(
            CurseForgePublicationState.LocalArtifactChanged,
            entry.PublicationState);
        Assert.True(
            entry.IsReadyForUpload);
        Assert.Equal(
            20402,
            entry.FileId);
        Assert.Equal(
            1712846,
            entry.ProjectId);
    }

    private static CurseForgeReleaseOverviewService
        CreateService(
            ReleaseArtifactManifestService artifactService,
            CurseForgeReleaseSettingsService settingsService) =>
        new(
            new ReleaseHistoryService(),
            artifactService,
            settingsService,
            new CurseForgePublicationRecordService(
                artifactService));

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
                    ProjectLayout.ManifestFileName),
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
                        ProjectLayout.ReleaseDirectoryName))
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
