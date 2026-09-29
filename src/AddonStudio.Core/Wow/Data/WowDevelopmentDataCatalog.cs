namespace AddonStudio.Core.Wow.Data;

public sealed record WowDevelopmentDataCatalog(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<WowClientBuild> Clients,
    IReadOnlyList<WowDatasetDescriptor> Datasets);
