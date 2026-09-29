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
    public async Task Service_WritesCanonicalPublishingFiles()
    {
        using var temp = new TempDirectory();

        var projectDirectory = Directory.CreateDirectory(
            Path.Combine(
                temp.Path,
                "ForeverBag")).FullName;

        File.WriteAllText(
            Path.Combine(
                projectDirectory,
                "project.json"),
            "{}");

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

        await service.WriteAsync(
            projectDirectory,
            PublishingContentKind.Changelog,
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
                    "CHANGELOG.md")));
    }

    [Fact]
    public async Task Service_ReadMissingContentReturnsEmptyText()
    {
        using var temp = new TempDirectory();

        var projectDirectory = Directory.CreateDirectory(
            Path.Combine(
                temp.Path,
                "ForeverBag")).FullName;

        File.WriteAllText(
            Path.Combine(
                projectDirectory,
                "project.json"),
            "{}");

        var service =
            new PublishingContentService();

        var content = await service.ReadAsync(
            projectDirectory,
            PublishingContentKind.Description);

        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public void ResolveAll_ReturnsExactlyThreePublishingTargets()
    {
        using var temp = new TempDirectory();

        var projectDirectory = Directory.CreateDirectory(
            Path.Combine(
                temp.Path,
                "ForeverBag")).FullName;

        File.WriteAllText(
            Path.Combine(
                projectDirectory,
                "project.json"),
            "{}");

        var service =
            new PublishingContentService();

        var files = service.ResolveAll(projectDirectory);

        Assert.Equal(3, files.Count);
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

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "AddonStudio.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
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
