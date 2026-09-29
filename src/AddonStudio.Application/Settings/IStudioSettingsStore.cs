namespace AddonStudio.Application.Settings;

public interface IStudioSettingsStore
{
    StudioSettings Load();

    void Save(StudioSettings settings);

    void Delete();
}
