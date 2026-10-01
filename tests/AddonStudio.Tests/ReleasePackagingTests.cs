using System.IO.Compression;
using AddonStudio.Core.Projects;
using AddonStudio.Packaging.Releases;

namespace AddonStudio.Tests;

public sealed class ReleasePackagingTests
{
    [Fact]
    public void Preparation_IsReadyWhenRequiredReleaseDataExists()
    {
        using var temp =
            new TemporaryProject();

        temp.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Title: ForeverBag
            ## Version: 1.1.0
            ForeverBag.lua
            """);

        temp.CreatePublishingContent(
            "1.1.0");

        var service =
            new ReleasePreparationService();

        var snapshot =
            service.Inspect(
                new ReleasePreparationRequest(
                    temp.Path,
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: true));

        Assert.True(
            snapshot.IsReady);
        Assert.Equal(
            0,
            snapshot.ErrorCount);
        Assert.Contains(
            snapshot.Issues,
            issue =>
                issue.Code ==
                "SCREENSHOTS_OPTIONAL");
    }

    [Fact]
    public void Preparation_RejectsVersionThatIsNotPortable()
    {
        using var temp =
            new TemporaryProject();

        temp.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Version: 1:1.0
            ForeverBag.lua
            """);

        temp.CreateProjectPageOnly();

        var snapshot =
            new ReleasePreparationService()
                .Inspect(
                    new ReleasePreparationRequest(
                        temp.Path,
                        CreateManifest(),
                        "1:1.0",
                        CurseForgeProjectVerified: true));

        Assert.Contains(
            snapshot.Issues,
            issue =>
                issue.Code ==
                "RELEASE_VERSION_INVALID");
    }

    [Fact]
    public void Preparation_RejectsMissingVersionedChangelog()
    {
        using var temp =
            new TemporaryProject();

        temp.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Title: ForeverBag
            ## Version: 1.1.0
            ForeverBag.lua
            """);

        temp.CreateProjectPageOnly();

        var service =
            new ReleasePreparationService();

        var snapshot =
            service.Inspect(
                new ReleasePreparationRequest(
                    temp.Path,
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: true));

        Assert.False(
            snapshot.IsReady);

        Assert.Contains(
            snapshot.Issues,
            issue =>
                issue.Code ==
                    "CHANGELOG_MISSING" &&
                issue.Severity ==
                    ReleasePreparationSeverity.Error);
    }

    [Fact]
    public void Preparation_RequiresVerifiedCurseForgeBinding()
    {
        using var temp =
            new TemporaryProject();

        temp.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Title: ForeverBag
            ## Version: 1.1.0
            ForeverBag.lua
            """);

        temp.CreatePublishingContent(
            "1.1.0");

        var service =
            new ReleasePreparationService();

        var snapshot =
            service.Inspect(
                new ReleasePreparationRequest(
                    temp.Path,
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: false));

        Assert.Contains(
            snapshot.Issues,
            issue =>
                issue.Code ==
                "CURSEFORGE_BINDING_NOT_VERIFIED");
    }

    [Fact]
    public async Task PackageBuilder_CreatesRuntimeOnlyZip()
    {
        using var temp =
            new TemporaryProject();

        var addonDirectory =
            temp.CreateRuntimeAddon(
                "ForeverBag",
                """
                ## Interface: 16001
                ## Title: ForeverBag
                ## Version: 1.1.0
                ForeverBag.lua
                """);

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(
                addonDirectory,
                "ForeverBag.lua"),
            "print('ForeverBag')");

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(
                addonDirectory,
                "README.md"),
            "do not package");

        var gitDirectory =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    addonDirectory,
                    ".git"));

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(
                gitDirectory.FullName,
                "config"),
            "do not package");

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(
                temp.Path,
                "project.json.backup"),
            "studio-only");

        var builder =
            new ReleasePackageBuilder();

        var result =
            await builder.BuildAsync(
                new ReleasePackageRequest(
                    temp.Path,
                    CreateManifest(),
                    "1.1.0"));

        Assert.Equal(
            "ForeverBag-1.1.0.zip",
            result.FileName);

        Assert.Equal(
            System.IO.Path.Combine(
                temp.Path,
                "Release",
                "Versions",
                "1.1.0",
                "ForeverBag-1.1.0.zip"),
            result.PackagePath);

        Assert.True(
            result.SizeBytes > 0);
        Assert.Equal(
            64,
            result.Sha256.Length);
        Assert.All(
            result.Sha256,
            character =>
                Assert.True(
                    char.IsAsciiHexDigit(character)));

        using var archive =
            ZipFile.OpenRead(
                result.PackagePath);

        var entries =
            archive.Entries
                .Select(entry =>
                    entry.FullName)
                .ToArray();

        Assert.Contains(
            "ForeverBag/ForeverBag.toc",
            entries);
        Assert.Contains(
            "ForeverBag/ForeverBag.lua",
            entries);
        Assert.DoesNotContain(
            entries,
            entry =>
                entry.EndsWith(
                    ".md",
                    StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            entries,
            entry =>
                entry.Contains(
                    "/.git/",
                    StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            entries,
            entry =>
                entry.Contains(
                    "project.json",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PackageBuilder_IsDeterministicForUnchangedInput()
    {
        using var temp =
            new TemporaryProject();

        temp.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Version: 1.1.0
            ForeverBag.lua
            """);

        var builder =
            new ReleasePackageBuilder();

        var first =
            await builder.BuildAsync(
                new ReleasePackageRequest(
                    temp.Path,
                    CreateManifest(),
                    "1.1.0"));

        var firstBytes =
            await File.ReadAllBytesAsync(
                first.PackagePath);

        var second =
            await builder.BuildAsync(
                new ReleasePackageRequest(
                    temp.Path,
                    CreateManifest(),
                    "1.1.0"));

        var secondBytes =
            await File.ReadAllBytesAsync(
                second.PackagePath);

        Assert.Equal(
            firstBytes,
            secondBytes);
        Assert.Equal(
            first.Sha256,
            second.Sha256);
    }

    [Fact]
    public async Task Workflow_DoesNotBuildWhenPreparationFails()
    {
        using var temp =
            new TemporaryProject();

        temp.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Version: 1.1.0
            ForeverBag.lua
            """);

        temp.CreateProjectPageOnly();

        var workflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                new ReleaseArtifactManifestService());

        var result =
            await workflow.PrepareAsync(
                new ReleasePreparationRequest(
                    temp.Path,
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        Assert.False(
            result.Preparation.IsReady);
        Assert.False(
            result.PackageCreated);
        Assert.False(
            result.ArtifactManifestCreated);

        Assert.False(
            File.Exists(
                System.IO.Path.Combine(
                    temp.Path,
                    "Release",
                    "Versions",
                    "1.1.0",
                    "ForeverBag-1.1.0.zip")));
    }

    [Fact]
    public async Task Workflow_BuildsPackageWhenPreparationIsReady()
    {
        using var temp =
            new TemporaryProject();

        temp.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Version: 1.1.0
            ForeverBag.lua
            """);

        temp.CreatePublishingContent(
            "1.1.0");

        var workflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                new ReleaseArtifactManifestService());

        var result =
            await workflow.PrepareAsync(
                new ReleasePreparationRequest(
                    temp.Path,
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        Assert.True(
            result.Preparation.IsReady);
        Assert.True(
            result.PackageCreated);
        Assert.True(
            result.ArtifactManifestCreated);
        Assert.NotNull(
            result.Package);
        Assert.NotNull(
            result.ArtifactManifestPath);
        Assert.True(
            File.Exists(
                result.Package.PackagePath));
        Assert.True(
            File.Exists(
                result.ArtifactManifestPath));

        var verification =
            await new ReleaseArtifactManifestService()
                .VerifyAsync(
                    temp.Path,
                    "1.1.0");

        Assert.True(
            verification.IsValid);
        Assert.Empty(
            verification.Issues);
    }

    [Fact]
    public async Task ArtifactVerification_DetectsTamperedPackage()
    {
        using var temp =
            new TemporaryProject();

        temp.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Version: 1.1.0
            ForeverBag.lua
            """);

        temp.CreatePublishingContent(
            "1.1.0");

        var workflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                new ReleaseArtifactManifestService());

        var result =
            await workflow.PrepareAsync(
                new ReleasePreparationRequest(
                    temp.Path,
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        var package =
            Assert.IsType<ReleasePackageResult>(
                result.Package);

        await File.AppendAllTextAsync(
            package.PackagePath,
            "tampered");

        var verification =
            await new ReleaseArtifactManifestService()
                .VerifyAsync(
                    temp.Path,
                    "1.1.0");

        Assert.False(
            verification.IsValid);
        Assert.Contains(
            verification.Issues,
            issue =>
                issue.Contains(
                    "SHA-256",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task ArtifactVerification_RejectsEscapingPackageFileName()
    {
        using var temp =
            new TemporaryProject();

        var versionDirectory =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    temp.Path,
                    "Release",
                    "Versions",
                    "1.1.0"))
                .FullName;

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(
                versionDirectory,
                ReleaseArtifactManifestService.FileName),
            """
            {
              "schemaVersion": 1,
              "version": "1.1.0",
              "packageFileName": "../outside.zip",
              "sizeBytes": 0,
              "sha256": "00",
              "entries": []
            }
            """);

        var service =
            new ReleaseArtifactManifestService();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.VerifyAsync(
                temp.Path,
                "1.1.0"));
    }

    [Fact]
    public async Task PackageBuilder_UsesConfiguredPackageName()
    {
        using var temp =
            new TemporaryProject();

        temp.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Version: 1.1.0
            ForeverBag.lua
            """);

        var manifest =
            CreateManifest() with
            {
                Release =
                    new ReleaseConfiguration
                    {
                        PackageName =
                            "Pallando-ForeverBag"
                    }
            };

        var builder =
            new ReleasePackageBuilder();

        var result =
            await builder.BuildAsync(
                new ReleasePackageRequest(
                    temp.Path,
                    manifest,
                    "1.1.0"));

        Assert.Equal(
            "Pallando-ForeverBag-1.1.0.zip",
            result.FileName);
    }

    [Fact]
    public async Task History_SortsNumericVersionsNewestFirst()
    {
        using var temp =
            new TemporaryProject();

        temp.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Version: 1.10.0
            ForeverBag.lua
            """);

        temp.CreatePublishingContent(
            "1.9.0");
        temp.CreatePublishingContent(
            "1.10.0");

        var builder =
            new ReleasePackageBuilder();

        await builder.BuildAsync(
            new ReleasePackageRequest(
                temp.Path,
                CreateManifest(),
                "1.9.0"));

        await builder.BuildAsync(
            new ReleasePackageRequest(
                temp.Path,
                CreateManifest(),
                "1.10.0"));

        var history =
            new ReleaseHistoryService()
                .GetHistory(
                    temp.Path);

        Assert.Equal(
            ["1.10.0", "1.9.0"],
            history
                .Select(entry =>
                    entry.Version)
                .ToArray());
    }

    [Fact]
    public async Task History_ReportsChangelogAndPackagePerVersion()
    {
        using var temp =
            new TemporaryProject();

        temp.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Version: 1.1.0
            ForeverBag.lua
            """);

        temp.CreatePublishingContent(
            "1.1.0");

        var workflow =
            new ReleaseWorkflowService(
                new ReleasePreparationService(),
                new ReleasePackageBuilder(),
                new ReleaseArtifactManifestService());

        var workflowResult =
            await workflow.PrepareAsync(
                new ReleasePreparationRequest(
                    temp.Path,
                    CreateManifest(),
                    "1.1.0",
                    CurseForgeProjectVerified: true),
                buildPackage: true);

        var package =
            Assert.IsType<ReleasePackageResult>(
                workflowResult.Package);

        var history =
            new ReleaseHistoryService()
                .GetHistory(
                    temp.Path);

        var entry =
            Assert.Single(
                history);

        Assert.Equal(
            "1.1.0",
            entry.Version);
        Assert.True(
            entry.HasChangelog);
        Assert.True(
            entry.HasArtifactManifest);
        Assert.Contains(
            package.PackagePath,
            entry.PackageFiles);
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

    private sealed class TemporaryProject : IDisposable
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

        public string CreateRuntimeAddon(
            string addonName,
            string tocContent)
        {
            var addonDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        Path,
                        "AddOns",
                        addonName))
                    .FullName;

            File.WriteAllText(
                System.IO.Path.Combine(
                    addonDirectory,
                    addonName + ".toc"),
                tocContent);

            if (!File.Exists(
                    System.IO.Path.Combine(
                        addonDirectory,
                        addonName + ".lua")))
            {
                File.WriteAllText(
                    System.IO.Path.Combine(
                        addonDirectory,
                        addonName + ".lua"),
                    "-- addon");
            }

            return addonDirectory;
        }

        public void CreateProjectPageOnly()
        {
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

        public void CreatePublishingContent(
            string version)
        {
            CreateProjectPageOnly();

            var versionDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        Path,
                        "Release",
                        "Versions",
                        version))
                    .FullName;

            File.WriteAllText(
                System.IO.Path.Combine(
                    versionDirectory,
                    "CHANGELOG.md"),
                "Changes");
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
