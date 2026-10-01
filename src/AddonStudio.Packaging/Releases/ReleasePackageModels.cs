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
    string Sha256,
    IReadOnlyList<string> Entries);
