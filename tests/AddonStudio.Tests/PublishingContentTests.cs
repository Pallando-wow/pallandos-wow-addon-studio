using AddonStudio.Application.Publishing;
using AddonStudio.Core.Publishing;

namespace AddonStudio.Tests;

public sealed class PublishingContentTests
{
    [Theory]
    [InlineData(
        PublishingContentKind.Summary,
        "SUMMARY.md")]
    [InlineData(
        PublishingContentKind.Description,
        "DESCRIPTION.md")]
    [InlineData(
        PublishingContentKind.Changelog,
        "CHANGELOG.md")]
    public void GetFileName_UsesCanonicalMarkdownName(
        PublishingContentKind kind,
        string expected)
    {
        Assert.Equal(
            expected,
            PublishingContentLayout.GetFileName(kind));
    }

    [Fact]
    public void ReleaseChangelogPath_UsesVersionDirectory()
    {
        Assert.Equal(
            Path.Combine(
                "Versions",
                "1.1.0",
                "CHANGELOG.md"),
            PublishingContentLayout
                .GetReleaseChangelogRelativePath(
                    "1.1.0"));
    }

    [Fact]
    public async Task Service_WritesProjectPageAndVersionedChangelog()
    {
        using var temp = new TempDirectory();
        var projectDirectory =
            CreateProject(temp.Path);

        var service =
            new PublishingContentService();

        await service.WriteAsync(
            projectDirectory,
            PublishingContentKind.Summary,
            "English summary");

        await service.WriteAsync(
            projectDirectory,
            PublishingContentKind.Description,
            "## English\nText\n\n## Deutsch\nText");

        await service.WriteReleaseChangelogAsync(
            projectDirectory,
            "1.1.0",
            "## English\nChanges\n\n## Deutsch\nAenderungen");

        Assert.Equal(
            "English summary",
            File.ReadAllText(
                Path.Combine(
                    projectDirectory,
                    "Release",
                    "SUMMARY.md")));

        Assert.Contains(
            "## Deutsch",
            File.ReadAllText(
                Path.Combine(
                    projectDirectory,
                    "Release",
                    "DESCRIPTION.md")));

        Assert.Contains(
            "## Deutsch",
            File.ReadAllText(
                Path.Combine(
                    projectDirectory,
                    "Release",
                    "Versions",
                    "1.1.0",
                    "CHANGELOG.md")));
    }

    [Fact]
    public async Task ReleaseChangelog_ReadsLegacyFallback()
    {
        using var temp = new TempDirectory();
        var projectDirectory =
            CreateProject(temp.Path);

        var legacyPath = Path.Combine(
            projectDirectory,
            "Release",
            "CHANGELOG.md");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                legacyPath)!);

        await File.WriteAllTextAsync(
            legacyPath,
            "Legacy changes");

        var service =
            new PublishingContentService();

        Assert.True(
            service.IsReleaseChangelogUsingLegacyFallback(
                projectDirectory,
                "1.1.0"));

        var content =
            await service.ReadReleaseChangelogAsync(
                projectDirectory,
                "1.1.0");

        Assert.Equal(
            "Legacy changes",
            content);
    }

    [Fact]
    public async Task ReleaseChangelog_WriteCanMigrateLegacyFile()
    {
        using var temp = new TempDirectory();
        var projectDirectory =
            CreateProject(temp.Path);

        var legacyPath = Path.Combine(
            projectDirectory,
            "Release",
            "CHANGELOG.md");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                legacyPath)!);

        await File.WriteAllTextAsync(
            legacyPath,
            "Legacy changes");

        var service =
            new PublishingContentService();

        await service.WriteReleaseChangelogAsync(
            projectDirectory,
            "1.1.0",
            "Current changes",
            removeLegacyFile: true);

        Assert.False(
            File.Exists(
                legacyPath));

        Assert.Equal(
            "Current changes",
            File.ReadAllText(
                Path.Combine(
                    projectDirectory,
                    "Release",
                    "Versions",
                    "1.1.0",
                    "CHANGELOG.md")));
    }

    [Fact]
    public async Task Service_ReadMissingContentReturnsEmptyText()
    {
        using var temp = new TempDirectory();
        var projectDirectory =
            CreateProject(temp.Path);

        var service =
            new PublishingContentService();

        var content = await service.ReadAsync(
            projectDirectory,
            PublishingContentKind.Description);

        Assert.Equal(
            string.Empty,
            content);
    }

    [Fact]
    public void ResolveAll_ReturnsExactlyThreePublishingTargets()
    {
        using var temp = new TempDirectory();
        var projectDirectory =
            CreateProject(temp.Path);

        var service =
            new PublishingContentService();

        var files =
            service.ResolveAll(
                projectDirectory);

        Assert.Equal(
            3,
            files.Count);

        Assert.Contains(
            files,
            file => file.Kind ==
                PublishingContentKind.Summary);

        Assert.Contains(
            files,
            file => file.Kind ==
                PublishingContentKind.Description);

        Assert.Contains(
            files,
            file => file.Kind ==
                PublishingContentKind.Changelog);
    }

    private static string CreateProject(
        string root)
    {
        var projectDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    root,
                    "ForeverBag"))
                .FullName;

        File.WriteAllText(
            Path.Combine(
                projectDirectory,
                "project.json"),
            "{}");

        return projectDirectory;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "AddonStudio.Tests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
        }
    }
}
