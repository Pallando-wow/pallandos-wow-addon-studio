using System.Text;
using System.Text.RegularExpressions;
using AddonStudio.Core.Projects;

namespace AddonStudio.Application.Projects;

public sealed class AddonProjectService(
    IAddonSourceInspector sourceInspector,
    IProjectManifestWriter manifestWriter)
{
    public async Task<ProjectOperationResult> CreateAsync(
        CreateAddonProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var projectName = RequireProjectName(request.Name);
        var destinationDirectory = RequireExistingDirectory(
            request.DestinationDirectory,
            "Destination directory");

        var projectDirectory = GetNewProjectDirectory(destinationDirectory, projectName);
        Directory.CreateDirectory(projectDirectory);

        try
        {
            var runtimeRoot = Path.Combine(
                projectDirectory,
                ProjectLayout.RuntimeDirectoryName);
            var runtimeDirectory = Path.Combine(runtimeRoot, projectName);

            Directory.CreateDirectory(runtimeDirectory);
            CreateOptionalProjectDirectories(projectDirectory);

            var tocPath = Path.Combine(runtimeDirectory, $"{projectName}.toc");
            await File.WriteAllTextAsync(
                tocPath,
                BuildInitialToc(projectName),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            var luaPath = Path.Combine(runtimeDirectory, "Core.lua");
            await File.WriteAllTextAsync(
                luaPath,
                BuildInitialLua(projectName),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            var manifest = CreateManifest(projectName, [projectName]);
            await manifestWriter.WriteAsync(
                Path.Combine(projectDirectory, ProjectLayout.ManifestFileName),
                manifest,
                cancellationToken);

            return new ProjectOperationResult(
                projectDirectory,
                projectName,
                [projectName]);
        }
        catch
        {
            TryDeleteNewProjectDirectory(projectDirectory);
            throw;
        }
    }

    public async Task<ProjectOperationResult> ImportAsync(
        ImportAddonProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var sourceDirectory = RequireExistingDirectory(
            request.SourceDirectory,
            "Source directory");
        var destinationDirectory = RequireExistingDirectory(
            request.DestinationDirectory,
            "Destination directory");

        if (IsSameOrChildPath(
                sourceDirectory,
                destinationDirectory))
        {
            throw new InvalidOperationException(
                "Import source must be outside the global project root. Move the source folder outside the project root and import it from there.");
        }

        var inspection = await sourceInspector.InspectAsync(
            sourceDirectory,
            cancellationToken);

        var projectName = string.IsNullOrWhiteSpace(request.ProjectName)
            ? inspection.SuggestedProjectName
            : RequireProjectName(request.ProjectName);

        var projectDirectory = GetNewProjectDirectory(destinationDirectory, projectName);
        Directory.CreateDirectory(projectDirectory);

        try
        {
            var runtimeRoot = Path.Combine(
                projectDirectory,
                ProjectLayout.RuntimeDirectoryName);
            Directory.CreateDirectory(runtimeRoot);

            foreach (var runtimeAddon in inspection.RuntimeAddons)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var targetDirectory = Path.Combine(runtimeRoot, runtimeAddon.Name);
                CopyDirectory(
                    runtimeAddon.SourcePath,
                    targetDirectory,
                    cancellationToken);
            }

            CreateOptionalProjectDirectories(projectDirectory);

            var runtimeNames = inspection.RuntimeAddons
                .Select(addon => addon.Name)
                .ToArray();

            var manifest = CreateManifest(projectName, runtimeNames);
            await manifestWriter.WriteAsync(
                Path.Combine(projectDirectory, ProjectLayout.ManifestFileName),
                manifest,
                cancellationToken);

            return new ProjectOperationResult(
                projectDirectory,
                projectName,
                runtimeNames);
        }
        catch
        {
            TryDeleteNewProjectDirectory(projectDirectory);
            throw;
        }
    }

    private static ProjectManifest CreateManifest(
        string projectName,
        IReadOnlyList<string> runtimeAddons)
    {
        var primaryAddon = runtimeAddons.First();

        return new ProjectManifest
        {
            Project = new ProjectIdentity
            {
                Id = CreateProjectId(projectName),
                Name = projectName,
                Type = ProjectType.Addon
            },
            Runtime = new RuntimeLayout
            {
                PrimaryAddon = primaryAddon,
                Addons = runtimeAddons
            }
        };
    }

    private static string RequireProjectName(string? value)
    {
        var name = value?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.");
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                $"Project name '{name}' contains invalid filename characters.");
        }

        return name;
    }

    private static string RequireExistingDirectory(
        string? value,
        string label)
    {
        var path = value?.Trim();

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"{label} is required.");
        }

        var fullPath = Path.GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"{label} '{fullPath}' does not exist.");
        }

        return fullPath;
    }

    private static bool IsSameOrChildPath(
        string path,
        string parentPath)
    {
        var normalizedPath = Path.GetFullPath(path)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        var normalizedParent = Path.GetFullPath(parentPath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        if (string.Equals(
                normalizedPath,
                normalizedParent,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parentPrefix =
            normalizedParent + Path.DirectorySeparatorChar;

        return normalizedPath.StartsWith(
            parentPrefix,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetNewProjectDirectory(
        string destinationDirectory,
        string projectName)
    {
        var projectDirectory = Path.Combine(destinationDirectory, projectName);

        if (Directory.Exists(projectDirectory) ||
            File.Exists(projectDirectory))
        {
            throw new IOException(
                $"Destination '{projectDirectory}' already exists.");
        }

        return projectDirectory;
    }

    private static void CreateOptionalProjectDirectories(string projectDirectory)
    {
        Directory.CreateDirectory(Path.Combine(
            projectDirectory,
            ProjectLayout.DocumentationDirectoryName));
        Directory.CreateDirectory(Path.Combine(
            projectDirectory,
            ProjectLayout.ReleaseDirectoryName));

        var mediaDirectory = Path.Combine(
            projectDirectory,
            ProjectLayout.MediaDirectoryName);

        Directory.CreateDirectory(Path.Combine(
            mediaDirectory,
            ProjectLayout.LogoDirectoryName));
        Directory.CreateDirectory(Path.Combine(
            mediaDirectory,
            ProjectLayout.ScreenshotsDirectoryName));
    }

    private static string BuildInitialToc(string projectName) =>
        $"""
        ## Title: {projectName}
        ## Notes: Created with Pallando's WoW Addon Studio
        ## Version: 0.1.0

        Core.lua
        """;

    private static string BuildInitialLua(string projectName) =>
        $"""
        local addonName = ...

        -- {projectName}
        """;

    private static string CreateProjectId(string projectName)
    {
        var normalized = projectName
            .Normalize(NormalizationForm.FormD)
            .ToLowerInvariant();

        var builder = new StringBuilder(normalized.Length);
        var previousDash = false;

        foreach (var character in normalized)
        {
            if ((character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9') ||
                character == '.')
            {
                builder.Append(character);
                previousDash = false;
                continue;
            }

            if (!previousDash && builder.Length > 0)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        var id = Regex.Replace(builder.ToString().Trim('-'), "-{2,}", "-");

        return string.IsNullOrWhiteSpace(id)
            ? "addon"
            : id;
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(file);

            if (string.Equals(
                    fileName,
                    ProjectLayout.ManifestFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(
                file,
                Path.Combine(targetDirectory, fileName),
                overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = Path.GetFileName(directory);

            if (string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyDirectory(
                directory,
                Path.Combine(targetDirectory, name),
                cancellationToken);
        }
    }

    private static void TryDeleteNewProjectDirectory(string projectDirectory)
    {
        try
        {
            if (Directory.Exists(projectDirectory))
            {
                Directory.Delete(projectDirectory, recursive: true);
            }
        }
        catch
        {
            // Preserve the original exception.
        }
    }
}
