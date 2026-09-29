namespace AddonStudio.Core.Wow.Data;

public sealed record WowDataSourceReference(
    string Type,
    string Provider,
    string? Reference,
    DateTimeOffset ObservedAt);
