using AddonStudio.Application.Projects;
using AddonStudio.Core.Projects;
using AddonStudio.Data.Projects;

namespace AddonStudio.Tests;

public sealed class ProjectCurseForgeSettingsServiceTests
{
    [Fact]
    public async Task SaveAsync_NormalizesAndPersistsConfiguration()
    {
        var projectDirectory = Path.Combine(
            Path.GetTempPath(),
            "AddonStudio.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(projectDirectory);

        try
        {
            var manifest = CreateManifest();
            var writer = new ProjectManifestWriter();
            var service =
                new ProjectCurseForgeSettingsService(
                    writer);

            var updated = await service.SaveAsync(
                projectDirectory,
                manifest,
                new CurseForgeConfiguration
                {
                    ProjectId = " 12345 ",
                    Slug = " forever-bag ",
                    MainCategoryId = " 10 ",
                    AdditionalCategoryIds =
                    [
                        "20",
                        " 30 ",
                        "20"
                    ],
                    License = " GPL-3.0 ",
                    AllowDistribution = true
                });

            Assert.NotNull(updated.CurseForge);
            Assert.Equal("12345", updated.CurseForge.ProjectId);
            Assert.Equal("forever-bag", updated.CurseForge.Slug);
            Assert.Equal("10", updated.CurseForge.MainCategoryId);
            Assert.Equal(
                ["20", "30"],
                updated.CurseForge.AdditionalCategoryIds);
            Assert.Equal("GPL-3.0", updated.CurseForge.License);
            Assert.True(updated.CurseForge.AllowDistribution);

            var reader = new ProjectManifestReader();
            var persisted = await reader.ReadAsync(
                Path.Combine(
                    projectDirectory,
                    ProjectLayout.ManifestFileName));

            Assert.NotNull(persisted.CurseForge);
            Assert.Equal(
                updated.CurseForge.ProjectId,
                persisted.CurseForge.ProjectId);
            Assert.Equal(
                updated.CurseForge.Slug,
                persisted.CurseForge.Slug);
            Assert.Equal(
                updated.CurseForge.MainCategoryId,
                persisted.CurseForge.MainCategoryId);
            Assert.Equal(
                updated.CurseForge.AdditionalCategoryIds,
                persisted.CurseForge.AdditionalCategoryIds);
            Assert.Equal(
                updated.CurseForge.License,
                persisted.CurseForge.License);
            Assert.Equal(
                updated.CurseForge.AllowDistribution,
                persisted.CurseForge.AllowDistribution);
        }
        finally
        {
            Directory.Delete(
                projectDirectory,
                recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_RemovesEmptyConfiguration()
    {
        var projectDirectory = Path.Combine(
            Path.GetTempPath(),
            "AddonStudio.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(projectDirectory);

        try
        {
            var writer = new ProjectManifestWriter();
            var service =
                new ProjectCurseForgeSettingsService(
                    writer);

            var updated = await service.SaveAsync(
                projectDirectory,
                CreateManifest(),
                new CurseForgeConfiguration());

            Assert.Null(updated.CurseForge);
        }
        finally
        {
            Directory.Delete(
                projectDirectory,
                recursive: true);
        }
    }

    private static ProjectManifest CreateManifest() =>
        new()
        {
            Project = new ProjectIdentity
            {
                Id = "forever-bag",
                Name = "ForeverBag",
                Type = ProjectType.Addon
            },
            Runtime = new RuntimeLayout
            {
                PrimaryAddon = "ForeverBag",
                Addons =
                [
                    "ForeverBag"
                ]
            }
        };
}
