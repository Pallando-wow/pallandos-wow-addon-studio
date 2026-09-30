namespace AddonStudio.Application.Settings;

public interface ILocalSecretStore
{
    string? Load(string key);

    void Save(string key, string value);

    void Delete(string key);

    void DeleteAll();
}
