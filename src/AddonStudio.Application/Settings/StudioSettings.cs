namespace AddonStudio.Application.Settings;

public sealed class StudioSettings
{
    public string ProjectRoot { get; init; } = string.Empty;

    public string WowForeverAddOnsPath { get; init; } = string.Empty;

    public string SavedVariablesPath { get; init; } = string.Empty;

    public string CurseForgeApiKey { get; init; } = string.Empty;
}
