using AddonStudio.Core.Projects;

namespace AddonStudio.Packaging.Releases;

public sealed record ReleasePackageRequest(
    string ProjectDirectory,
    ProjectManifest Manifest,
    string Version);

public sealed record ReleasePackageResult(
    string PackagePath,
    string FileName,
    long SizeBytes,
    IReadOnlyList<string> Entries);
