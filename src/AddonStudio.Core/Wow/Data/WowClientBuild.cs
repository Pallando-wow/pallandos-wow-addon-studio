namespace AddonStudio.Core.Wow.Data;

public sealed record WowClientBuild(
    string Id,
    string Name,
    string Version,
    int Build,
    int Interface,
    WowDataSourceReference Source);
