using AddonStudio.Wow.Projects;

namespace AddonStudio.Tests;

public sealed class AddonSourceInspectorTests
{
    [Fact]
    public async Task InspectAsync_DetectsDirectAddonFolder()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            Path.Combine(temp.Path, "ExampleAddon.toc"),
            "## Title: ExampleAddon");
        File.WriteAllText(
            Path.Combine(temp.Path, "Core.lua"),
            "print('test')");

        var inspector = new AddonSourceInspector();

        var result = await inspector.InspectAsync(temp.Path);

        Assert.Equal("ExampleAddon", result.SuggestedProjectName);
        var addon = Assert.Single(result.RuntimeAddons);
        Assert.Equal("ExampleAddon", addon.Name);
        Assert.Single(addon.TocFiles);
    }

    [Fact]
    public async Task InspectAsync_DetectsMultipleChildAddonFolders()
    {
        using var temp = new TempDirectory();

        foreach (var name in new[] { "Suite", "Suite_Module" })
        {
            var directory = Directory.CreateDirectory(
                Path.Combine(temp.Path, name)).FullName;
            File.WriteAllText(
                Path.Combine(directory, $"{name}.toc"),
                $"## Title: {name}");
        }

        var inspector = new AddonSourceInspector();

        var result = await inspector.InspectAsync(temp.Path);

        Assert.Equal(2, result.RuntimeAddons.Count);
        Assert.Contains(result.RuntimeAddons, addon => addon.Name == "Suite");
        Assert.Contains(result.RuntimeAddons, addon => addon.Name == "Suite_Module");
    }

    [Fact]
    public async Task InspectAsync_DetectsLegacyProjectWithAddOnsDirectory()
    {
        using var temp = new TempDirectory();

        var addonDirectory = Directory.CreateDirectory(
            Path.Combine(
                temp.Path,
                "AddOns",
                "ForeverBag")).FullName;

        File.WriteAllText(
            Path.Combine(addonDirectory, "ForeverBag.toc"),
            "## Title: ForeverBag");
        File.WriteAllText(
            Path.Combine(addonDirectory, "ForeverBag.lua"),
            "print('test')");

        Directory.CreateDirectory(
            Path.Combine(temp.Path, "Documentation"));
        Directory.CreateDirectory(
            Path.Combine(temp.Path, "CurseForge"));

        var inspector = new AddonSourceInspector();

        var result = await inspector.InspectAsync(temp.Path);

        Assert.Equal("ForeverBag", result.SuggestedProjectName);
        var addon = Assert.Single(result.RuntimeAddons);
        Assert.Equal("ForeverBag", addon.Name);
        Assert.Equal(addonDirectory, addon.SourcePath);
    }

    [Fact]
    public async Task InspectAsync_DetectsSingleArchiveWrapperWithAddOnsDirectory()
    {
        using var temp = new TempDirectory();

        var wrapper = Directory.CreateDirectory(
            Path.Combine(temp.Path, "ForeverBag")).FullName;

        var addonDirectory = Directory.CreateDirectory(
            Path.Combine(
                wrapper,
                "AddOns",
                "ForeverBag")).FullName;

        File.WriteAllText(
            Path.Combine(addonDirectory, "ForeverBag.toc"),
            "## Title: ForeverBag");

        var inspector = new AddonSourceInspector();

        var result = await inspector.InspectAsync(temp.Path);

        Assert.Equal("ForeverBag", result.SuggestedProjectName);
        var addon = Assert.Single(result.RuntimeAddons);
        Assert.Equal(addonDirectory, addon.SourcePath);
    }

    [Fact]
    public async Task InspectAsync_RejectsDirectoryWithoutToc()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            Path.Combine(temp.Path, "Core.lua"),
            "print('test')");

        var inspector = new AddonSourceInspector();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => inspector.InspectAsync(temp.Path));
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
