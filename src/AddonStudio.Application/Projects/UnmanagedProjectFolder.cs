namespace AddonStudio.Application.Projects;

public sealed record UnmanagedProjectFolder(
    string DirectoryPath,
    AddonSourceInspection? AddonInspection)
{
    public string Name =>
        Path.GetFileName(
            DirectoryPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));

    public bool IsImportableAddon =>
        AddonInspection is not null;

    public string StatusText =>
        IsImportableAddon
            ? "Import required"
            : "Unmanaged folder";

    public int RuntimeAddonCount =>
        AddonInspection?.RuntimeAddons.Count ?? 0;

    public string RuntimeAddonSummary =>
        AddonInspection is null
            ? string.Empty
            : RuntimeAddonCount == 1
                ? "1 runtime addon"
                : $"{RuntimeAddonCount} runtime addons";
}
