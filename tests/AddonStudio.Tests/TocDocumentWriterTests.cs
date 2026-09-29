using AddonStudio.Wow.Toc;

namespace AddonStudio.Tests;

public sealed class TocDocumentWriterTests
{
    [Fact]
    public async Task SetMetadataAsync_UpdatesValueAndPreservesOtherLines()
    {
        using var temp = new TempDirectory();

        var path = Path.Combine(
            temp.Path,
            "Sample.toc");

        await File.WriteAllTextAsync(
            path,
            "## Interface: 11507\n" +
            "## X-Custom: keep me\n" +
            "## Version: 1.0.0\n" +
            "\n" +
            "# comment\n" +
            "Core.lua\n");

        var writer = new TocDocumentWriter();

        await writer.SetMetadataAsync(
            path,
            "Version",
            "1.1.0");

        var output =
            await File.ReadAllTextAsync(path);

        Assert.Contains(
            "## Version: 1.1.0",
            output);
        Assert.Contains(
            "## X-Custom: keep me",
            output);
        Assert.Contains(
            "# comment",
            output);
        Assert.EndsWith(
            "Core.lua\n",
            output);
    }

    [Fact]
    public async Task SetMetadataAsync_AddsNewMetadataBeforeRuntimeFiles()
    {
        using var temp = new TempDirectory();

        var path = Path.Combine(
            temp.Path,
            "Sample.toc");

        await File.WriteAllTextAsync(
            path,
            "## Interface: 11507\n" +
            "\n" +
            "Core.lua");

        var writer = new TocDocumentWriter();

        await writer.SetMetadataAsync(
            path,
            "Version",
            "1.0.0");

        var output =
            await File.ReadAllTextAsync(path);

        Assert.Equal(
            "## Interface: 11507\n" +
            "## Version: 1.0.0\n" +
            "\n" +
            "Core.lua",
            output);
    }

    [Fact]
    public async Task SetMetadataAsync_CollapsesDuplicateKey()
    {
        using var temp = new TempDirectory();

        var path = Path.Combine(
            temp.Path,
            "Sample.toc");

        await File.WriteAllTextAsync(
            path,
            "## Version: 1.0.0\n" +
            "## Title: Sample\n" +
            "## version: 1.0.1\n" +
            "Core.lua\n");

        var writer = new TocDocumentWriter();

        await writer.SetMetadataAsync(
            path,
            "VERSION",
            "1.1.0");

        var output =
            await File.ReadAllTextAsync(path);

        Assert.DoesNotContain(
            "1.0.0",
            output);
        Assert.DoesNotContain(
            "1.0.1",
            output);
        Assert.Contains(
            "## version: 1.1.0",
            output);
        Assert.Equal(
            1,
            output.Split('\n')
                .Count(line =>
                    line.StartsWith(
                        "## version:",
                        StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task RemoveMetadataAsync_RemovesAllMatchingDefinitions()
    {
        using var temp = new TempDirectory();

        var path = Path.Combine(
            temp.Path,
            "Sample.toc");

        await File.WriteAllTextAsync(
            path,
            "## Notes: Old\n" +
            "## Title: Sample\n" +
            "## notes: New\n" +
            "Core.lua");

        var writer = new TocDocumentWriter();

        await writer.RemoveMetadataAsync(
            path,
            "Notes");

        var output =
            await File.ReadAllTextAsync(path);

        Assert.DoesNotContain(
            "Notes:",
            output,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "## Title: Sample",
            output);
    }

    [Fact]
    public async Task ApplyMetadataAsync_PreservesCrLfAndTrailingNewLine()
    {
        using var temp = new TempDirectory();

        var path = Path.Combine(
            temp.Path,
            "Sample.toc");

        await File.WriteAllTextAsync(
            path,
            "## Interface: 11507\r\n" +
            "Core.lua\r\n");

        var writer = new TocDocumentWriter();

        await writer.ApplyMetadataAsync(
            path,
            new Dictionary<string, string?>
            {
                ["Version"] = "1.0.0",
                ["Notes"] = "Test"
            });

        var output =
            await File.ReadAllTextAsync(path);

        Assert.EndsWith(
            "\r\n",
            output);
        Assert.DoesNotContain(
            "\n",
            output.Replace(
                "\r\n",
                string.Empty,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetMetadataAsync_RejectsLineBreakInValue()
    {
        using var temp = new TempDirectory();

        var path = Path.Combine(
            temp.Path,
            "Sample.toc");

        await File.WriteAllTextAsync(
            path,
            "## Interface: 11507");

        var writer = new TocDocumentWriter();

        await Assert.ThrowsAsync<ArgumentException>(
            () => writer.SetMetadataAsync(
                path,
                "Notes",
                "Line 1\nLine 2"));
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
