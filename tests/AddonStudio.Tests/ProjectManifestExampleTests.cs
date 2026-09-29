using AddonStudio.Core.Projects;
using AddonStudio.Data.Projects;

namespace AddonStudio.Tests;

public class ProjectManifestExampleTests
{
    [Fact]
    public async Task RepositoryExamples_LoadAndPassDomainValidation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var examplesDirectory = Path.Combine(
            repositoryRoot,
            "examples",
            "project-manifests");

        var files = Directory.GetFiles(
            examplesDirectory,
            "*.project.json",
            SearchOption.TopDirectoryOnly);

        Assert.NotEmpty(files);

        var reader = new ProjectManifestReader();

        foreach (var file in files)
        {
            var manifest = await reader.ReadAsync(file);
            var issues = ProjectManifestValidator.Validate(manifest);

            Assert.DoesNotContain(
                issues,
                issue => issue.Severity == ProjectValidationSeverity.Error);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AddonStudio.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the AddonStudio repository root.");
    }
}
