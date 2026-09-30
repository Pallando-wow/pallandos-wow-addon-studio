namespace AddonStudio.Application.WowData;

public static class PallandoCollectorSource
{
    public const string FileName =
        "PallandoDataCollector.lua";

    public static string ResolveFromSavedVariablesDirectory(
        string savedVariablesDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            savedVariablesDirectory);

        return Path.Combine(
            Path.GetFullPath(
                savedVariablesDirectory),
            FileName);
    }
}
