using AddonStudio.Core.Components;
using System.Text.RegularExpressions;

namespace AddonStudio.Core.Projects;

public static class ProjectManifestValidator
{
    private static readonly Regex IdentifierPattern =
        new("^[a-z0-9][a-z0-9.-]*$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<ProjectValidationIssue> Validate(ProjectManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var issues = new List<ProjectValidationIssue>();

        if (manifest.SchemaVersion != ProjectManifest.CurrentSchemaVersion)
        {
            issues.Add(Error(
                "manifest.schema.unsupported",
                $"Schema version {manifest.SchemaVersion} is not supported. Expected {ProjectManifest.CurrentSchemaVersion}."));
        }

        ValidateProject(manifest.Project, issues);
        ValidateRuntime(manifest.Runtime, issues);
        ValidateComponents(manifest.Components, manifest.Runtime, issues);

        return issues;
    }

    private static void ValidateProject(
        ProjectIdentity project,
        ICollection<ProjectValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(project.Id) || !IdentifierPattern.IsMatch(project.Id))
        {
            issues.Add(Error(
                "project.id.invalid",
                "Project id must use lowercase letters, digits, dots or hyphens and must start with a letter or digit."));
        }

        if (string.IsNullOrWhiteSpace(project.Name))
        {
            issues.Add(Error("project.name.required", "Project name is required."));
        }
    }

    private static void ValidateRuntime(
        RuntimeLayout runtime,
        ICollection<ProjectValidationIssue> issues)
    {
        if (runtime.Addons.Count == 0)
        {
            issues.Add(Error("runtime.addons.required", "At least one runtime addon is required."));
            return;
        }

        if (runtime.Addons.Any(string.IsNullOrWhiteSpace))
        {
            issues.Add(Error(
                "runtime.addons.invalid",
                "Runtime addon names must not be empty."));
        }

        foreach (var duplicate in runtime.Addons
                     .Where(addon => !string.IsNullOrWhiteSpace(addon))
                     .GroupBy(addon => addon, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            issues.Add(Error(
                "runtime.addons.duplicate",
                $"Runtime addon '{duplicate}' is listed more than once."));
        }

        if (string.IsNullOrWhiteSpace(runtime.PrimaryAddon))
        {
            issues.Add(Error("runtime.primary.required", "Primary runtime addon is required."));
        }
        else if (!runtime.Addons.Contains(
                     runtime.PrimaryAddon,
                     StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "runtime.primary.unknown",
                $"Primary runtime addon '{runtime.PrimaryAddon}' is not present in the runtime addon list."));
        }
    }

    private static void ValidateComponents(
        IReadOnlyList<ComponentUsage> components,
        RuntimeLayout runtime,
        ICollection<ProjectValidationIssue> issues)
    {
        foreach (var duplicate in components
                     .GroupBy(component => component.Id, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            issues.Add(Error(
                "component.id.duplicate",
                $"Component '{duplicate}' is referenced more than once."));
        }

        foreach (var component in components)
        {
            if (string.IsNullOrWhiteSpace(component.Id) || !IdentifierPattern.IsMatch(component.Id))
            {
                issues.Add(Error(
                    "component.id.invalid",
                    $"Component id '{component.Id}' is invalid."));
            }

            if (component.Targets.Count == 0)
            {
                issues.Add(Error(
                    "component.targets.required",
                    $"Component '{component.Id}' requires at least one target."));
            }

            foreach (var target in component.Targets)
            {
                if (!runtime.Addons.Contains(target.RuntimeAddon, StringComparer.OrdinalIgnoreCase))
                {
                    issues.Add(Error(
                        "component.target.runtime-addon.unknown",
                        $"Component '{component.Id}' targets unknown runtime addon '{target.RuntimeAddon}'."));
                }

                if (string.IsNullOrWhiteSpace(target.Path))
                {
                    issues.Add(Error(
                        "component.target.path.required",
                        $"Component '{component.Id}' has an empty target path."));
                }
            }
        }
    }

    private static ProjectValidationIssue Error(string code, string message) =>
        new(ProjectValidationSeverity.Error, code, message);
}
