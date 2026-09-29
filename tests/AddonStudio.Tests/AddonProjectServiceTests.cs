using AddonStudio.Application.Projects;
using AddonStudio.Data.Projects;
using AddonStudio.Wow.Projects;

namespace AddonStudio.Tests;

public sealed class AddonProjectServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesFixedStudioLayout()
    {
        using var destination = new TempDirectory();

        var service = CreateService();

        var result = await service.CreateAsync(
            new CreateAddonProjectRequest(
                "MyAddon",
                destination.Path));

        Assert.True(File.Exists(Path.Combine(
            result.ProjectDirectory,
            "project.json")));
        Assert.True(File.Exists(Path.Combine(
            result.ProjectDirectory,
            "AddOns",
            "MyAddon",
            "MyAddon.toc")));
        Assert.True(File.Exists(Path.Combine(
            result.ProjectDirectory,
            "AddOns",
            "MyAddon",
            "Core.lua")));
        Assert.True(Directory.Exists(Path.Combine(
            result.ProjectDirectory,
            "Documentation")));
        Assert.True(Directory.Exists(Path.Combine(
            result.ProjectDirectory,
            "Release")));
        Assert.True(Directory.Exists(Path.Combine(
            result.ProjectDirectory,
            "Media",
            "Logo")));
        Assert.True(Directory.Exists(Path.Combine(
            result.ProjectDirectory,
            "Media",
            "Screenshots")));
    }

    [Fact]
    public async Task ImportAsync_CopiesAndNormalizesWithoutChangingSource()
    {
        using var source = new TempDirectory();
        using var destination = new TempDirectory();

        File.WriteAllText(
            Path.Combine(source.Path, "ImportedAddon.toc"),
            "## Title: ImportedAddon");
        File.WriteAllText(
            Path.Combine(source.Path, "Core.lua"),
            "print('source')");

        var service = CreateService();

        var result = await service.ImportAsync(
            new ImportAddonProjectRequest(
                source.Path,
                destination.Path));

        Assert.True(File.Exists(Path.Combine(
            result.ProjectDirectory,
            "project.json")));
        Assert.True(File.Exists(Path.Combine(
            result.ProjectDirectory,
            "AddOns",
            "ImportedAddon",
            "ImportedAddon.toc")));
        Assert.True(File.Exists(Path.Combine(
            result.ProjectDirectory,
            "AddOns",
            "ImportedAddon",
            "Core.lua")));

        Assert.True(File.Exists(Path.Combine(
            source.Path,
            "ImportedAddon.toc")));
        Assert.True(File.Exists(Path.Combine(
            source.Path,
            "Core.lua")));
        Assert.False(File.Exists(Path.Combine(
            source.Path,
            "project.json")));
    }

    [Fact]
    public async Task ImportAsync_LegacyProjectCopiesOnlyRuntimeAddonContent()
    {
        using var source = new TempDirectory();
        using var destination = new TempDirectory();

        var runtimeAddon = Directory.CreateDirectory(
            Path.Combine(
                source.Path,
                "AddOns",
                "ForeverBag")).FullName;

        File.WriteAllText(
            Path.Combine(runtimeAddon, "ForeverBag.toc"),
            "## Title: ForeverBag\n## Version: 1.1.0");
        File.WriteAllText(
            Path.Combine(runtimeAddon, "ForeverBag.lua"),
            "print('runtime')");

        var documentation = Directory.CreateDirectory(
            Path.Combine(source.Path, "Documentation")).FullName;
        File.WriteAllText(
            Path.Combine(documentation, "NOTES.md"),
            "legacy notes");

        var curseForge = Directory.CreateDirectory(
            Path.Combine(source.Path, "CurseForge")).FullName;
        File.WriteAllText(
            Path.Combine(curseForge, "ReleaseNotes_EN.txt"),
            "Development draft for 0.1.82");

        var service = CreateService();

        var result = await service.ImportAsync(
            new ImportAddonProjectRequest(
                source.Path,
                destination.Path));

        Assert.True(File.Exists(Path.Combine(
            result.ProjectDirectory,
            "AddOns",
            "ForeverBag",
            "ForeverBag.toc")));
        Assert.True(File.Exists(Path.Combine(
            result.ProjectDirectory,
            "AddOns",
            "ForeverBag",
            "ForeverBag.lua")));

        Assert.False(File.Exists(Path.Combine(
            result.ProjectDirectory,
            "Documentation",
            "NOTES.md")));
        Assert.False(Directory.Exists(Path.Combine(
            result.ProjectDirectory,
            "CurseForge")));
        Assert.False(File.Exists(Path.Combine(
            result.ProjectDirectory,
            "Release",
            "ReleaseNotes_EN.txt")));
    }

    [Fact]
    public async Task ImportAsync_RejectsSourceInsideProjectRoot()
    {
        using var destination = new TempDirectory();

        var source = Directory.CreateDirectory(
            Path.Combine(destination.Path, "LegacyAddon")).FullName;

        File.WriteAllText(
            Path.Combine(source, "LegacyAddon.toc"),
            "## Title: LegacyAddon");

        var service = CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ImportAsync(
                new ImportAddonProjectRequest(
                    source,
                    destination.Path)));

        Assert.Contains(
            "outside the global project root",
            exception.Message);
    }

    private static AddonProjectService CreateService() =>
        new(
            new AddonSourceInspector(),
            new ProjectManifestWriter());

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
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
