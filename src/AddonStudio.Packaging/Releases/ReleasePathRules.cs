namespace AddonStudio.Packaging.Releases;

internal static class ReleasePathRules
{
    private const string PortableInvalidFileNameCharacters =
        "<>:\"/\\|?*";

    public static string RequireVersionDirectoryName(
        string? version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            version);

        var value =
            version.Trim();

        if (value is "." or ".." ||
            value.Any(character =>
                char.IsControl(character) ||
                PortableInvalidFileNameCharacters
                    .Contains(character)))
        {
            throw new ArgumentException(
                "Release version cannot be used as a directory name.",
                nameof(version));
        }

        return value;
    }

    public static string SanitizePackageBaseName(
        string? value)
    {
        var baseName =
            string.IsNullOrWhiteSpace(value)
                ? "Addon"
                : Path.GetFileNameWithoutExtension(
                    value.Trim());

        var safeBaseName =
            new string(
                baseName
                    .Select(character =>
                        char.IsControl(character) ||
                        PortableInvalidFileNameCharacters
                            .Contains(character)
                            ? '-'
                            : character)
                    .ToArray())
                .Trim(
                    ' ',
                    '.',
                    '-');

        return safeBaseName.Length == 0
            ? "Addon"
            : safeBaseName;
    }

    public static string RequireSimpleZipFileName(
        string? fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            fileName);

        var value =
            fileName.Trim();

        if (!string.Equals(
                Path.GetFileName(value),
                value,
                StringComparison.Ordinal) ||
            value.Any(character =>
                char.IsControl(character) ||
                character is '/' or '\\') ||
            !value.EndsWith(
                ".zip",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Release artifact package file name is invalid.");
        }

        return value;
    }
}
