using System.Net;
using System.Text;
using AddonStudio.Application.Settings;
using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class CurseForgePublishPreflightServiceTests
{
    [Fact]
    public async Task Preflight_ValidatesSelectedGameVersionsAgainstUploadApi()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0");

        var requestCount =
            0;

        var services =
            await CreateServicesAsync(
                project,
                "1.2.0",
                "upload-token",
                request =>
                {
                    requestCount++;

                    Assert.Equal(
                        HttpMethod.Get,
                        request.Method);
                    Assert.Equal(
                        "/api/game/versions",
                        request.RequestUri?.AbsolutePath);

                    Assert.True(
                        request.Headers.TryGetValues(
                            "X-Api-Token",
                            out var values));
                    Assert.Equal(
                        "upload-token",
                        Assert.Single(
                            values));

                    return Json(
                        """
                        [
                          {
                            "id": 12919,
                            "gameVersionTypeID": 67408,
                            "name": "1.15.7",
                            "slug": "1-15-7"
                          },
                          {
                            "id": 13001,
                            "gameVersionTypeID": 67408,
                            "name": "1.15.8",
                            "slug": "1-15-8"
                          }
                        ]
                        """);
                });

        await services.Settings.WriteAsync(
            project.Path,
            "1.2.0",
            [13001, 12919],
            CurseForgeFileReleaseType.Release);

        var result =
            await services.Preflight.CheckAsync(
                project.Path,
                "1.2.0",
                1712846);

        Assert.True(
            result.IsReadyForExplicitUpload);
        Assert.True(
            result.UploadApiChecked);
        Assert.True(
            result.UploadApiReachable);
        Assert.Empty(
            result.MissingGameVersionIds);
        Assert.Empty(
            result.Issues);
        Assert.Equal(
            2,
            result.SelectedGameVersions.Count);
        Assert.Contains(
            result.SelectedGameVersions,
            version =>
                version.Id == 12919 &&
                version.Name == "1.15.7");
        Assert.Contains(
            result.SelectedGameVersions,
            version =>
                version.Id == 13001 &&
                version.Name == "1.15.8");
        Assert.Equal(
            1,
            requestCount);
    }

    [Fact]
    public async Task Preflight_BlocksGameVersionIdsNoLongerAvailable()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0");

        var services =
            await CreateServicesAsync(
                project,
                "1.2.0",
                "upload-token",
                _ =>
                    Json(
                        """
                        [
                          {
                            "id": 12919,
                            "gameVersionTypeID": 67408,
                            "name": "1.15.7",
                            "slug": "1-15-7"
                          }
                        ]
                        """));

        await services.Settings.WriteAsync(
            project.Path,
            "1.2.0",
            [12919, 13001]);

        var result =
            await services.Preflight.CheckAsync(
                project.Path,
                "1.2.0",
                1712846);

        Assert.False(
            result.IsReadyForExplicitUpload);
        Assert.True(
            result.UploadApiChecked);
        Assert.True(
            result.UploadApiReachable);
        Assert.Equal(
            [13001],
            result.MissingGameVersionIds);
        Assert.Contains(
            result.Issues,
            issue =>
                issue.Contains(
                    "13001",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task Preflight_DoesNotCallUploadApiWithoutToken()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0");

        var requestSent =
            false;

        var services =
            await CreateServicesAsync(
                project,
                "1.2.0",
                uploadToken: null,
                _ =>
                {
                    requestSent =
                        true;

                    return Json(
                        "[]");
                });

        await services.Settings.WriteAsync(
            project.Path,
            "1.2.0",
            [12919]);

        var result =
            await services.Preflight.CheckAsync(
                project.Path,
                "1.2.0",
                1712846);

        Assert.False(
            result.IsReadyForExplicitUpload);
        Assert.False(
            result.UploadApiChecked);
        Assert.False(
            result.UploadApiReachable);
        Assert.False(
            requestSent);
        Assert.Contains(
            result.Issues,
            issue =>
                issue.Contains(
                    "upload token",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preflight_ReportsUploadApiAuthenticationFailure()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.2.0");

        var services =
            await CreateServicesAsync(
                project,
                "1.2.0",
                "invalid-token",
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.Unauthorized)
                    {
                        Content =
                            new StringContent(
                                "invalid token",
                                Encoding.UTF8,
                                "text/plain")
                    });

        await services.Settings.WriteAsync(
            project.Path,
            "1.2.0",
            [12919]);

        var result =
            await services.Preflight.CheckAsync(
                project.Path,
                "1.2.0",
                1712846);

        Assert.False(
            result.IsReadyForExplicitUpload);
        Assert.True(
            result.UploadApiChecked);
        Assert.False(
            result.UploadApiReachable);
        Assert.Contains(
            result.Issues,
            issue =>
                issue.Contains(
                    "401",
                    StringComparison.Ordinal) &&
                issue.Contains(
                    "invalid token",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<ServiceSet>
        CreateServicesAsync(
            TemporaryProject project,
            string version,
            string? uploadToken,
            Func<HttpRequestMessage, HttpResponseMessage>
                responseFactory)
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

        var uploadApiClient =
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
                uploadApiClient,
                uploadTokenService);

        return new ServiceSet(
            settingsService,
            preflightService);
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
        CurseForgeReleaseSettingsService Settings,
        CurseForgePublishPreflightService Preflight);

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
