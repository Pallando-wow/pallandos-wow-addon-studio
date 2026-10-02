using AddonStudio.Application.Settings;
using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class CurseForgePublishPreparationServiceTests
{
    [Fact]
    public async Task Preparation_UsesStoredSettingsAndCanonicalChangelog()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0",
            "# 1.2.0\n\n- Fixed release preparation.");

        var services =
            await CreateServicesAsync(
                project,
                "1.2.0",
                uploadToken: "upload-token");

        await services.Settings.WriteAsync(
            project.Path,
            "1.2.0",
            [13001, 12919],
            CurseForgeFileReleaseType.Beta,
            isMarkedForManualRelease: true,
            displayName: "ForeverBag 1.2.0 Beta");

        var preparation =
            await services.Preparation.PrepareAsync(
                project.Path,
                "1.2.0",
                1712846);

        Assert.True(
            preparation.IsReadyForUpload);
        Assert.True(
            preparation.HasUploadToken);
        Assert.False(
            preparation.RequiresRepublishConfirmation);
        Assert.Empty(
            preparation.Issues);

        var plan =
            Assert.IsType<CurseForgeUploadPlan>(
                preparation.UploadPlan);

        Assert.Equal(
            "# 1.2.0\n\n- Fixed release preparation.",
            plan.Changelog);
        Assert.Equal(
            [12919, 13001],
            plan.GameVersionIds);
        Assert.Equal(
            CurseForgeFileReleaseType.Beta,
            plan.ReleaseType);
        Assert.True(
            plan.IsMarkedForManualRelease);
        Assert.Equal(
            "ForeverBag 1.2.0 Beta",
            plan.DisplayName);
    }

    [Fact]
    public async Task Preparation_ReportsMissingUploadTokenWithoutLosingPlan()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0",
            "Changes");

        var services =
            await CreateServicesAsync(
                project,
                "1.2.0",
                uploadToken: null);

        await services.Settings.WriteAsync(
            project.Path,
            "1.2.0",
            [12919]);

        var preparation =
            await services.Preparation.PrepareAsync(
                project.Path,
                "1.2.0",
                1712846);

        Assert.False(
            preparation.IsReadyForUpload);
        Assert.False(
            preparation.HasUploadToken);
        Assert.NotNull(
            preparation.UploadPlan);
        Assert.Contains(
            preparation.Issues,
            issue =>
                issue.Contains(
                    "upload token",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preparation_RequiresExplicitRepublishForSameArtifact()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0",
            "Changes");

        var services =
            await CreateServicesAsync(
                project,
                "1.2.0",
                uploadToken: "upload-token");

        await services.Settings.WriteAsync(
            project.Path,
            "1.2.0",
            [12919]);

        var settings =
            await services.Settings.ReadAsync(
                project.Path,
                "1.2.0");

        Assert.NotNull(
            settings);

        var plan =
            await services.Plan.CreateAsync(
                project.Path,
                "1.2.0",
                1712846,
                settings,
                "Changes");

        await services.Publication.WriteAsync(
            project.Path,
            "1.2.0",
            plan,
            new CurseForgeUploadResult(
                20402),
            DateTimeOffset.UtcNow);

        var blocked =
            await services.Preparation.PrepareAsync(
                project.Path,
                "1.2.0",
                1712846);

        Assert.False(
            blocked.IsReadyForUpload);
        Assert.True(
            blocked.RequiresRepublishConfirmation);
        Assert.Contains(
            blocked.Issues,
            issue =>
                issue.Contains(
                    "#20402",
                    StringComparison.Ordinal));

        var confirmed =
            await services.Preparation.PrepareAsync(
                project.Path,
                "1.2.0",
                1712846,
                allowRepublish: true);

        Assert.True(
            confirmed.IsReadyForUpload);
        Assert.False(
            confirmed.RequiresRepublishConfirmation);
        Assert.Empty(
            confirmed.Issues);
    }

    private static async Task<ServiceSet>
        CreateServicesAsync(
            TemporaryProject project,
            string version,
            string? uploadToken)
    {
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
                version,
                CurseForgeProjectVerified: true),
            buildPackage: true);

        var settingsService =
            new CurseForgeReleaseSettingsService(
                artifactService);

        var publicationService =
            new CurseForgePublicationRecordService(
                artifactService);

        var overviewService =
            new CurseForgeReleaseOverviewService(
                new ReleaseHistoryService(),
                artifactService,
                settingsService,
                publicationService);

        var planService =
            new CurseForgeUploadPlanService(
                artifactService);

        var secretStore =
            new InMemorySecretStore();

        var uploadTokenService =
            new CurseForgeUploadTokenService(
                secretStore);

        if (!string.IsNullOrWhiteSpace(
                uploadToken))
        {
            uploadTokenService.Save(
                uploadToken);
        }

        var preparationService =
            new CurseForgePublishPreparationService(
                overviewService,
                settingsService,
                planService,
                uploadTokenService);

        return new ServiceSet(
            settingsService,
            publicationService,
            planService,
            preparationService);
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

    private sealed record ServiceSet(
        CurseForgeReleaseSettingsService Settings,
        CurseForgePublicationRecordService Publication,
        CurseForgeUploadPlanService Plan,
        CurseForgePublishPreparationService Preparation);

    private sealed class InMemorySecretStore :
        ILocalSecretStore
    {
        private readonly Dictionary<string, string>
            values =
                new(
                    StringComparer.Ordinal);

        public string? Load(
            string key) =>
            values.TryGetValue(
                key,
                out var value)
                ? value
                : null;

        public void Save(
            string key,
            string value) =>
            values[key] =
                value;

        public void Delete(
            string key) =>
            values.Remove(
                key);

        public void DeleteAll() =>
            values.Clear();
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

        public void CreateRelease(
            string version,
            string changelog)
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
                changelog);

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
