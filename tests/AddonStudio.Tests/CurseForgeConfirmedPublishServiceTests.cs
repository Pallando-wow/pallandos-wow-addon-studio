using System.Net;
using System.Text;
using AddonStudio.Application.Settings;
using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class CurseForgeConfirmedPublishServiceTests
{
    [Fact]
    public async Task ConfirmedPublish_RechecksPreflightAndUploads()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0");

        var getCount =
            0;
        var uploadCount =
            0;

        var services =
            await CreateServicesAsync(
                project,
                "1.2.0",
                request =>
                {
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

                    uploadCount++;

                    return Json(
                        """
                        {
                          "id": 20402
                        }
                        """);
                });

        await services.Settings.WriteAsync(
            project.Path,
            "1.2.0",
            [12919]);

        var artifact =
            await services.Artifacts.ReadAsync(
                project.Path,
                "1.2.0");

        Assert.NotNull(
            artifact);

        var result =
            await services.Confirmed.ExecuteConfirmedAsync(
                project.Path,
                new CurseForgeConfirmedPublishRequest(
                    "1.2.0",
                    1712846,
                    artifact.Sha256));

        Assert.Equal(
            1,
            getCount);
        Assert.Equal(
            1,
            uploadCount);
        Assert.Equal(
            20402,
            result.Execution.Upload.FileId);
        Assert.True(
            result.Preflight.IsReadyForExplicitUpload);

        var publication =
            await services.Publication.ReadAsync(
                project.Path,
                "1.2.0");

        Assert.NotNull(
            publication);
        Assert.Equal(
            20402,
            publication.FileId);
    }

    [Fact]
    public async Task ConfirmedPublish_BlocksStaleArtifactConfirmationBeforeUpload()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0");

        var uploadCount =
            0;

        var services =
            await CreateServicesAsync(
                project,
                "1.2.0",
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

                    uploadCount++;

                    return Json(
                        """
                        {
                          "id": 20402
                        }
                        """);
                });

        await services.Settings.WriteAsync(
            project.Path,
            "1.2.0",
            [12919]);

        var exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () =>
                    services.Confirmed.ExecuteConfirmedAsync(
                        project.Path,
                        new CurseForgeConfirmedPublishRequest(
                            "1.2.0",
                            1712846,
                            new string(
                                '0',
                                64))));

        Assert.Contains(
            "changed",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            0,
            uploadCount);
    }

    [Fact]
    public async Task ConfirmedPublish_BlocksWhenRemoteGameVersionDisappears()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0");

        var uploadCount =
            0;

        var services =
            await CreateServicesAsync(
                project,
                "1.2.0",
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

                    uploadCount++;

                    return Json(
                        """
                        {
                          "id": 20402
                        }
                        """);
                });

        await services.Settings.WriteAsync(
            project.Path,
            "1.2.0",
            [13001]);

        var artifact =
            await services.Artifacts.ReadAsync(
                project.Path,
                "1.2.0");

        Assert.NotNull(
            artifact);

        var exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () =>
                    services.Confirmed.ExecuteConfirmedAsync(
                        project.Path,
                        new CurseForgeConfirmedPublishRequest(
                            "1.2.0",
                            1712846,
                            artifact.Sha256)));

        Assert.Contains(
            "13001",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            0,
            uploadCount);
    }

    [Fact]
    public async Task ConfirmedPublish_RequiresExplicitRepublishFlag()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0");

        var uploadCount =
            0;

        var services =
            await CreateServicesAsync(
                project,
                "1.2.0",
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

                    uploadCount++;

                    return Json(
                        $$"""
                        {
                          "id": {{20402 + uploadCount}}
                        }
                        """);
                });

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
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () =>
                    services.Confirmed.ExecuteConfirmedAsync(
                        project.Path,
                        new CurseForgeConfirmedPublishRequest(
                            "1.2.0",
                            1712846,
                            plan.ArtifactSha256)));

        Assert.Contains(
            "already published",
            blocked.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            0,
            uploadCount);

        var republished =
            await services.Confirmed.ExecuteConfirmedAsync(
                project.Path,
                new CurseForgeConfirmedPublishRequest(
                    "1.2.0",
                    1712846,
                    plan.ArtifactSha256,
                    AllowRepublish: true));

        Assert.Equal(
            20403,
            republished.Execution.Upload.FileId);
        Assert.Equal(
            1,
            uploadCount);
    }

    private static async Task<ServiceSet>
        CreateServicesAsync(
            TemporaryProject project,
            string version,
            Func<HttpRequestMessage, HttpResponseMessage>
                responseFactory)
    {
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

        var tokenService =
            new CurseForgeUploadTokenService(
                secretStore);

        tokenService.Save(
            "upload-token");

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

        var confirmedService =
            new CurseForgeConfirmedPublishService(
                preflightService,
                tokenService,
                executionService);

        return new ServiceSet(
            artifactService,
            settingsService,
            publicationService,
            planService,
            confirmedService);
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

    private sealed record ServiceSet(
        ReleaseArtifactManifestService Artifacts,
        CurseForgeReleaseSettingsService Settings,
        CurseForgePublicationRecordService Publication,
        CurseForgeUploadPlanService Plan,
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
