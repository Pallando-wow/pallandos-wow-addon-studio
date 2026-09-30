using AddonStudio.Media;

namespace AddonStudio.Tests;

public sealed class ProjectMediaServiceTests
{
    [Fact]
    public void GetSnapshot_ReturnsEmptyMedia_WhenFoldersDoNotExist()
    {
        using var project = new TemporaryDirectory();
        var service = new ProjectMediaService();

        var snapshot = service.GetSnapshot(project.Path);

        Assert.Null(snapshot.LogoFilePath);
        Assert.Empty(snapshot.ScreenshotFilePaths);
    }

    [Fact]
    public async Task SetLogoAsync_StoresCanonicalPng()
    {
        using var project = new TemporaryDirectory();
        using var source = new TemporaryDirectory();
        var sourcePath = Path.Combine(source.Path, "MyLogo.PNG");
        await File.WriteAllTextAsync(sourcePath, "png");

        var service = new ProjectMediaService();

        var storedPath = await service.SetLogoAsync(
            project.Path,
            sourcePath);

        Assert.Equal(
            Path.Combine(
                project.Path,
                "Media",
                "Logo",
                "logo.png"),
            storedPath);
        Assert.True(File.Exists(storedPath));
    }

    [Fact]
    public async Task SetLogoAsync_RejectsNonPng()
    {
        using var project = new TemporaryDirectory();
        using var source = new TemporaryDirectory();
        var sourcePath = Path.Combine(source.Path, "logo.jpg");
        await File.WriteAllTextAsync(sourcePath, "jpg");

        var service = new ProjectMediaService();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.SetLogoAsync(
                project.Path,
                sourcePath));
    }

    [Fact]
    public async Task AddScreenshotsAsync_KeepsExistingFileAndAddsUniqueName()
    {
        using var project = new TemporaryDirectory();
        using var source = new TemporaryDirectory();

        var firstSource = Path.Combine(source.Path, "overview.png");
        await File.WriteAllTextAsync(firstSource, "first");

        var secondSource = Path.Combine(source.Path, "second.png");
        await File.WriteAllTextAsync(secondSource, "second");

        var service = new ProjectMediaService();

        await service.AddScreenshotsAsync(
            project.Path,
            [firstSource]);

        File.Move(
            secondSource,
            Path.Combine(source.Path, "overview-2.png"));

        var anotherOverview = Path.Combine(source.Path, "overview.png");
        await File.WriteAllTextAsync(anotherOverview, "replacement");

        var added = await service.AddScreenshotsAsync(
            project.Path,
            [anotherOverview]);

        Assert.Single(added);
        Assert.EndsWith(
            "overview-2.png",
            added[0],
            StringComparison.OrdinalIgnoreCase);

        var snapshot = service.GetSnapshot(project.Path);

        Assert.Equal(
            2,
            snapshot.ScreenshotFilePaths.Count);
    }

    [Fact]
    public async Task RemoveScreenshot_DeletesOnlyProjectScreenshot()
    {
        using var project = new TemporaryDirectory();
        using var source = new TemporaryDirectory();

        var sourcePath =
            Path.Combine(
                source.Path,
                "overview.png");

        await File.WriteAllTextAsync(
            sourcePath,
            "image");

        var service =
            new ProjectMediaService();

        var added =
            await service.AddScreenshotsAsync(
                project.Path,
                [sourcePath]);

        var screenshotPath =
            Assert.Single(
                added);

        service.RemoveScreenshot(
            project.Path,
            screenshotPath);

        Assert.False(
            File.Exists(
                screenshotPath));

        Assert.Empty(
            service.GetSnapshot(
                project.Path)
                .ScreenshotFilePaths);
    }

    [Fact]
    public async Task RemoveScreenshot_RejectsFileOutsideScreenshotFolder()
    {
        using var project = new TemporaryDirectory();

        var outsideFile =
            Path.Combine(
                project.Path,
                "outside.png");

        await File.WriteAllTextAsync(
            outsideFile,
            "image");

        var service =
            new ProjectMediaService();

        Assert.Throws<InvalidOperationException>(
            () => service.RemoveScreenshot(
                project.Path,
                outsideFile));

        Assert.True(
            File.Exists(
                outsideFile));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
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
