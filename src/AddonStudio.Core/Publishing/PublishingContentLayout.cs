namespace AddonStudio.Core.Publishing;

public static class PublishingContentLayout
{
    public static string GetFileName(
        PublishingContentKind kind) =>
        kind switch
        {
            PublishingContentKind.Summary =>
                "SUMMARY.md",
            PublishingContentKind.Description =>
                "DESCRIPTION.md",
            PublishingContentKind.Changelog =>
                "CHANGELOG.md",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unknown publishing content kind.")
        };
}
