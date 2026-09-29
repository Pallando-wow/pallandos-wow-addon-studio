using AddonStudio.Application.Projects;
using AddonStudio.Core.Projects;

namespace AddonStudio.Tests;

public sealed class ProjectExplorerServiceTests
{
    [Fact]
    public void BuildTree_ReturnsManagedProjectFilesInStableOrder()
    {
        using var temp = new TempDirectory();

        var projectDirectory = Directory.CreateDirectory(
            Path.Combine(temp.Path, "ForeverBag")).FullName;

        File.WriteAllText(
            Path.Combine(projectDirectory, "project.json"),
            "{}");

        var runtimeDirectory = Directory.CreateDirectory(
            Path.Combine(
                projectDirectory,
                "AddOns",
                "ForeverBag")).FullName;

        File.WriteAllText(
            Path.Combine(runtimeDirectory, "ForeverBag.toc"),
            "## Title: ForeverBag");
        File.WriteAllText(
            Path.Combine(runtimeDirectory, "Core.lua"),
            "print('test')");

        Directory.CreateDirectory(
            Path.Combine(projectDirectory, "Documentation"));
        Directory.CreateDirectory(
            Path.Combine(projectDirectory, "Release"));
        Directory.CreateDirectory(
            Path.Combine(projectDirectory, "Media"));
        Directory.CreateDirectory(
            Path.Combine(projectDirectory, ".git"));

        var project = new ProjectCatalogEntry(
            projectDirectory,
            new ProjectManifest
            {
                Project = new ProjectIdentity
                {
                    Id = "foreverbag",
                    Name = "ForeverBag",
                    Type = ProjectType.Addon
                },
                Runtime = new RuntimeLayout
                {
                    PrimaryAddon = "ForeverBag",
                    Addons = ["ForeverBag"]
                }
            });

        var service = new ProjectExplorerService();

        var tree = service.BuildTree(project);

        Assert.Equal(
            ["project.json", "AddOns", "Documentation", "Release", "Media"],
            tree.Select(item => item.Name).ToArray());

        var addOns = tree[1];
        var runtimeAddon = Assert.Single(addOns.Children);

        Assert.Equal("ForeverBag", runtimeAddon.Name);
        Assert.Contains(
            runtimeAddon.Children,
            item => item.Name == "ForeverBag.toc");
        Assert.Contains(
            runtimeAddon.Children,
            item => item.Name == "Core.lua");

        Assert.DoesNotContain(
            tree,
            item => item.Name == ".git");
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
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
