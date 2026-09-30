using AddonStudio.Application.WowData;

namespace AddonStudio.Tests;

public sealed class PallandoCollectorSourceTests
{
    [Fact]
    public void ResolveFromSavedVariablesDirectory_UsesFixedFileName()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "WoW",
            "SavedVariables");

        var path =
            PallandoCollectorSource
                .ResolveFromSavedVariablesDirectory(
                    directory);

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(directory),
                "PallandoDataCollector.lua"),
            path);
    }
}
