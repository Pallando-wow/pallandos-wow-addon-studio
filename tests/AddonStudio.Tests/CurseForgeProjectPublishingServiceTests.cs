using System.Net;
using System.Text;
using AddonStudio.Application.Settings;
using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class CurseForgeProjectPublishingServiceTests
{
    [Fact]
    public async Task Prepare_UsesProjectIdFromManifest()
    {
        using var project =
            new TemporaryProject();

        const string version =
            "1.2.0";

        var services =
            await CreateServicesAsync(
                project,
                version,
                request =>
                {
                    Assert.Equal(
                        HttpMethod.Get,
                        request.Method);

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
                });

        var result =
            await services.ProjectPublishing.PrepareAsync(
                project.Path,
                CreateManifest(
                    "1712846"),
                version);

        Assert.True(
            result.IsReadyForExplicitUpload);
        Assert.Equal(
            1712846,
            result.LocalPreparation.UploadPlan?.ProjectId);
    }

    [Fact]
    public async Task PublishConfirmed_UsesManifestProjectIdForUpload()
    {
        using var project =
            new TemporaryProject();

        const string version =
            "1.2.0";

        var postCount =
            0;

        var services =
            await CreateServicesAsync(
                project,
                version,
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

        var artifact =
            await services.Artifacts.ReadAsync(
                project.Path,
                version);

        Assert.NotNull(
            artifact);

        var result =
            await services.ProjectPublishing.PublishConfirmedAsync(
                project.Path,
                CreateManifest(
                    "1712846"),
                version,
                artifact.Sha256);

        Assert.Equal(
            20402,
            result.Execution.Upload.FileId);
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
    }

    [Fact]
    public async Task Prepare_RejectsMissingProjectIdBeforeNetwork()
    {
        using var project =
            new TemporaryProject();

        const string version =
            "1.2.0";

        var requestSent =
            false;

        var services =
            await CreateServicesAsync(
                project,
                version,
                request =>
                {
                    requestSent =
                        true;

                    return Json(
                        "[]");
                });

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () =>
                    services.ProjectPublishing.PrepareAsync(
                        project.Path,
                        CreateManifest(
                            null),
                        version));

        Assert.Contains(
            "project id",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            requestSent);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public async Task Prepare_RejectsInvalidProjectIdBeforeNetwork(
        string projectId)
    {
        using var project =
            new TemporaryProject();

        const string version =
            "1.2.0";

        var requestSent =
            false;

        var services =
            await CreateServicesAsync(
                project,
                version,
                request =>
                {
                    requestSent =
                        true;

                    return Json(
                        "[]");
                });

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () =>
                    services.ProjectPublishing.PrepareAsync(
                        project.Path,
                        CreateManifest(
                            projectId),
                        version));

        Assert.Contains(
            "invalid",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            requestSent);
    }

    private static async Task<ServiceSet>
        CreateServicesAsync(
            TemporaryProject project,
            string version,
            Func<HttpRequestMessage, HttpResponseMessage>
                responseFactory)
    {
        project.CreateReleaseSource(
            version);

        var artifactService =
            new ReleaseArtifactManifestService();

        await new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                artifactService)
            .PrepareAsync(
                new ReleasePreparationRequest(
                    project.Path,
                    CreateManifest(
                        "1712846"),
                    version,
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        var settingsService =
            new CurseForgeReleaseSettingsService(
                artifactService);

        await settingsService.WriteAsync(
            project.Path,
            version,
            [12919]);

        var publicationService =
            new CurseForgePublicationRecordService(
                artifactService);

        var overviewService =
            new CurseForgeReleaseOverviewService(
                new ReleaseHistoryService(),
                artifactService,
                settingsService,
                publicationService);

        var secretStore =
            new InMemorySecretStore();

        var tokenService =
            new CurseForgeUploadTokenService(
                secretStore);

        tokenService.Save(
            "upload-token");

        var preparationService =
            new CurseForgePublishPreparationService(
                overviewService,
                settingsService,
                new CurseForgeUploadPlanService(
                    artifactService),
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

        var confirmedService =
            new CurseForgeConfirmedPublishService(
                preflightService,
                tokenService,
                new CurseForgeUploadExecutionService(
                    apiClient,
                    publicationService));

        return new ServiceSet(
            artifactService,
            publicationService,
            new CurseForgeProjectPublishingService(
                preflightService,
                confirmedService));
    }

    private static ProjectManifest CreateManifest(
        string? projectId) =>
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
                        projectId,
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

    private sealed record ServiceSet(
        ReleaseArtifactManifestService Artifacts,
        CurseForgePublicationRecordService Publication,
        CurseForgeProjectPublishingService ProjectPublishing);

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
