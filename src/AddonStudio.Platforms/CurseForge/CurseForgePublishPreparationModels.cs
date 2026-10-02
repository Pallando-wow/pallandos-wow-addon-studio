namespace AddonStudio.Platforms.CurseForge;

public sealed record CurseForgePublishPreparation(
    string Version,
    CurseForgePublicationState PublicationState,
    bool HasUploadToken,
    bool RequiresRepublishConfirmation,
    CurseForgeUploadPlan? UploadPlan,
    IReadOnlyList<string> Issues)
{
    public bool IsReadyForUpload =>
        UploadPlan is not null &&
        HasUploadToken &&
        !RequiresRepublishConfirmation &&
        Issues.Count == 0;
}
