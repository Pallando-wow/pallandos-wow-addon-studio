namespace AddonStudio.Core.Wow.Data;

public sealed record WowDatasetDescriptor(
    string Id,
    string ClientId,
    string Kind,
    string Path,
    int SchemaVersion,
    long RecordCount,
    DateTimeOffset GeneratedAt);
