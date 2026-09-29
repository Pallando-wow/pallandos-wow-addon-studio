using AddonStudio.Application.Documents;

namespace AddonStudio.Tests;

public sealed class MarkdownDocumentServiceTests
{
    [Fact]
    public async Task OpenAsync_ReadsMarkdownInsideManagedProject()
    {
        using var temp = new TempDirectory();

        var project = CreateManagedProject(temp.Path);
        var path = Path.Combine(
            project,
            "Documentation",
            "PROJECT.md");

        Directory.CreateDirectory(
            Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            "# Project");

        var service = new MarkdownDocumentService();

        var document = await service.OpenAsync(
            project,
            path);

        Assert.Equal(
            "PROJECT.md",
            document.DisplayName);
        Assert.Equal(
            "# Project",
            document.Text);
        Assert.Equal(
            Path.GetFullPath(path),
            document.FilePath);
    }

    [Fact]
    public async Task SaveAsync_WritesUtf8Markdown()
    {
        using var temp = new TempDirectory();

        var project = CreateManagedProject(temp.Path);
        var path = Path.Combine(
            project,
            "Release",
            "DESCRIPTION.enUS.md");

        Directory.CreateDirectory(
            Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            "Old");

        var service = new MarkdownDocumentService();

        await service.SaveAsync(
            project,
            path,
            "# New description äöü");

        Assert.Equal(
            "# New description äöü",
            await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task OpenAsync_RejectsFileOutsideProject()
    {
        using var temp = new TempDirectory();

        var project = CreateManagedProject(temp.Path);
        var outside = Path.Combine(
            temp.Path,
            "outside.md");

        await File.WriteAllTextAsync(
            outside,
            "# Outside");

        var service = new MarkdownDocumentService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.OpenAsync(
                project,
                outside));
    }

    [Fact]
    public async Task OpenAsync_RejectsNonMarkdownFile()
    {
        using var temp = new TempDirectory();

        var project = CreateManagedProject(temp.Path);
        var path = Path.Combine(
            project,
            "AddOns",
            "Sample",
            "Core.lua");

        Directory.CreateDirectory(
            Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            "local addonName = ...");

        var service = new MarkdownDocumentService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.OpenAsync(
                project,
                path));
    }

    private static string CreateManagedProject(
        string root)
    {
        var project = Directory.CreateDirectory(
            Path.Combine(
                root,
                "Sample")).FullName;

        File.WriteAllText(
            Path.Combine(
                project,
                "project.json"),
            "{}");

        return project;
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
