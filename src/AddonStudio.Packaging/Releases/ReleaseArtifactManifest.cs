namespace AddonStudio.Packaging.Releases;

public sealed record ReleaseArtifactManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public required string Version { get; init; }

    public required string PackageFileName { get; init; }

    public long SizeBytes { get; init; }

    public required string Sha256 { get; init; }

    public IReadOnlyList<string> Entries { get; init; } = [];
}

public sealed record ReleaseArtifactVerificationResult(
    bool IsValid,
    IReadOnlyList<string> Issues);
