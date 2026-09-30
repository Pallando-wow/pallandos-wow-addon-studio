using AddonStudio.Platforms.Security;

namespace AddonStudio.Tests;

public sealed class WindowsProtectedSecretStoreTests
{
    [Fact]
    public void Store_RoundTripsProtectedSecret()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var temp = new TempDirectory();
        var store =
            new WindowsProtectedSecretStore(
                temp.Path);

        store.Save(
            "curseforge-api-key",
            "super-secret-value");

        var loaded =
            store.Load(
                "curseforge-api-key");

        Assert.Equal(
            "super-secret-value",
            loaded);

        var file =
            Assert.Single(
                Directory.GetFiles(
                    temp.Path));

        var bytes =
            File.ReadAllBytes(file);

        Assert.DoesNotContain(
            "super-secret-value",
            System.Text.Encoding.UTF8.GetString(bytes),
            StringComparison.Ordinal);

        store.Delete(
            "curseforge-api-key");

        Assert.Null(
            store.Load(
                "curseforge-api-key"));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "AddonStudio.Tests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
        }
    }
}
