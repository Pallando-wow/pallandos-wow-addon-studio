using AddonStudio.Application.Settings;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class CurseForgeUploadTokenServiceTests
{
    [Fact]
    public void SaveAndLoad_UseDedicatedSecretKey()
    {
        var store =
            new InMemorySecretStore();

        var service =
            new CurseForgeUploadTokenService(
                store);

        service.Save(
            "  upload-token  ");

        Assert.Equal(
            CurseForgeUploadTokenService.SecretName,
            store.LastSavedKey);
        Assert.Equal(
            "upload-token",
            store.Values[
                CurseForgeUploadTokenService.SecretName]);
        Assert.Equal(
            "upload-token",
            service.Load());
    }

    [Fact]
    public void Delete_RemovesOnlyUploadToken()
    {
        var store =
            new InMemorySecretStore();

        store.Values[
            "curseforge-api-key"] =
            "public-api-key";

        store.Values[
            CurseForgeUploadTokenService.SecretName] =
            "upload-token";

        var service =
            new CurseForgeUploadTokenService(
                store);

        service.Delete();

        Assert.False(
            store.Values.ContainsKey(
                CurseForgeUploadTokenService.SecretName));
        Assert.Equal(
            "public-api-key",
            store.Values[
                "curseforge-api-key"]);
    }

    [Fact]
    public void Load_ReturnsNullForMissingOrBlankToken()
    {
        var store =
            new InMemorySecretStore();

        var service =
            new CurseForgeUploadTokenService(
                store);

        Assert.Null(
            service.Load());

        store.Values[
            CurseForgeUploadTokenService.SecretName] =
            "   ";

        Assert.Null(
            service.Load());
    }

    [Fact]
    public void Save_RejectsBlankToken()
    {
        var service =
            new CurseForgeUploadTokenService(
                new InMemorySecretStore());

        Assert.Throws<ArgumentException>(
            () => service.Save(
                "   "));
    }

    private sealed class InMemorySecretStore :
        ILocalSecretStore
    {
        public Dictionary<string, string>
            Values { get; } =
                new(
                    StringComparer.Ordinal);

        public string? LastSavedKey { get; private set; }

        public string? Load(
            string key) =>
            Values.TryGetValue(
                key,
                out var value)
                ? value
                : null;

        public void Save(
            string key,
            string value)
        {
            LastSavedKey =
                key;

            Values[key] =
                value;
        }

        public void Delete(
            string key) =>
            Values.Remove(
                key);

        public void DeleteAll() =>
            Values.Clear();
    }
}
