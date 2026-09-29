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
