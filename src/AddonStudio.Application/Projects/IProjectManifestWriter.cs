using AddonStudio.Core.Projects;

namespace AddonStudio.Application.Projects;

public interface IProjectManifestWriter
{
    Task WriteAsync(
        string path,
        ProjectManifest manifest,
        CancellationToken cancellationToken = default);
}
