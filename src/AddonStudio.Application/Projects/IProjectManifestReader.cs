using AddonStudio.Core.Projects;

namespace AddonStudio.Application.Projects;

public interface IProjectManifestReader
{
    Task<ProjectManifest> ReadAsync(
        string path,
        CancellationToken cancellationToken = default);
}
