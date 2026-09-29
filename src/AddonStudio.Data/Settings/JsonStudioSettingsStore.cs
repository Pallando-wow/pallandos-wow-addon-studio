using System.Text.Json;
using AddonStudio.Application.Settings;

namespace AddonStudio.Data.Settings;

public sealed class JsonStudioSettingsStore : IStudioSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
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

            return JsonSerializer.Deserialize<StudioSettings>(
                       json,
                       SerializerOptions)
                   ?? new StudioSettings();
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

        var json = JsonSerializer.Serialize(
            settings,
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
}
