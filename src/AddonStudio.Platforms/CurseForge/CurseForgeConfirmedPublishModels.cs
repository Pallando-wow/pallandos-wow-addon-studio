namespace AddonStudio.Platforms.CurseForge;

public sealed record CurseForgeConfirmedPublishRequest(
    string Version,
    int ProjectId,
    string ArtifactSha256,
    bool AllowRepublish = false);

public sealed record CurseForgeConfirmedPublishResult(
    CurseForgePublishPreflightResult Preflight,
    CurseForgeUploadExecutionResult Execution);
