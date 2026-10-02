using System.Net;
using System.Security.Cryptography;
using System.Text;
using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class CurseForgePublicationTests
{
    [Fact]
    public async Task ExecuteAsync_UploadsAndStoresPublicationRecord()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.1.0");

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
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        var package =
            Assert.IsType<ReleasePackageResult>(
                release.Package);

        var plan =
            await new CurseForgeUploadPlanService(
                    artifactService)
                .CreateAsync(
                    project.Path,
                    "1.1.0",
                    1712846,
                    [12919],
                    "Changes",
                    CurseForgeFileReleaseType.Release);

        var handler =
            new StubHandler(
                request =>
                {
                    Assert.Equal(
                        "/api/projects/1712846/upload-file",
                        request.RequestUri?.AbsolutePath);

                    return Json(
                        """
                        {
                          "id": 20402
                        }
                        """);
                });

        var apiClient =
            new CurseForgeUploadApiClient(
                new HttpClient(handler)
                {
                    BaseAddress =
                        new Uri(
                            "https://wow.curseforge.com/")
                });

        var recordService =
            new CurseForgePublicationRecordService(
                artifactService);

        var uploadedAt =
            new DateTimeOffset(
                2026,
                10,
                1,
                7,
                30,
                0,
                TimeSpan.Zero);

        var execution =
            new CurseForgeUploadExecutionService(
                apiClient,
                recordService,
                new FixedTimeProvider(
                    uploadedAt));

        var result =
            await execution.ExecuteAsync(
                "upload-token",
                project.Path,
                "1.1.0",
                plan);

        Assert.Equal(
            20402,
            result.Upload.FileId);
        Assert.True(
            File.Exists(
                result.PublicationRecordPath));

        var record =
            await recordService.ReadAsync(
                project.Path,
                "1.1.0");

        Assert.NotNull(
            record);
        Assert.Equal(
            1712846,
            record.ProjectId);
        Assert.Equal(
            20402,
            record.FileId);
        Assert.Equal(
            package.FileName,
            record.PackageFileName);
        Assert.Equal(
            package.Sha256,
            record.ArtifactSha256);
        Assert.Equal(
            [12919],
            record.GameVersionIds);
        Assert.Equal(
            "release",
            record.ReleaseType);
        Assert.False(
            record.IsMarkedForManualRelease);
        Assert.Equal(
            uploadedAt,
            record.UploadedAtUtc);

        var rawRecord =
            await File.ReadAllTextAsync(
                result.PublicationRecordPath);

        Assert.DoesNotContain(
            "upload-token",
            rawRecord,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_BlocksDuplicateArtifactBeforeHttpRequest()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.1.0");

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
                "1.1.0",
                CurseForgeProjectVerified: true),
            buildPackage: true);

        var plan =
            await new CurseForgeUploadPlanService(
                    artifactService)
                .CreateAsync(
                    project.Path,
                    "1.1.0",
                    1712846,
                    [12919],
                    "Changes");

        var recordService =
            new CurseForgePublicationRecordService(
                artifactService);

        await recordService.WriteAsync(
            project.Path,
            "1.1.0",
            plan,
            new CurseForgeUploadResult(
                20402),
            DateTimeOffset.UtcNow);

        var requestSent =
            false;

        var apiClient =
            new CurseForgeUploadApiClient(
                new HttpClient(
                    new StubHandler(
                        _ =>
                        {
                            requestSent =
                                true;

                            return Json(
                                """
                                {
                                  "id": 20403
                                }
                                """);
                        }))
                {
                    BaseAddress =
                        new Uri(
                            "https://wow.curseforge.com/")
                });

        var execution =
            new CurseForgeUploadExecutionService(
                apiClient,
                recordService);

        var exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () => execution.ExecuteAsync(
                    "upload-token",
                    project.Path,
                    "1.1.0",
                    plan));

        Assert.Contains(
            "already published",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "#20402",
            exception.Message,
            StringComparison.Ordinal);
        Assert.False(
            requestSent);
    }

    [Fact]
    public async Task ExecuteAsync_AllowsExplicitRepublish()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.1.0");

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
                "1.1.0",
                CurseForgeProjectVerified: true),
            buildPackage: true);

        var plan =
            await new CurseForgeUploadPlanService(
                    artifactService)
                .CreateAsync(
                    project.Path,
                    "1.1.0",
                    1712846,
                    [12919],
                    "Changes");

        var recordService =
            new CurseForgePublicationRecordService(
                artifactService);

        await recordService.WriteAsync(
            project.Path,
            "1.1.0",
            plan,
            new CurseForgeUploadResult(
                20402),
            DateTimeOffset.UtcNow);

        var apiClient =
            new CurseForgeUploadApiClient(
                new HttpClient(
                    new StubHandler(
                        _ =>
                            Json(
                                """
                                {
                                  "id": 20403
                                }
                                """)))
                {
                    BaseAddress =
                        new Uri(
                            "https://wow.curseforge.com/")
                });

        var execution =
            new CurseForgeUploadExecutionService(
                apiClient,
                recordService);

        var result =
            await execution.ExecuteAsync(
                "upload-token",
                project.Path,
                "1.1.0",
                plan,
                allowRepublish: true);

        Assert.Equal(
            20403,
            result.Upload.FileId);

        var record =
            await recordService.ReadAsync(
                project.Path,
                "1.1.0");

        Assert.NotNull(
            record);
        Assert.Equal(
            20403,
            record.FileId);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotStoreRecordWhenUploadFails()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.1.0");

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
                "1.1.0",
                CurseForgeProjectVerified: true),
            buildPackage: true);

        var plan =
            await new CurseForgeUploadPlanService(
                    artifactService)
                .CreateAsync(
                    project.Path,
                    "1.1.0",
                    1712846,
                    [12919],
                    "Changes");

        var apiClient =
            new CurseForgeUploadApiClient(
                new HttpClient(
                    new StubHandler(
                        _ =>
                            new HttpResponseMessage(
                                HttpStatusCode.Forbidden)
                            {
                                Content =
                                    new StringContent(
                                        "forbidden",
                                        Encoding.UTF8,
                                        "text/plain")
                            }))
                {
                    BaseAddress =
                        new Uri(
                            "https://wow.curseforge.com/")
                });

        var recordService =
            new CurseForgePublicationRecordService(
                artifactService);

        var execution =
            new CurseForgeUploadExecutionService(
                apiClient,
                recordService);

        await Assert.ThrowsAsync<
            HttpRequestException>(
                () => execution.ExecuteAsync(
                    "bad-token",
                    project.Path,
                    "1.1.0",
                    plan));

        Assert.False(
            File.Exists(
                recordService.GetPath(
                    project.Path,
                    "1.1.0")));
    }

    [Fact]
    public async Task PublicationHistory_ReportsPublishedRelease()
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

        var plan =
            await new CurseForgeUploadPlanService(
                    artifactService)
                .CreateAsync(
                    project.Path,
                    "1.1.0",
                    1712846,
                    [12919],
                    "Changes");

        var recordService =
            new CurseForgePublicationRecordService(
                artifactService);

        var uploadedAt =
            new DateTimeOffset(
                2026,
                10,
                1,
                8,
                0,
                0,
                TimeSpan.Zero);

        await recordService.WriteAsync(
            project.Path,
            "1.1.0",
            plan,
            new CurseForgeUploadResult(
                20402),
            uploadedAt);

        var history =
            await new CurseForgePublicationHistoryService(
                    new ReleaseHistoryService(),
                    artifactService,
                    recordService)
                .GetHistoryAsync(
                    project.Path);

        var entry =
            Assert.Single(
                history);

        Assert.Equal(
            "1.1.0",
            entry.Version);
        Assert.Equal(
            CurseForgePublicationState.Published,
            entry.State);
        Assert.Equal(
            1712846,
            entry.ProjectId);
        Assert.Equal(
            20402,
            entry.FileId);
        Assert.Equal(
            [12919],
            entry.GameVersionIds);
        Assert.Equal(
            "release",
            entry.ReleaseType);
        Assert.Equal(
            uploadedAt,
            entry.UploadedAtUtc);
    }

    [Fact]
    public async Task PublicationHistory_ReportsChangedLocalArtifact()
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

        var firstRelease =
            await workflow.PrepareAsync(
                new ReleasePreparationRequest(
                    project.Path,
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        var firstPackage =
            Assert.IsType<ReleasePackageResult>(
                firstRelease.Package);

        var plan =
            await new CurseForgeUploadPlanService(
                    artifactService)
                .CreateAsync(
                    project.Path,
                    "1.1.0",
                    1712846,
                    [12919],
                    "Changes");

        var recordService =
            new CurseForgePublicationRecordService(
                artifactService);

        await recordService.WriteAsync(
            project.Path,
            "1.1.0",
            plan,
            new CurseForgeUploadResult(
                20402),
            DateTimeOffset.UtcNow);

        var addonFile =
            System.IO.Path.Combine(
                project.Path,
                "AddOns",
                "ForeverBag",
                "ForeverBag.lua");

        await File.AppendAllTextAsync(
            addonFile,
            Environment.NewLine +
            "-- changed after publication");

        var secondRelease =
            await workflow.PrepareAsync(
                new ReleasePreparationRequest(
                    project.Path,
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        var secondPackage =
            Assert.IsType<ReleasePackageResult>(
                secondRelease.Package);

        Assert.NotEqual(
            firstPackage.Sha256,
            secondPackage.Sha256);

        var history =
            await new CurseForgePublicationHistoryService(
                    new ReleaseHistoryService(),
                    artifactService,
                    recordService)
                .GetHistoryAsync(
                    project.Path);

        var entry =
            Assert.Single(
                history);

        Assert.Equal(
            CurseForgePublicationState.LocalArtifactChanged,
            entry.State);
        Assert.Equal(
            firstPackage.Sha256,
            entry.PublishedArtifactSha256);
        Assert.Equal(
            20402,
            entry.FileId);
    }

    [Fact]
    public async Task PublicationHistory_ReportsUnpublishedRelease()
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

        var recordService =
            new CurseForgePublicationRecordService(
                artifactService);

        var history =
            await new CurseForgePublicationHistoryService(
                    new ReleaseHistoryService(),
                    artifactService,
                    recordService)
                .GetHistoryAsync(
                    project.Path);

        var entry =
            Assert.Single(
                history);

        Assert.Equal(
            CurseForgePublicationState.NotPublished,
            entry.State);
        Assert.Null(
            entry.FileId);
        Assert.Null(
            entry.UploadedAtUtc);
    }

    [Fact]
    public async Task PublicationRecord_RejectsPlanForDifferentArtifact()
    {
        using var project =
            new TemporaryProject();

        project.CreateRelease(
            "1.1.0");

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
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        var package =
            Assert.IsType<ReleasePackageResult>(
                release.Package);

        var plan =
            new CurseForgeUploadPlan(
                1712846,
                package.PackagePath,
                package.FileName,
                "ForeverBag",
                "Changes",
                CurseForgeChangelogMarkupType.Markdown,
                [12919],
                CurseForgeFileReleaseType.Release,
                package.SizeBytes,
                false,
                Convert.ToHexString(
                    SHA256.HashData(
                        [1, 2, 3]))
                    .ToLowerInvariant());

        var recordService =
            new CurseForgePublicationRecordService(
                artifactService);

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => recordService.WriteAsync(
                    project.Path,
                    "1.1.0",
                    plan,
                    new CurseForgeUploadResult(
                        20402),
                    DateTimeOffset.UtcNow));

        Assert.Contains(
            "does not match",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
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
                responseFactory(request));
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            utcNow;
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
