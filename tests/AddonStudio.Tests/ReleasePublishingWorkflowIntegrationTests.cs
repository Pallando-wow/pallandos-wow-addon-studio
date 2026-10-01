using System.Net;
using System.Text;
using AddonStudio.Application.Settings;
using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class ReleasePublishingWorkflowIntegrationTests
{
    [Fact]
    public async Task FullWorkflow_BuildsPreflightsPublishesAndReportsPublished()
    {
        using var project =
            new TemporaryProject();

        const string version =
            "1.2.0";

        project.CreateReleaseSource(
            version);

        var manifest =
            CreateManifest();

        var artifactService =
            new ReleaseArtifactManifestService();

        var releaseWorkflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                artifactService);

        var release =
            await releaseWorkflow.PrepareAsync(
                new ReleasePreparationRequest(
                    project.Path,
                    manifest,
                    version,
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        var package =
            Assert.IsType<ReleasePackageResult>(
                release.Package);

        Assert.True(
            release.Preparation.IsReady);
        Assert.True(
            release.ArtifactManifestCreated);

        var getCount =
            0;
        var postCount =
            0;

        var services =
            CreatePublishingServices(
                artifactService,
                request =>
                {
                    Assert.Equal(
                        "upload-token",
                        request.Headers
                            .GetValues(
                                "X-Api-Token")
                            .Single());

                    if (request.Method ==
                        HttpMethod.Get)
                    {
                        getCount++;

                        return Json(
                            """
                            [
                              {
                                "id": 12919,
                                "gameVersionTypeID": 67408,
                                "name": "1.15.7",
                                "slug": "1-15-7"
                              }
                            ]
                            """);
                    }

                    Assert.Equal(
                        HttpMethod.Post,
                        request.Method);
                    Assert.Equal(
                        "/api/projects/1712846/upload-file",
                        request.RequestUri?.AbsolutePath);

                    postCount++;

                    return Json(
                        """
                        {
                          "id": 20402
                        }
                        """);
                });

        await services.Settings.WriteAsync(
            project.Path,
            version,
            [12919],
            CurseForgeFileReleaseType.Release,
            displayName:
                "ForeverBag 1.2.0");

        services.Token.Save(
            "upload-token");

        var preflight =
            await services.Preflight.CheckAsync(
                project.Path,
                version,
                1712846);

        Assert.True(
            preflight.IsReadyForExplicitUpload);
        Assert.Single(
            preflight.SelectedGameVersions);
        Assert.Equal(
            12919,
            preflight.SelectedGameVersions[0].Id);
        Assert.Equal(
            1,
            getCount);
        Assert.Equal(
            0,
            postCount);

        var confirmed =
            await services.Confirmed.ExecuteConfirmedAsync(
                project.Path,
                new CurseForgeConfirmedPublishRequest(
                    version,
                    1712846,
                    package.Sha256));

        Assert.Equal(
            20402,
            confirmed.Execution.Upload.FileId);
        Assert.Equal(
            2,
            getCount);
        Assert.Equal(
            1,
            postCount);

        var publication =
            await services.Publication.ReadAsync(
                project.Path,
                version);

        Assert.NotNull(
            publication);
        Assert.Equal(
            1712846,
            publication.ProjectId);
        Assert.Equal(
            20402,
            publication.FileId);
        Assert.Equal(
            package.Sha256,
            publication.ArtifactSha256);

        var overview =
            await services.Overview.GetOverviewAsync(
                project.Path);

        var entry =
            Assert.Single(
                overview);

        Assert.Equal(
            version,
            entry.Version);
        Assert.Equal(
            CurseForgePublicationState.Published,
            entry.PublicationState);
        Assert.Equal(
            20402,
            entry.FileId);

        var rawPublication =
            await File.ReadAllTextAsync(
                services.Publication.GetPath(
                    project.Path,
                    version));

        Assert.DoesNotContain(
            "upload-token",
            rawPublication,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullWorkflow_BlocksPublishWhenPackageChangesAfterPreflight()
    {
        using var project =
            new TemporaryProject();

        const string version =
            "1.2.0";

        project.CreateReleaseSource(
            version);

        var artifactService =
            new ReleaseArtifactManifestService();

        var release =
            await new ReleaseWorkflowService(
                    new ReleasePreparationService(),
                    new ReleasePackageBuilder(),
                    artifactService)
                .PrepareAsync(
                    new ReleasePreparationRequest(
                        project.Path,
                        CreateManifest(),
                        version,
                        CurseForgeProjectVerified: true),
                    buildPackage: true);

        var package =
            Assert.IsType<ReleasePackageResult>(
                release.Package);

        var postCount =
            0;

        var services =
            CreatePublishingServices(
                artifactService,
                request =>
                {
                    if (request.Method ==
                        HttpMethod.Get)
                    {
                        return Json(
                            """
                            [
                              {
                                "id": 12919,
                                "gameVersionTypeID": 67408,
                                "name": "1.15.7",
                                "slug": "1-15-7"
                              }
                            ]
                            """);
                    }

                    postCount++;

                    return Json(
                        """
                        {
                          "id": 20402
                        }
                        """);
                });

        await services.Settings.WriteAsync(
            project.Path,
            version,
            [12919]);

        services.Token.Save(
            "upload-token");

        var preflight =
            await services.Preflight.CheckAsync(
                project.Path,
                version,
                1712846);

        Assert.True(
            preflight.IsReadyForExplicitUpload);

        await using (
            var stream =
                new FileStream(
                    package.PackagePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    useAsync: true))
        {
            await stream.WriteAsync(
                new byte[]
                {
                    1,
                    2,
                    3,
                    4
                });

            await stream.FlushAsync();
        }

        var exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () =>
                    services.Confirmed.ExecuteConfirmedAsync(
                        project.Path,
                        new CurseForgeConfirmedPublishRequest(
                            version,
                            1712846,
                            package.Sha256)));

        Assert.Contains(
            "preflight",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            0,
            postCount);

        Assert.False(
            File.Exists(
                services.Publication.GetPath(
                    project.Path,
                    version)));

        var overview =
            await services.Overview.GetOverviewAsync(
                project.Path);

        var entry =
            Assert.Single(
                overview);

        Assert.Equal(
            CurseForgePublicationState.NotPublished,
            entry.PublicationState);
        Assert.Contains(
            entry.Issues,
            issue =>
                issue.Contains(
                    "Artifact:",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static PublishingServices
        CreatePublishingServices(
            ReleaseArtifactManifestService artifactService,
            Func<HttpRequestMessage, HttpResponseMessage>
                responseFactory)
    {
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

        var tokenService =
            new CurseForgeUploadTokenService(
                secretStore);

        var preparationService =
            new CurseForgePublishPreparationService(
                overviewService,
                settingsService,
                planService,
                tokenService);

        var apiClient =
            new CurseForgeUploadApiClient(
                new HttpClient(
                    new StubHandler(
                        responseFactory))
                {
                    BaseAddress =
                        new Uri(
                            "https://wow.curseforge.com/")
                });

        var preflightService =
            new CurseForgePublishPreflightService(
                preparationService,
                apiClient,
                tokenService);

        var executionService =
            new CurseForgeUploadExecutionService(
                apiClient,
                publicationService);

        return new PublishingServices(
            settingsService,
            publicationService,
            overviewService,
            tokenService,
            preflightService,
            new CurseForgeConfirmedPublishService(
                preflightService,
                tokenService,
                executionService));
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

    private static HttpResponseMessage Json(
        string json) =>
        new(HttpStatusCode.OK)
        {
            Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
        };

    private sealed record PublishingServices(
        CurseForgeReleaseSettingsService Settings,
        CurseForgePublicationRecordService Publication,
        CurseForgeReleaseOverviewService Overview,
        CurseForgeUploadTokenService Token,
        CurseForgePublishPreflightService Preflight,
        CurseForgeConfirmedPublishService Confirmed);

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage>
            responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                responseFactory(
                    request));
    }

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

        public void CreateReleaseSource(
            string version)
        {
            var addonDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        Path,
                        ProjectLayout.RuntimeDirectoryName,
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
                        ProjectLayout.MediaDirectoryName,
                        ProjectLayout.LogoDirectoryName))
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
