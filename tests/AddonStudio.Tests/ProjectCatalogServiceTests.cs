using AddonStudio.Application.Projects;
using AddonStudio.Core.Projects;
using AddonStudio.Data.Projects;
using AddonStudio.Wow.Projects;

namespace AddonStudio.Tests;

public sealed class ProjectCatalogServiceTests
{
    [Fact]
    public async Task DiscoverAsync_FindsValidProjectsAndReportsInvalidOnes()
    {
        using var temp = new TempDirectory();

        await CreateProjectAsync(
            temp.Path,
            "ForeverBag",
            ProjectType.Addon,
            ["ForeverBag"]);

        var invalidDirectory = Directory.CreateDirectory(
            Path.Combine(temp.Path, "BrokenProject")).FullName;

        await File.WriteAllTextAsync(
            Path.Combine(invalidDirectory, "project.json"),
            "{ not valid json");

        var service = new ProjectCatalogService(
            new ProjectManifestReader(),
            new AddonSourceInspector());

        var result = await service.DiscoverAsync(temp.Path);

        var project = Assert.Single(result.Projects);
        Assert.Equal("ForeverBag", project.Name);
        Assert.Equal(ProjectType.Addon, project.Type);
        Assert.Equal(1, project.RuntimeAddonCount);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(invalidDirectory, issue.ProjectDirectory);
    }

    [Fact]
    public async Task DiscoverAsync_SkipsManifestWhenRuntimeAddonDirectoryIsMissing()
    {
        using var temp = new TempDirectory();

        var projectDirectory = Directory.CreateDirectory(
            Path.Combine(temp.Path, "MissingRuntime")).FullName;

        var writer = new ProjectManifestWriter();

        await writer.WriteAsync(
            Path.Combine(projectDirectory, "project.json"),
            CreateManifest(
                "MissingRuntime",
                ProjectType.Addon,
                ["MissingRuntime"]));

        var service = new ProjectCatalogService(
            new ProjectManifestReader(),
            new AddonSourceInspector());

        var result = await service.DiscoverAsync(temp.Path);

        Assert.Empty(result.Projects);
        Assert.Single(result.Issues);
    }

    [Fact]
    public async Task DiscoverAsync_ReportsLegacyAddonFolderAsImportRequired()
    {
        using var temp = new TempDirectory();

        var legacyProject = Directory.CreateDirectory(
            Path.Combine(temp.Path, "ForeverBag")).FullName;

        var runtimeAddon = Directory.CreateDirectory(
            Path.Combine(
                legacyProject,
                "AddOns",
                "ForeverBag")).FullName;

        File.WriteAllText(
            Path.Combine(runtimeAddon, "ForeverBag.toc"),
            "## Title: ForeverBag");

        var service = new ProjectCatalogService(
            new ProjectManifestReader(),
            new AddonSourceInspector());

        var result = await service.DiscoverAsync(temp.Path);

        Assert.Empty(result.Projects);
        Assert.Empty(result.Issues);

        var unmanaged = Assert.Single(result.UnmanagedFolders);

        Assert.Equal("ForeverBag", unmanaged.Name);
        Assert.True(unmanaged.IsImportableAddon);
        Assert.Equal("Import required", unmanaged.StatusText);
        Assert.Equal(1, unmanaged.RuntimeAddonCount);
    }

    [Fact]
    public async Task DiscoverAsync_ReportsUnknownFolderAsUnmanaged()
    {
        using var temp = new TempDirectory();

        Directory.CreateDirectory(
            Path.Combine(temp.Path, "Notes"));

        var service = new ProjectCatalogService(
            new ProjectManifestReader(),
            new AddonSourceInspector());

        var result = await service.DiscoverAsync(temp.Path);

        var unmanaged = Assert.Single(result.UnmanagedFolders);

        Assert.Equal("Notes", unmanaged.Name);
        Assert.False(unmanaged.IsImportableAddon);
        Assert.Equal("Unmanaged folder", unmanaged.StatusText);
    }

    private static async Task CreateProjectAsync(
        string root,
        string name,
        ProjectType type,
        IReadOnlyList<string> runtimeAddons)
    {
        var projectDirectory = Directory.CreateDirectory(
            Path.Combine(root, name)).FullName;

        foreach (var runtimeAddon in runtimeAddons)
        {
            Directory.CreateDirectory(
                Path.Combine(
                    projectDirectory,
                    "AddOns",
                    runtimeAddon));
        }

        var writer = new ProjectManifestWriter();

        await writer.WriteAsync(
            Path.Combine(projectDirectory, "project.json"),
            CreateManifest(name, type, runtimeAddons));
    }

    private static ProjectManifest CreateManifest(
        string name,
        ProjectType type,
        IReadOnlyList<string> runtimeAddons) =>
        new()
        {
            Project = new ProjectIdentity
            {
                Id = name.ToLowerInvariant(),
                Name = name,
                Type = type
            },
            Runtime = new RuntimeLayout
            {
                PrimaryAddon = runtimeAddons[0],
                Addons = runtimeAddons
            }
        };

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
