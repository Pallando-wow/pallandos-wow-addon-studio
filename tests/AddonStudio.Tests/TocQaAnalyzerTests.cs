using AddonStudio.Core.Wow;
using AddonStudio.Wow.Toc;

namespace AddonStudio.Tests;

public sealed class TocQaAnalyzerTests
{
    [Fact]
    public async Task Analyze_ValidTocHasNoDiagnostics()
    {
        using var temp = new TempDirectory();

        var addon = Directory.CreateDirectory(
            Path.Combine(
                temp.Path,
                "Sample")).FullName;
        var tocPath = Path.Combine(
            addon,
            "Sample.toc");

        await File.WriteAllTextAsync(
            Path.Combine(
                addon,
                "Core.lua"),
            "print('ok')");

        await File.WriteAllTextAsync(
            tocPath,
            """
            ## Interface: 11507
            ## Title: Sample
            ## Version: 1.0.0
            Core.lua
            """);

        var document =
            await new TocDocumentReader()
                .ReadAsync(tocPath);

        var diagnostics =
            new TocQaAnalyzer()
                .Analyze(
                    document,
                    addon);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_ReportsMissingAndInvalidMetadata()
    {
        using var temp = new TempDirectory();

        var addon = Directory.CreateDirectory(
            Path.Combine(
                temp.Path,
                "Sample")).FullName;
        var tocPath = Path.Combine(
            addon,
            "Sample.toc");

        await File.WriteAllTextAsync(
            tocPath,
            """
            ## Interface: forever
            Core.lua
            """);

        var document =
            await new TocDocumentReader()
                .ReadAsync(tocPath);

        var diagnostics =
            new TocQaAnalyzer()
                .Analyze(
                    document,
                    addon);

        Assert.Contains(
            diagnostics,
            item => item.Code == "TOC002" &&
                    item.Severity ==
                        TocDiagnosticSeverity.Error);
        Assert.Contains(
            diagnostics,
            item => item.Code == "TOC003");
        Assert.Contains(
            diagnostics,
            item => item.Code == "TOC004");
    }

    [Fact]
    public async Task Analyze_ReportsDuplicateMetadataAndRuntimeFile()
    {
        using var temp = new TempDirectory();

        var addon = Directory.CreateDirectory(
            Path.Combine(
                temp.Path,
                "Sample")).FullName;
        var tocPath = Path.Combine(
            addon,
            "Sample.toc");

        await File.WriteAllTextAsync(
            Path.Combine(
                addon,
                "Core.lua"),
            "print('ok')");

        await File.WriteAllTextAsync(
            tocPath,
            """
            ## Interface: 11507
            ## Title: Sample
            ## Version: 1.0.0
            ## version: 1.0.1
            Core.lua
            core.lua
            """);

        var document =
            await new TocDocumentReader()
                .ReadAsync(tocPath);

        var diagnostics =
            new TocQaAnalyzer()
                .Analyze(
                    document,
                    addon);

        Assert.Contains(
            diagnostics,
            item => item.Code == "TOC005");
        Assert.Contains(
            diagnostics,
            item => item.Code == "TOC006");
    }

    [Fact]
    public async Task Analyze_ReportsMissingRuntimeFile()
    {
        using var temp = new TempDirectory();

        var addon = Directory.CreateDirectory(
            Path.Combine(
                temp.Path,
                "Sample")).FullName;
        var tocPath = Path.Combine(
            addon,
            "Sample.toc");

        await File.WriteAllTextAsync(
            tocPath,
            """
            ## Interface: 11507
            ## Title: Sample
            ## Version: 1.0.0
            Missing.lua
            """);

        var document =
            await new TocDocumentReader()
                .ReadAsync(tocPath);

        var diagnostics =
            new TocQaAnalyzer()
                .Analyze(
                    document,
                    addon);

        var missing = Assert.Single(
            diagnostics,
            item => item.Code == "TOC008");

        Assert.Equal(
            "Missing.lua",
            missing.RuntimePath);
    }

    [Theory]
    [InlineData("../Outside.lua")]
    [InlineData("/absolute.lua")]
    [InlineData("C:\\Outside.lua")]
    public async Task Analyze_RejectsRuntimePathOutsideAddon(
        string runtimePath)
    {
        using var temp = new TempDirectory();

        var addon = Directory.CreateDirectory(
            Path.Combine(
                temp.Path,
                "Sample")).FullName;
        var tocPath = Path.Combine(
            addon,
            "Sample.toc");

        await File.WriteAllTextAsync(
            tocPath,
            "## Interface: 11507\n" +
            "## Title: Sample\n" +
            "## Version: 1.0.0\n" +
            runtimePath);

        var document =
            await new TocDocumentReader()
                .ReadAsync(tocPath);

        var diagnostics =
            new TocQaAnalyzer()
                .Analyze(
                    document,
                    addon);

        Assert.Contains(
            diagnostics,
            item => item.Code == "TOC007");
    }

    [Fact]
    public async Task Analyze_ReportsInterfaceMismatchAgainstExpectedClient()
    {
        using var temp = new TempDirectory();

        var addon = Directory.CreateDirectory(
            Path.Combine(
                temp.Path,
                "Sample")).FullName;
        var tocPath = Path.Combine(
            addon,
            "Sample.toc");

        await File.WriteAllTextAsync(
            Path.Combine(
                addon,
                "Core.lua"),
            "print('ok')");

        await File.WriteAllTextAsync(
            tocPath,
            """
            ## Interface: 11509
            ## Title: Sample
            ## Version: 1.0.0
            Core.lua
            """);

        var document =
            await new TocDocumentReader()
                .ReadAsync(tocPath);

        var diagnostics =
            new TocQaAnalyzer()
                .Analyze(
                    document,
                    addon,
                    expectedInterface: 16001);

        var mismatch = Assert.Single(
            diagnostics,
            item => item.Code == "TOC009");

        Assert.Equal(
            TocDiagnosticSeverity.Warning,
            mismatch.Severity);
        Assert.Contains(
            "16001",
            mismatch.Message);
        Assert.Contains(
            "11509",
            mismatch.Message);
    }

    [Fact]
    public async Task Analyze_DoesNotReportInterfaceMismatchWhenExpectedValueIsPresent()
    {
        using var temp = new TempDirectory();

        var addon = Directory.CreateDirectory(
            Path.Combine(
                temp.Path,
                "Sample")).FullName;
        var tocPath = Path.Combine(
            addon,
            "Sample.toc");

        await File.WriteAllTextAsync(
            Path.Combine(
                addon,
                "Core.lua"),
            "print('ok')");

        await File.WriteAllTextAsync(
            tocPath,
            """
            ## Interface: 11509, 16001
            ## Title: Sample
            ## Version: 1.0.0
            Core.lua
            """);

        var document =
            await new TocDocumentReader()
                .ReadAsync(tocPath);

        var diagnostics =
            new TocQaAnalyzer()
                .Analyze(
                    document,
                    addon,
                    expectedInterface: 16001);

        Assert.DoesNotContain(
            diagnostics,
            item => item.Code == "TOC009");
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
