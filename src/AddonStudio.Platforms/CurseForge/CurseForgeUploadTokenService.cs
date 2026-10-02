using AddonStudio.Application.Settings;

namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgeUploadTokenService(
    ILocalSecretStore secretStore)
{
    public const string SecretName =
        "curseforge-upload-token";

    public string? Load()
    {
        var value =
            secretStore.Load(
                SecretName);

        return string.IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }

    public void Save(
        string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            token);

        secretStore.Save(
            SecretName,
            token.Trim());
    }

    public void Delete() =>
        secretStore.Delete(
            SecretName);
}
