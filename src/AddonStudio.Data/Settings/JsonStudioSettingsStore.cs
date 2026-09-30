using System.Text.Json;
using System.Text.Json.Serialization;
using AddonStudio.Application.Settings;

namespace AddonStudio.Data.Settings;

public sealed class JsonStudioSettingsStore : IStudioSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition =
            JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string settingsPath;

    public JsonStudioSettingsStore(string? settingsPath = null)
    {
        this.settingsPath = settingsPath ?? GetDefaultSettingsPath();
    }

    public StudioSettings Load()
    {
        if (!File.Exists(settingsPath))
        {
            return new StudioSettings();
        }

        try
        {
            var json = File.ReadAllText(settingsPath);

            var persisted =
                JsonSerializer.Deserialize<PersistedStudioSettings>(
                    json,
                    SerializerOptions)
                ?? new PersistedStudioSettings();

            return new StudioSettings
            {
                ProjectRoot =
                    persisted.ProjectRoot,
                WowForeverAddOnsPath =
                    persisted.WowForeverAddOnsPath,
                SavedVariablesPath =
                    persisted.SavedVariablesPath,
                CurseForgeApiKey =
                    persisted.CurseForgeApiKey ??
                    string.Empty
            };
        }
        catch (JsonException)
        {
            return new StudioSettings();
        }
        catch (IOException)
        {
            return new StudioSettings();
        }
    }

    public void Save(StudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(settingsPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var persisted =
            new PersistedStudioSettings
            {
                ProjectRoot =
                    settings.ProjectRoot,
                WowForeverAddOnsPath =
                    settings.WowForeverAddOnsPath,
                SavedVariablesPath =
                    settings.SavedVariablesPath
            };

        var json = JsonSerializer.Serialize(
            persisted,
            SerializerOptions);

        File.WriteAllText(settingsPath, json);
    }

    public void Delete()
    {
        if (File.Exists(settingsPath))
        {
            File.Delete(settingsPath);
        }
    }

    private static string GetDefaultSettingsPath()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            localApplicationData,
            "PallandosWowAddonStudio",
            "settings.json");
    }

    private sealed class PersistedStudioSettings
    {
        public string ProjectRoot { get; init; } =
            string.Empty;

        public string WowForeverAddOnsPath { get; init; } =
            string.Empty;

        public string SavedVariablesPath { get; init; } =
            string.Empty;

        // Legacy migration only. New saves deliberately omit this value.
        public string? CurseForgeApiKey { get; init; }
    }
}
