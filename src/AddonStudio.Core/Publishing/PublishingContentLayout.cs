namespace AddonStudio.Core.Publishing;

public static class PublishingContentLayout
{
    public const string VersionsDirectoryName =
        "Versions";

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

    public static string GetReleaseChangelogRelativePath(
        string version)
    {
        var versionDirectory =
            RequireVersionDirectoryName(
                version);

        return Path.Combine(
            VersionsDirectoryName,
            versionDirectory,
            GetFileName(
                PublishingContentKind.Changelog));
    }

    private static string RequireVersionDirectoryName(
        string? version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            version);

        var value = version.Trim();

        if (value is "." or ".." ||
            value.IndexOfAny(
                Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains(
                Path.DirectorySeparatorChar) ||
            value.Contains(
                Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException(
                "Release version cannot be used as a directory name.",
                nameof(version));
        }

        return value;
    }
}
