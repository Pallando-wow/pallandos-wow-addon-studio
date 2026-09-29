using AddonStudio.Core.Projects;

namespace AddonStudio.Application.Projects;

public sealed record ProjectCatalogEntry(
    string ProjectDirectory,
    ProjectManifest Manifest)
{
    public string Name => Manifest.Project.Name;

    public ProjectType Type => Manifest.Project.Type;

    public string TypeName => Type == ProjectType.Library
        ? "Library"
        : "Addon";

    public string PrimaryAddon =>
        Manifest.Runtime.PrimaryAddon ?? string.Empty;

    public int RuntimeAddonCount =>
        Manifest.Runtime.Addons.Count;
}
