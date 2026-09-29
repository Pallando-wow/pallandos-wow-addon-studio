using AddonStudio.Core.Projects;

namespace AddonStudio.Application.Projects;

public sealed class ProjectCatalogService(
    IProjectManifestReader manifestReader,
    IAddonSourceInspector addonSourceInspector)
{
    public async Task<ProjectCatalogResult> DiscoverAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var fullRoot = Path.GetFullPath(projectRoot);

        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException(
                $"Project root '{fullRoot}' does not exist.");
        }

        var projects = new List<ProjectCatalogEntry>();
        var unmanagedFolders = new List<UnmanagedProjectFolder>();
        var issues = new List<ProjectCatalogIssue>();

        foreach (var projectDirectory in Directory
                     .EnumerateDirectories(fullRoot)
                     .OrderBy(
                         path => Path.GetFileName(path),
                         StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var manifestPath = Path.Combine(
                projectDirectory,
                ProjectLayout.ManifestFileName);

            if (!File.Exists(manifestPath))
            {
                var inspection = await TryInspectAddonSourceAsync(
                    projectDirectory,
                    cancellationToken);

                unmanagedFolders.Add(
                    new UnmanagedProjectFolder(
                        projectDirectory,
                        inspection));

                continue;
            }

            try
            {
                var manifest = await manifestReader.ReadAsync(
                    manifestPath,
                    cancellationToken);

                var validationIssues =
                    ProjectManifestValidator.Validate(manifest);

                var errors = validationIssues
                    .Where(issue =>
                        issue.Severity ==
                        ProjectValidationSeverity.Error)
                    .ToArray();

                if (errors.Length > 0)
                {
                    issues.Add(
                        new ProjectCatalogIssue(
                            projectDirectory,
                            string.Join(
                                "; ",
                                errors.Select(error =>
                                    $"{error.Code}: {error.Message}"))));
                    continue;
                }

                var missingRuntimeAddons = manifest.Runtime.Addons
                    .Where(addon =>
                        !Directory.Exists(
                            Path.Combine(
                                projectDirectory,
                                ProjectLayout.RuntimeDirectoryName,
                                addon)))
                    .ToArray();

                if (missingRuntimeAddons.Length > 0)
                {
                    issues.Add(
                        new ProjectCatalogIssue(
                            projectDirectory,
                            $"Missing runtime addon directories: {string.Join(", ", missingRuntimeAddons)}"));
                    continue;
                }

                projects.Add(
                    new ProjectCatalogEntry(
                        projectDirectory,
                        manifest));
            }
            catch (Exception exception)
                when (exception is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or System.Text.Json.JsonException)
            {
                issues.Add(
                    new ProjectCatalogIssue(
                        projectDirectory,
                        exception.Message));
            }
        }

        return new ProjectCatalogResult(
            projects,
            unmanagedFolders,
            issues);
    }

    private async Task<AddonSourceInspection?> TryInspectAddonSourceAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        try
        {
            return await addonSourceInspector.InspectAsync(
                directory,
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is InvalidDataException
                or InvalidOperationException)
        {
            return null;
        }
    }
}
