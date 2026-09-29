namespace AddonStudio.Application.Projects;

public interface IAddonSourceInspector
{
    Task<AddonSourceInspection> InspectAsync(
        string sourcePath,
        CancellationToken cancellationToken = default);
}
