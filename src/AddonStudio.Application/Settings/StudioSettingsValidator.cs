namespace AddonStudio.Application.Settings;

public static class StudioSettingsValidator
{
    public static IReadOnlyList<string> Validate(StudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var issues = new List<string>();

        ValidateDirectory(
            settings.ProjectRoot,
            "Global project root",
            issues);

        ValidateDirectory(
            settings.WowForeverAddOnsPath,
            "WoW Forever AddOns path",
            issues);

        return issues;
    }

    public static bool IsComplete(StudioSettings settings) =>
        Validate(settings).Count == 0;

    private static void ValidateDirectory(
        string? value,
        string label,
        ICollection<string> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add($"{label} is required.");
            return;
        }

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(value);
        }
        catch (Exception)
        {
            issues.Add($"{label} is not a valid path.");
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            issues.Add($"{label} does not exist: {fullPath}");
        }
    }
}
