using AddonStudio.Application.Settings;
using AddonStudio.Data.Settings;

namespace AddonStudio.Tests;

public sealed class StudioSettingsTests
{
    [Fact]
    public void Store_RoundTripsSettings()
    {
        using var temp = new TempDirectory();

        var projectRoot = Directory.CreateDirectory(
            Path.Combine(temp.Path, "Projects")).FullName;
        var wowPath = Directory.CreateDirectory(
            Path.Combine(temp.Path, "WoW", "Interface", "AddOns")).FullName;
        var settingsPath = Path.Combine(temp.Path, "settings.json");

        var store = new JsonStudioSettingsStore(settingsPath);

        store.Save(
            new StudioSettings
            {
                ProjectRoot = projectRoot,
                WowForeverAddOnsPath = wowPath,
                CurseForgeApiKey = "test-api-key"
            });

        var loaded = store.Load();

        Assert.Equal(projectRoot, loaded.ProjectRoot);
        Assert.Equal(wowPath, loaded.WowForeverAddOnsPath);
        Assert.Equal("test-api-key", loaded.CurseForgeApiKey);
        Assert.True(StudioSettingsValidator.IsComplete(loaded));
    }

    [Fact]
    public void Validator_RequiresBothExistingDirectories()
    {
        using var temp = new TempDirectory();

        var settings = new StudioSettings
        {
            ProjectRoot = temp.Path
        };

        var issues = StudioSettingsValidator.Validate(settings);

        Assert.Single(issues);
        Assert.Contains("WoW Forever AddOns path", issues[0]);
    }

    [Fact]
    public void Store_ReturnsEmptySettingsWhenFileDoesNotExist()
    {
        using var temp = new TempDirectory();

        var store = new JsonStudioSettingsStore(
            Path.Combine(temp.Path, "missing.json"));

        var loaded = store.Load();

        Assert.Empty(loaded.ProjectRoot);
        Assert.Empty(loaded.WowForeverAddOnsPath);
        Assert.False(StudioSettingsValidator.IsComplete(loaded));
    }

    [Fact]
    public void Store_DeleteRemovesSavedSettings()
    {
        using var temp = new TempDirectory();

        var settingsPath = Path.Combine(temp.Path, "settings.json");
        var store = new JsonStudioSettingsStore(settingsPath);

        store.Save(
            new StudioSettings
            {
                ProjectRoot = temp.Path,
                WowForeverAddOnsPath = temp.Path
            });

        Assert.True(File.Exists(settingsPath));

        store.Delete();

        Assert.False(File.Exists(settingsPath));

        var loaded = store.Load();
        Assert.False(StudioSettingsValidator.IsComplete(loaded));
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
