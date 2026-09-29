using AddonStudio.Core.Wow;
using AddonStudio.Wow.Toc;

namespace AddonStudio.Tests;

public sealed class TocDocumentReaderTests
{
    [Fact]
    public async Task ReadAsync_ParsesMetadataAndRuntimeFiles()
    {
        using var temp = new TempDirectory();

        var path = Path.Combine(
            temp.Path,
            "ForeverBag.toc");

        await File.WriteAllTextAsync(
            path,
            """
            ## Interface: 11507
            ## Title: ForeverBag
            ## Notes: Modern bag replacement
            ## Version: 1.2.3
            ## SavedVariables: ForeverBagDB, ForeverBagCache
            ## OptionalDeps: LibStub, LibDataBroker-1.1

            # Runtime files
            Core.lua
            UI/Main.xml
            """);

        var document =
            await new TocDocumentReader()
                .ReadAsync(path);

        Assert.Equal(
            "ForeverBag",
            document.Title);
        Assert.Equal(
            "Modern bag replacement",
            document.Notes);
        Assert.Equal(
            "1.2.3",
            document.Version);
        Assert.Equal(
            ["11507"],
            document.Interfaces);
        Assert.Equal(
            ["ForeverBagDB", "ForeverBagCache"],
            document.SavedVariables);
        Assert.Equal(
            ["LibStub", "LibDataBroker-1.1"],
            document.OptionalDependencies);
        Assert.Equal(
            ["Core.lua", "UI/Main.xml"],
            document.Files);
    }

    [Fact]
    public async Task ReadAsync_MetadataKeysAreCaseInsensitiveAndLastValueWins()
    {
        using var temp = new TempDirectory();

        var path = Path.Combine(
            temp.Path,
            "Sample.toc");

        await File.WriteAllTextAsync(
            path,
            """
            ## Version: 1.0.0
            ## version: 1.0.1
            Core.lua
            """);

        var document =
            await new TocDocumentReader()
                .ReadAsync(path);

        Assert.Equal(
            "1.0.1",
            document.GetMetadata("VERSION"));
        Assert.Equal(
            "1.0.1",
            document.Version);
    }

    [Fact]
    public async Task ReadAsync_PreservesLineKindsAndRawText()
    {
        using var temp = new TempDirectory();

        var path = Path.Combine(
            temp.Path,
            "Sample.toc");

        await File.WriteAllTextAsync(
            path,
            """
              ## Title: Sample
            # comment

              Core.lua
            """);

        var document =
            await new TocDocumentReader()
                .ReadAsync(path);

        Assert.Equal(
            TocLineKind.Metadata,
            document.Lines[0].Kind);
        Assert.Equal(
            "  ## Title: Sample",
            document.Lines[0].RawText);
        Assert.Equal(
            TocLineKind.Comment,
            document.Lines[1].Kind);
        Assert.Equal(
            TocLineKind.Blank,
            document.Lines[2].Kind);
        Assert.Equal(
            TocLineKind.File,
            document.Lines[3].Kind);
        Assert.Equal(
            "Core.lua",
            document.Lines[3].Value);
    }

    [Fact]
    public async Task ReadAsync_MalformedMetadataLineRemainsComment()
    {
        using var temp = new TempDirectory();

        var path = Path.Combine(
            temp.Path,
            "Sample.toc");

        await File.WriteAllTextAsync(
            path,
            """
            ## This is not metadata
            Core.lua
            """);

        var document =
            await new TocDocumentReader()
                .ReadAsync(path);

        Assert.Equal(
            TocLineKind.Comment,
            document.Lines[0].Kind);
        Assert.Empty(document.Metadata);
    }

    [Fact]
    public async Task ReadAsync_RejectsNonTocFile()
    {
        using var temp = new TempDirectory();

        var path = Path.Combine(
            temp.Path,
            "Sample.txt");

        await File.WriteAllTextAsync(
            path,
            "## Title: Sample");

        var reader = new TocDocumentReader();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => reader.ReadAsync(path));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
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
