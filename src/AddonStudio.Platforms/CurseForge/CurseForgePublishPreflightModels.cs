namespace AddonStudio.Platforms.CurseForge;

public sealed record CurseForgePublishPreflightResult(
    CurseForgePublishPreparation LocalPreparation,
    bool UploadApiChecked,
    bool UploadApiReachable,
    IReadOnlyList<CurseForgeUploadGameVersion> SelectedGameVersions,
    IReadOnlyList<int> MissingGameVersionIds,
    IReadOnlyList<string> Issues)
{
    public bool IsReadyForExplicitUpload =>
        LocalPreparation.IsReadyForUpload &&
        UploadApiChecked &&
        UploadApiReachable &&
        MissingGameVersionIds.Count == 0 &&
        Issues.Count == 0;
}
