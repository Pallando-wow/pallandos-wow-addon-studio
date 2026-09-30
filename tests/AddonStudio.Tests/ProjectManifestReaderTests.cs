using AddonStudio.Core.Projects;
using AddonStudio.Data.Projects;

namespace AddonStudio.Tests;

public class ProjectManifestReaderTests
{
    [Fact]
    public async Task ReaderLoadsManifestWithoutOptionalComponents()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "project": {
                "id": "minimal-addon",
                "name": "Minimal Addon",
                "type": "addon"
              },
              "runtime": {
                "primaryAddon": "MinimalAddon",
                "addons": [
                  "MinimalAddon"
                ]
              }
            }
            """;

        var path = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(path, json);

            var reader = new ProjectManifestReader();
            var manifest = await reader.ReadAsync(path);

            Assert.Equal(ProjectType.Addon, manifest.Project.Type);
            Assert.Empty(manifest.Components);
            Assert.Null(manifest.CurseForge);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReaderLoadsExtendedCurseForgeConfiguration()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "project": {
                "id": "curseforge-addon",
                "name": "CurseForge Addon",
                "type": "addon"
              },
              "runtime": {
                "primaryAddon": "CurseForgeAddon",
                "addons": [
                  "CurseForgeAddon"
                ]
              },
              "curseForge": {
                "projectId": "12345",
                "slug": "curseforge-addon",
                "mainCategoryId": "1",
                "additionalCategoryIds": [
                  "2",
                  "3"
                ],
                "license": "GPL-3.0",
                "allowDistribution": false
              }
            }
            """;

        var path = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(path, json);

            var reader = new ProjectManifestReader();
            var manifest = await reader.ReadAsync(path);

            Assert.NotNull(manifest.CurseForge);
            Assert.Equal("12345", manifest.CurseForge.ProjectId);
            Assert.Equal("curseforge-addon", manifest.CurseForge.Slug);
            Assert.Equal("1", manifest.CurseForge.MainCategoryId);
            Assert.Equal(
                ["2", "3"],
                manifest.CurseForge.AdditionalCategoryIds);
            Assert.Equal("GPL-3.0", manifest.CurseForge.License);
            Assert.False(manifest.CurseForge.AllowDistribution);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReaderRejectsUnknownManifestProperties()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "project": {
                "id": "invalid-addon",
                "name": "Invalid Addon",
                "type": "addon"
              },
              "runtime": {
                "primaryAddon": "InvalidAddon",
                "addons": [
                  "InvalidAddon"
                ]
              },
              "unexpected": true
            }
            """;

        var path = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(path, json);

            var reader = new ProjectManifestReader();

            await Assert.ThrowsAsync<System.Text.Json.JsonException>(
                () => reader.ReadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
